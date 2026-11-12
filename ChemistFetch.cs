using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.StationFramework;

namespace SmartStorage;

internal static class ChemistFetch
{
    private static readonly Dictionary<IntPtr, float> NextCheck = new Dictionary<IntPtr, float>();

    internal static void Reset()
    {
        NextCheck.Clear();
    }

    internal static void UpdateBehaviourPostfix(Chemist __instance)
    {
        if (__instance == null) return;
        if (Config.Enabled == null || !Config.Enabled.Value) return;
        if (Config.EnableChemist == null || !Config.EnableChemist.Value) return;
        if (!Net.IsServer) return;

        float now = UnityEngine.Time.realtimeSinceStartup;
        if (NextCheck.TryGetValue(__instance.Pointer, out float next) && now < next) return;

        float interval = Config.SearchIntervalSeconds == null ? 0.75f : Config.SearchIntervalSeconds.Value;
        if (interval < 0.1f) interval = 0.1f;
        NextCheck[__instance.Pointer] = now + interval;

        try
        {
            if (__instance.Fired) return;
            if (!__instance.CanWork()) return;

            MoveItemBehaviour move = __instance.MoveItemBehaviour;
            if (move == null) return;
            if (move.Active) return;
            if (__instance.IsAnyWorkInProgress()) return;

            ChemistConfiguration config = __instance.configuration;
            if (config == null) return;

            Property property = __instance.AssignedProperty;
            if (ChainMover.TryMove(__instance, move, config, property)) return;

            var targets = new List<TargetRef>();
            if (Config.RecipeAwareChemistry == null || Config.RecipeAwareChemistry.Value)
                AddChemistryStations(targets, config.ChemStations);
            else
                AddStations(targets, config.ChemStations);

            if (Config.AutoFillLabOvens == null || Config.AutoFillLabOvens.Value)
                AddLabOvens(targets, config.LabOvens);
            AddStations(targets, config.Cauldrons);
            AddMixStations(targets, config.MixStations, property);

            if (targets.Count == 0) return;

            List<FetchCandidate> candidates = StorageIndex.FindCandidates(__instance, targets, true);
            for (int i = 0; i < candidates.Count; i++)
                if (FetchService.TryAssign(__instance, move, candidates[i])) return;
        }
        catch (Exception e)
        {
            Log.Debug("chemist fetch failed: " + e.Message);
        }
    }

    private static void AddStations<T>(List<TargetRef> targets, Il2CppSystem.Collections.Generic.List<T> stations) where T : Il2CppScheduleOne.EntityFramework.BuildableItem
    {
        if (stations == null) return;
        for (int i = 0; i < stations.Count; i++)
        {
            T station = stations[i];
            if (station == null) continue;
            AddTargets(targets, station, station.TryCast<ITransitEntity>());
        }
    }

    private static void AddLabOvens(List<TargetRef> targets, Il2CppSystem.Collections.Generic.List<LabOven> ovens)
    {
        if (ovens == null) return;

        bool cookableOnly = Config.LabOvenCookableOnly == null || Config.LabOvenCookableOnly.Value;

        for (int i = 0; i < ovens.Count; i++)
        {
            LabOven oven = ovens[i];
            if (oven == null) continue;

            var transit = oven.TryCast<ITransitEntity>();
            ItemSlot slot = oven.IngredientSlot;
            if (transit == null || slot == null) continue;

            try
            {
                if (oven.CurrentOperation != null) continue;
                if (slot.IsLocked || slot.IsAddLocked) continue;
            }
            catch { continue; }

            var targetRef = new TargetRef
            {
                Entity = transit,
                Slot = slot,
            };
            if (cookableOnly)
                targetRef.ItemFilter = CookableItems.IsCookable;

            targets.Add(targetRef);
        }
    }

