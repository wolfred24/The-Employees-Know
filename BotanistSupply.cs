using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Property;

namespace SmartStorage;

internal static class BotanistSupply
{
    private sealed class Chosen
    {
        internal StorageRef Storage;
        internal float Until;
        internal string Kind;
    }

    private static readonly Dictionary<IntPtr, Chosen> ChosenByBotanist = new Dictionary<IntPtr, Chosen>();
    private static readonly Dictionary<(IntPtr Botanist, IntPtr Storage), (bool Value, float Until)> Reachability =
        new Dictionary<(IntPtr, IntPtr), (bool, float)>();


    internal static void Reset()
    {
        ChosenByBotanist.Clear();
        Reachability.Clear();
    }

    internal static bool CanReach(Botanist botanist, StorageRef storage)
    {
        if (botanist == null || storage == null || !storage.IsValid) return false;
        if (storage.Transit == null) return false;

        var key = (botanist.Pointer, storage.Transit.Pointer);
        float now = UnityEngine.Time.realtimeSinceStartup;
        if (Reachability.TryGetValue(key, out var cached) && now < cached.Until) return cached.Value;

        bool reachable;
        try { reachable = botanist.Movement.CanGetTo(storage.Transit, 1f); }
        catch { reachable = true; }

        Reachability[key] = (reachable, now + 5f);
        return reachable;
    }

    internal static void GetDryableInSuppliesPrefix(Botanist __instance)
    {
        if (__instance == null || !Enabled()) return;
        // Let the vanilla supply locker answer first for drying racks. If it has a
        // dryable, the postfix keeps it. If not, the postfix searches all enabled storages.
        ChosenByBotanist.Remove(__instance.Pointer);
    }

    internal static void GetDryableInSuppliesPostfix(Botanist __instance, ref QualityItemInstance __result)
    {
        if (__instance == null || !Enabled()) return;
        if (__result != null)
        {
            if (!SupplyLockerBlocked(__instance)) return; // vanilla supply locker found one
            __result = null; // supply locker is blocked, search enabled storages instead
        }

        try
        {
            Property property = __instance.AssignedProperty;
            if (property == null) return;

            var storages = new List<StorageRef>();
            StorageIndex.CollectForProperty(property, true, storages);

            if (!FindDryable(__instance, storages, out StorageRef storage, out ItemSlot slot, out QualityItemInstance dryable))
            {
                ChosenByBotanist.Remove(__instance.Pointer);
                return;
            }

            ChosenByBotanist[__instance.Pointer] = new Chosen
            {
                Storage = storage,
                Until = UnityEngine.Time.realtimeSinceStartup + 60f,
                Kind = "dryable",
            };
            __result = dryable;
            Log.Debug("botanist " + SafeName(__instance) + " dryable supply: " + storage.Name);
        }
        catch (Exception e)
        {
            Log.Debug("botanist dryable supply postfix failed: " + e.Message);
        }
    }

    private static bool SupplyLockerBlocked(Botanist botanist)
    {
        try
        {
            var config = botanist.configuration;
            var selected = config == null || config.Supplies == null ? null : config.Supplies.SelectedObject;
            var placeable = selected == null ? null : selected.TryCast<PlaceableStorageEntity>();
            if (placeable == null) return false;
            var transit = new ITransitEntity(placeable.Pointer);
            string guid = transit.GUID.ToString();
            return !StoragePermissions.IsAllowed(guid);
        }
        catch
        {
            return false;
        }
    }

    internal static bool FindDryable(Botanist botanist, List<StorageRef> storages, out StorageRef best, out ItemSlot bestSlot, out QualityItemInstance dryable)
    {
        best = null;
        bestSlot = null;
        dryable = null;
        int bestPriority = int.MaxValue;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < storages.Count; i++)
        {
            StorageRef storage = storages[i];
            if (!storage.IsValid || storage.OutputSlots == null) continue;

            for (int s = 0; s < storage.OutputSlots.Count; s++)
            {
                ItemSlot slot = storage.OutputSlots[s];
                if (slot == null) continue;

                try
                {
                    if (slot.IsLocked || slot.IsRemovalLocked) continue;
                }
                catch { continue; }

                ItemInstance item = slot.ItemInstance;
                if (item == null) continue;

                bool isDryable;
                try { isDryable = ItemFilter_Dryable.IsItemDryable(item); }
                catch { continue; }
                if (!isDryable) continue;

                QualityItemInstance quality = item.TryCast<QualityItemInstance>();
                if (quality == null) continue;

                if (!CanReach(botanist, storage)) continue;

                float distance = (botanist.transform.position - storage.Position).sqrMagnitude;
                int priority = storage.Priority;
                if (best == null || priority < bestPriority || (priority == bestPriority && distance < bestDistance))
                {
                    best = storage;
                    bestSlot = slot;
                    dryable = quality;
                    bestPriority = priority;
                    bestDistance = distance;
                }
            }
        }

