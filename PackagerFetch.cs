using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.ObjectScripts;

namespace SmartStorage;

internal static class PackagerFetch
{
    private static readonly Dictionary<IntPtr, float> NextCheck = new Dictionary<IntPtr, float>();
    private static readonly Dictionary<IntPtr, int> RouteFrame = new Dictionary<IntPtr, int>();

    internal static void Reset()
    {
        NextCheck.Clear();
        RouteFrame.Clear();
    }

    internal static void UpdateBehaviourPostfix(Packager __instance)
    {
        if (__instance == null) return;
        if (Config.Enabled == null || !Config.Enabled.Value) return;
        if (Config.EnablePackager == null || !Config.EnablePackager.Value) return;
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
            if (RouteFrame.TryGetValue(__instance.Pointer, out int routeFrame) && routeFrame == UnityEngine.Time.frameCount) return;
            if (__instance.IsAnyWorkInProgress()) return;

            PackagerConfiguration config = __instance.configuration;
            if (config == null) return;

            var targets = new List<TargetRef>();
            AddPackagingStations(targets, config.AssignedStations);
            AddBrickPresses(targets, config.AssignedBrickPresses);

            if (targets.Count == 0) return;

            List<FetchCandidate> candidates = StorageIndex.FindCandidates(__instance, targets, true);
            for (int i = 0; i < candidates.Count; i++)
                if (FetchService.TryAssign(__instance, move, candidates[i])) return;
        }
        catch (Exception e)
        {
            Log.Debug("packager fetch failed: " + e.Message);
        }
    }

    internal static void GetTransitRouteReadyPostfix(Packager __instance, AdvancedTransitRoute __result)
    {
        if (__instance != null && __result != null)
            RouteFrame[__instance.Pointer] = UnityEngine.Time.frameCount;
    }

    private static void AddPackagingStations(List<TargetRef> targets, Il2CppSystem.Collections.Generic.List<PackagingStation> stations)
    {
        if (stations == null) return;
        string mode = Config.PackagingProductAutoFillMode == null ? "FilteredOnly" : (Config.PackagingProductAutoFillMode.Value ?? "FilteredOnly");

        for (int i = 0; i < stations.Count; i++)
        {
            PackagingStation station = stations[i];
            if (station == null) continue;

            var transit = station.TryCast<ITransitEntity>();
            if (transit == null) continue;

            // Packaging materials (baggies, jars, ...) are always fetched.
            AddSlot(targets, transit, station.PackagingSlot);

            ItemSlot productSlot = station.ProductSlot;
            if (AllowProductSlot(mode, productSlot))
                AddSlot(targets, transit, productSlot);
            else if (productSlot != null)
                Log.Debug("packaging product slot skipped (mode=" + mode + ", whitelist=" + HasPositiveWhitelist(productSlot) + ")");
        }
    }

    private static void AddBrickPresses(List<TargetRef> targets, Il2CppSystem.Collections.Generic.List<BrickPress> presses)
    {
        if (presses == null) return;
        string mode = Config.BrickPressProductAutoFillMode == null ? "FilteredOnly" : (Config.BrickPressProductAutoFillMode.Value ?? "FilteredOnly");

        for (int i = 0; i < presses.Count; i++)
        {
            BrickPress press = presses[i];
            if (press == null) continue;

            var transit = press.TryCast<ITransitEntity>();
            if (transit == null) continue;

            var products = press.ProductSlots;
            if (products == null) continue;

            for (int j = 0; j < products.Length; j++)
            {
                ItemSlot slot = products[j];
                if (AllowProductSlot(mode, slot))
                    AddSlot(targets, transit, slot);
                else if (slot != null)
                    Log.Debug("brick press product slot skipped (mode=" + mode + ", whitelist=" + HasPositiveWhitelist(slot) + ")");
            }
        }
    }

    private static void AddTargets(List<TargetRef> targets, ITransitEntity transit, Il2CppSystem.Collections.Generic.List<ItemSlot> slots)
    {
        if (slots == null) return;
        for (int i = 0; i < slots.Count; i++)
            AddSlot(targets, transit, slots[i]);
    }

    private static bool AllowProductSlot(string mode, ItemSlot slot)
    {
        if (slot == null) return false;
        if (string.Equals(mode, "Any", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(mode, "Off", StringComparison.OrdinalIgnoreCase)) return false;
        return HasPositiveWhitelist(slot);
    }

    private static bool HasPositiveWhitelist(ItemSlot slot)
    {
        if (slot == null) return false;
        try
        {
            SlotFilter filter = slot.PlayerFilter;
            if (filter == null) return false;
            if (filter.Type != SlotFilter.EType.Whitelist) return false;
            var ids = filter.ItemIDs;
            return ids != null && ids.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void AddSlot(List<TargetRef> targets, ITransitEntity entity, ItemSlot slot)
    {
        if (slot == null) return;
        targets.Add(new TargetRef { Entity = entity, Slot = slot });
    }
}