    private static void AddChemistryStations(List<TargetRef> targets, Il2CppSystem.Collections.Generic.List<ChemistryStation> stations)
    {
        if (stations == null) return;
        bool requireFilter = Config.ChemistryRequirePositiveFilter == null || Config.ChemistryRequirePositiveFilter.Value;

        for (int i = 0; i < stations.Count; i++)
        {
            ChemistryStation station = stations[i];
            if (station == null) continue;

            var transit = station.TryCast<ITransitEntity>();
            if (transit == null) continue;

            ChemistryFilters.ApplyIfNeeded(station);
            StationRecipe recipe = ChemistryFilters.GetRecipe(station);
            if (recipe == null)
            {
                Log.Debug("chemistry station without recipe skipped: " + SafeStationName(station));
                continue;
            }

            var slots = station.IngredientSlots;
            var groups = recipe.Ingredients;
            if (slots == null || groups == null) continue;

            int count = Math.Min(Math.Min(slots.Length, groups.Count), 3);
            for (int g = 0; g < count; g++)
            {
                ItemSlot slot = slots[g];
                if (slot == null) continue;

                if (requireFilter && !SlotFilters.HasPositiveWhitelist(slot))
                {
                    Log.Debug("chemistry ingredient slot " + g + " skipped: no positive filter.");
                    continue;
                }

                HashSet<string> recipeIds = ChemistryFilters.GroupIds(groups[g]);
                if (recipeIds.Count == 0) continue;

                HashSet<string> filterIds = SlotFilters.WhitelistIds(slot);
                HashSet<string> allowed;
                if (filterIds.Count == 0)
                    allowed = requireFilter ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) : recipeIds;
                else
                    allowed = Intersect(recipeIds, filterIds);

                if (allowed.Count == 0)
                {
                    Log.Debug("chemistry ingredient slot " + g + " skipped: filter does not match the recipe.");
                    continue;
                }

                ItemInstance item = slot.ItemInstance;
                if (item != null)
                {
                    string id;
                    try { id = item.Definition == null ? null : item.Definition.ID; }
                    catch { id = null; }

                    if (string.IsNullOrWhiteSpace(id) || !allowed.Contains(id))
                    {
                        Log.Debug("chemistry ingredient slot " + g + " contains an item outside the recipe; skipped.");
                        continue;
                    }
                }

                targets.Add(new TargetRef
                {
                    Entity = transit,
                    Slot = slot,
                    AllowedItemIds = allowed,
                });
            }
        }
    }

    private static HashSet<string> Intersect(HashSet<string> a, HashSet<string> b)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in a)
            if (b.Contains(id)) result.Add(id);
        return result;
    }

    private static string SafeStationName(ChemistryStation station)
    {
        try { return station == null ? "?" : (station.Name ?? "chemistry station"); }
        catch { return "chemistry station"; }
    }

    private static void AddMixStations(List<TargetRef> targets, Il2CppSystem.Collections.Generic.List<MixingStation> stations, Property property)
    {
        if (stations == null) return;
        bool chainAware = Config.ChainAwareMixers == null || Config.ChainAwareMixers.Value;

        for (int i = 0; i < stations.Count; i++)
        {
            MixingStation station = stations[i];
            if (station == null) continue;

            var transit = station.TryCast<ITransitEntity>();
            if (transit == null) continue;

            if (!chainAware)
            {
                var inputs = station.InputSlots;
                if (inputs != null && inputs.Count > 0)
                {
                    AddTargets(targets, station, transit);
                    continue;
                }

                AddSlot(targets, transit, station.MixerSlot);
                AddSlot(targets, transit, station.ProductSlot);
                continue;
            }

            // The mixer slot is always available.
            AddSlot(targets, transit, station.MixerSlot);

            // The product slot is only available on the first mixer of a chain.
            // Every mixer that is another mixer's destination must wait for that output.
            if (property == null || !MixerChain.IsDestination(property, station))
                AddSlot(targets, transit, station.ProductSlot);
        }
    }

    private static void AddTargets(List<TargetRef> targets, Il2CppScheduleOne.EntityFramework.BuildableItem station, ITransitEntity transit)
    {
        if (transit == null) return;
        var slots = transit.InputSlots;
        if (slots == null) return;
        for (int i = 0; i < slots.Count; i++)
            AddSlot(targets, transit, slots[i]);
    }

    private static void AddSlot(List<TargetRef> targets, ITransitEntity entity, ItemSlot slot)
    {
        if (slot == null) return;
        targets.Add(new TargetRef { Entity = entity, Slot = slot });
    }
}