        return best != null;
    }

    internal static bool FindDryableForSlot(Botanist botanist, List<StorageRef> storages, ItemSlot destination,
        int fallbackCapacity, out StorageRef best, out ItemSlot bestSlot, out QualityItemInstance dryable, out int amount)
    {
        best = null;
        bestSlot = null;
        dryable = null;
        amount = 0;
        if (destination == null) return false;

        int bestPriority = int.MaxValue;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < storages.Count; i++)
        {
            StorageRef storage = storages[i];
            if (!storage.IsValid || storage.OutputSlots == null) continue;

            for (int s = 0; s < storage.OutputSlots.Count; s++)
            {
                ItemSlot slot = storage.OutputSlots[s];
                if (slot == null) continue;

                try
                {
                    if (slot.IsLocked || slot.IsRemovalLocked) continue;
                    if (slot.ItemInstance == null || slot.Quantity < 1) continue;
                }
                catch { continue; }

                ItemInstance item = slot.ItemInstance;
                if (item == null || item.Definition == null) continue;

                bool isDryable;
                try { isDryable = ItemFilter_Dryable.IsItemDryable(item); }
                catch { continue; }
                if (!isDryable) continue;

                QualityItemInstance quality = item.TryCast<QualityItemInstance>();
                if (quality == null) continue;

                try
                {
                    if (destination.ItemInstance != null)
                    {
                        ItemInstance current = destination.ItemInstance;
                        if (current.Definition == null || current.Definition.ID != item.Definition.ID) continue;
                    }

                    if (!destination.DoesItemMatchHardFilters(item)) continue;

                    int currentQuantity = destination.ItemInstance == null ? 0 : destination.Quantity;
                    int capacity = destination.GetCapacityForItem(item, false);
                    if (capacity <= 0) capacity = fallbackCapacity;
                    if (capacity <= currentQuantity) continue;

                    int free = capacity - currentQuantity;
                    int moveAmount = slot.Quantity;
                    if (moveAmount > free) moveAmount = free;
                    if (moveAmount < 1) continue;

                    if (!CanReach(botanist, storage)) continue;

                    float distance = (botanist.transform.position - storage.Position).sqrMagnitude;
                    int priority = storage.Priority;
                    if (best == null || priority < bestPriority || (priority == bestPriority && distance < bestDistance))
                    {
                        best = storage;
                        bestSlot = slot;
                        dryable = quality;
                        amount = moveAmount;
                        bestPriority = priority;
                        bestDistance = distance;
                    }
                }
                catch { continue; }
            }
        }

        return best != null;
    }

    internal static bool DoesBotanistHaveAccessPrefix(GrowContainerBehaviour __instance, GrowContainer __0, ref bool __result)
    {
        // If the required item is in any enabled storage, bypass the vanilla supply field entirely.
        if (!Enabled()) return true;

        try
        {
            if (!ChooseForContainer(__instance, __0, out ItemSlot slot)) return true;
            __result = true;
            return false;
        }
        catch (Exception e)
        {
            Log.Debug("DoesBotanistHaveAccess prefix failed: " + e.Message);
            return true;
        }
    }

    internal static bool DoSuppliesContainPrefix(GrowContainerBehaviour __instance, GrowContainer __0, ref bool __result)
    {
        if (!Enabled()) return true;

        try
        {
            if (!ChooseForContainer(__instance, __0, out ItemSlot slot)) return true;
            __result = true;
            return false;
        }
        catch (Exception e)
        {
            Log.Debug("DoSuppliesContain prefix failed: " + e.Message);
            return true;
        }
    }

    internal static bool GetSuppliesSlotPrefix(GrowContainerBehaviour __instance, Il2CppStringArray __0, ref ItemSlot __result)
    {
        string[] ids = ToArray(__0);
        if (ids == null || ids.Length == 0) return true;
        if (!Enabled()) return true;

        try
        {
            if (!ChooseForIds(__instance, ids, out ItemSlot slot)) return true;
            __result = slot;
            return false;
        }
        catch (Exception e)
        {
            Log.Debug("botanist supply slot prefix failed: " + e.Message);
            return true;
        }
    }

    internal static void GetSuppliesAsTransitEntityPostfix(Botanist __instance, ref ITransitEntity __result)
    {
        if (!Enabled() || __instance == null) return;

        try
        {
            bool supplyLockerBlocked = __result != null && SupplyLockerBlocked(__instance);
            if (supplyLockerBlocked)
            {
                __result = null;
                ChosenByBotanist.Remove(__instance.Pointer);
            }

            if (TryGetChosen(__instance, out Chosen chosen))
            {
                __result = chosen.Storage.Transit;
                return;
            }

            if (__result != null) return; // vanilla supply locker is valid

            // No supply locker assigned/selected, or it is blocked. Let the botanist
            // start its task by returning an accessible storage with something in it.
            // The specific grow-container/dryable checks will then narrow the source
            // to the storage that actually has the required item.
            if (ChooseAnySupplyStorage(__instance, out StorageRef fallback))
            {
                __result = fallback.Transit;
                Log.Debug("botanist " + SafeName(__instance) + " fallback supply storage: " + fallback.Name);
            }
        }
        catch (Exception e)
        {
            Log.Debug("botanist supply transit postfix failed: " + e.Message);
        }
    }

    private static bool TryGetChosen(Botanist botanist, out Chosen chosen)
    {
        chosen = null;
        if (botanist == null) return false;
        if (!ChosenByBotanist.TryGetValue(botanist.Pointer, out chosen) || chosen == null)
        {
            ChosenByBotanist.Remove(botanist.Pointer);
            return false;
        }
        if (UnityEngine.Time.realtimeSinceStartup > chosen.Until || chosen.Storage == null || !chosen.Storage.IsValid)
        {
            ChosenByBotanist.Remove(botanist.Pointer);
            chosen = null;
            return false;
        }
        return true;
    }

    private static bool ChooseAnySupplyStorage(Botanist botanist, out StorageRef chosen)
    {
        chosen = null;
        try
        {
            Property property = botanist.AssignedProperty;
            if (property == null) return false;

            var storages = new List<StorageRef>();
            StorageIndex.CollectForProperty(property, true, storages);

            StorageRef best = null;
            int bestPriority = int.MaxValue;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < storages.Count; i++)
            {
                StorageRef storage = storages[i];
                if (!storage.IsValid || storage.OutputSlots == null || storage.OutputSlots.Count == 0) continue;
                if (!HasAnyItem(storage)) continue;

                bool reachable;
                try { reachable = botanist.Movement.CanGetTo(storage.Transit, 1f); }
                catch { reachable = true; }
                if (!reachable) continue;

                float distance = (botanist.transform.position - storage.Position).sqrMagnitude;
                int priority = storage.Priority;
                if (best == null || priority < bestPriority || (priority == bestPriority && distance < bestDistance))
                {
                    best = storage;
                    bestPriority = priority;
                    bestDistance = distance;
                }
            }

            if (best == null) return false;

            chosen = best;
            ChosenByBotanist[botanist.Pointer] = new Chosen
            {
                Storage = best,
                Until = UnityEngine.Time.realtimeSinceStartup + 5f,
                Kind = "fallback",
            };
            return true;
        }
        catch (Exception e)
        {
            Log.Debug("ChooseAnySupplyStorage failed: " + e.Message);
            return false;
        }
    }

    private static bool HasAnyItem(StorageRef storage)
    {
        var slots = storage.OutputSlots;
        if (slots == null) return false;
        for (int i = 0; i < slots.Count; i++)
        {
            try
            {
                ItemSlot slot = slots[i];
                if (slot != null && slot.ItemInstance != null) return true;
            }
            catch { }
        }
        return false;
    }

    private static bool ChooseForContainer(GrowContainerBehaviour behaviour, GrowContainer container, out ItemSlot slot)
    {
        slot = null;
        if (!Enabled() || behaviour == null || container == null) return false;

        try
        {
            Botanist botanist = GetBotanist(behaviour);
            if (botanist == null)
            {
                Log.Debug("botanist supply: behaviour has no botanist.");
                return false;
            }

            string[] ids = GetRequiredItemSuitableIDs(behaviour, container);
            if (ids == null || ids.Length == 0)
            {
                ChosenByBotanist.Remove(botanist.Pointer);
                return false;
            }

            Property property = botanist.AssignedProperty;
            if (property == null)
            {
                ChosenByBotanist.Remove(botanist.Pointer);
                return false;
            }

            var storages = new List<StorageRef>();
            StorageIndex.CollectForProperty(property, true, storages);

            if (!StorageIndex.FindBestForBotanist(botanist, ids, storages, out StorageRef storage, out slot))
            {
                Log.Debug("botanist " + SafeName(botanist) + " found no supply storage for " + string.Join(",", ids) + " (" + storages.Count + " enabled storage(s)).");
                ChosenByBotanist.Remove(botanist.Pointer);
                return false;
            }

            ChosenByBotanist[botanist.Pointer] = new Chosen
            {
                Storage = storage,
                Until = UnityEngine.Time.realtimeSinceStartup + 60f,
                Kind = "container",
            };
            Log.Debug("botanist " + SafeName(botanist) + " will use " + storage.Name + " for " + string.Join(",", ids));
            return true;
        }
        catch (Exception e)
        {
            Log.Debug("ChooseForContainer failed: " + e.Message);
            return false;
        }
    }

    private static bool ChooseForIds(GrowContainerBehaviour behaviour, string[] ids, out ItemSlot slot)
    {
        slot = null;
        if (!Enabled() || behaviour == null || ids == null || ids.Length == 0) return false;

        try
        {
            Botanist botanist = GetBotanist(behaviour);
            if (botanist == null) return false;

            Property property = botanist.AssignedProperty;
            if (property == null) return false;

            var storages = new List<StorageRef>();
            StorageIndex.CollectForProperty(property, true, storages);

            if (!StorageIndex.FindBestForBotanist(botanist, ids, storages, out StorageRef storage, out slot))
            {
                Log.Debug("botanist " + SafeName(botanist) + " found no supply storage for " + string.Join(",", ids) + " (" + storages.Count + " enabled storage(s)).");
                ChosenByBotanist.Remove(botanist.Pointer);
                return false;
            }

            ChosenByBotanist[botanist.Pointer] = new Chosen
            {
                Storage = storage,
                Until = UnityEngine.Time.realtimeSinceStartup + 60f,
                Kind = "slot",
            };

            Log.Debug("botanist " + SafeName(botanist) + " supply slot: " + storage.Name);
            return true;
        }
        catch (Exception e)
        {
            Log.Debug("ChooseForIds failed: " + e.Message);
            return false;
        }
    }

    private static bool Enabled()
    {
        if (!Net.IsServer) return false;
        return Config.Enabled != null && Config.Enabled.Value
            && Config.EnableBotanist != null && Config.EnableBotanist.Value;
    }

    private static Botanist GetBotanist(GrowContainerBehaviour behaviour)
    {
        try
        {
            return Traverse.Create(behaviour).Property("_botanist").GetValue<Botanist>();
        }
        catch
        {
            try
            {
                var prop = AccessTools.Property(typeof(GrowContainerBehaviour), "_botanist");
                return prop == null ? null : prop.GetValue(behaviour, null) as Botanist;
            }
            catch
            {
                return null;
            }
        }
    }

    internal static void SetChosen(Botanist botanist, StorageRef storage, string kind)
    {
        if (botanist == null || storage == null) return;
        ChosenByBotanist[botanist.Pointer] = new Chosen
        {
            Storage = storage,
            Until = UnityEngine.Time.realtimeSinceStartup + 60f,
            Kind = kind,
        };
    }

    internal static void ClearChosen(Botanist botanist)
    {
        if (botanist == null) return;
        ChosenByBotanist.Remove(botanist.Pointer);
    }

    internal static string[] GetRequiredItemSuitableIDs(GrowContainerBehaviour behaviour, GrowContainer container)
    {
        try
        {
            var args = new object[] { container };
            var method = AccessTools.Method(behaviour.GetType(), "GetRequiredItemSuitableIDs", new[] { typeof(GrowContainer) });
            if (method == null)
                method = AccessTools.Method(typeof(GrowContainerBehaviour), "GetRequiredItemSuitableIDs", new[] { typeof(GrowContainer) });
            if (method == null) return null;
            object result = method.Invoke(behaviour, args);
            return result is Il2CppStringArray array ? ToArray(array) : (result as string[]);
        }
        catch (Exception e)
        {
            Log.Debug("GetRequiredItemSuitableIDs failed: " + e.Message);
            return null;
        }
    }

    private static string[] ToArray(Il2CppStringArray array)
    {
        if (array == null) return null;
        var result = new string[array.Length];
        for (int i = 0; i < array.Length; i++) result[i] = array[i];
        return result;
    }

    private static string SafeName(Botanist botanist)
    {
        try { return botanist == null ? "?" : (botanist.FullName ?? "?"); }
        catch { return "?"; }
    }
}
