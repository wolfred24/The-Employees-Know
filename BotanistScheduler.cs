using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.NPCs;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.StationFramework;
using NpcBehaviour = Il2CppScheduleOne.NPCs.Behaviour.Behaviour;

namespace SmartStorage;

internal static class BotanistScheduler
{
    internal static bool HookInstalled;

    private sealed class StorageCacheEntry
    {
        internal List<StorageRef> Storages;
        internal float Until;
    }

    private static readonly Dictionary<IntPtr, float> NextCheck = new Dictionary<IntPtr, float>();
    private static readonly HashSet<IntPtr> LoggedStartup = new HashSet<IntPtr>();
    private static readonly HashSet<IntPtr> Phase2Logged = new HashSet<IntPtr>();
    private static readonly Dictionary<IntPtr, StorageCacheEntry> StorageCache = new Dictionary<IntPtr, StorageCacheEntry>();
    private static readonly List<Botanist> CachedBotanists = new List<Botanist>();
    private static float NextScan;
    private static float NextBotanistScan;
    private static float LastHookTick;

    internal static void Reset()
    {
        NextCheck.Clear();
        LoggedStartup.Clear();
        Phase2Logged.Clear();
        StorageCache.Clear();
        CachedBotanists.Clear();
        NextScan = 0f;
        NextBotanistScan = 0f;
        LastHookTick = 0f;
    }

    internal static void OnEmployeeTick(Employee employee)
    {
        if (!Enabled()) return;

        LastHookTick = UnityEngine.Time.realtimeSinceStartup;

        try
        {
            Botanist botanist = employee == null ? null : employee.TryCast<Botanist>();
            if (botanist != null) ProcessBotanist(botanist);
        }
        catch (Exception e)
        {
            Log.Debug("botanist scheduler hook failed: " + e.Message);
        }
    }

    // Fallback only. Do not use FindObjectsOfType here: it is the main source of
    // periodic spikes. The native Employee.OnTick hook is used when available.
    internal static void Tick()
    {
        if (!Enabled()) return;

        float now = UnityEngine.Time.realtimeSinceStartup;
        if (HookInstalled && now - LastHookTick < 5f) return;
        if (now < NextScan) return;
        NextScan = now + 0.5f;

        if (now >= NextBotanistScan)
        {
            RefreshBotanists();
            NextBotanistScan = now + 10f;
        }

        for (int i = 0; i < CachedBotanists.Count; i++)
            ProcessBotanist(CachedBotanists[i]);
    }

    private static void RefreshBotanists()
    {
        CachedBotanists.Clear();
        var seen = new HashSet<IntPtr>();

        try
        {
            var registry = NPCManager.NPCRegistry;
            if (registry != null)
            {
                for (int i = 0; i < registry.Count; i++)
                {
                    var npc = registry[i];
                    if (npc == null) continue;
                    Botanist botanist = npc.TryCast<Botanist>();
                    if (botanist != null && seen.Add(botanist.Pointer)) CachedBotanists.Add(botanist);
                }
            }
        }
        catch (Exception e)
        {
            Log.Debug("botanist registry scan failed: " + e.Message);
        }

        if (CachedBotanists.Count > 0) return;

        try
        {
            var properties = Property.OwnedProperties;
            if (properties == null) return;

            for (int p = 0; p < properties.Count; p++)
            {
                var property = properties[p];
                var employees = property == null ? null : property.Employees;
                if (employees == null) continue;

                for (int i = 0; i < employees.Count; i++)
                {
                    var employee = employees[i];
                    if (employee == null) continue;
                    Botanist botanist = employee.TryCast<Botanist>();
                    if (botanist != null && seen.Add(botanist.Pointer)) CachedBotanists.Add(botanist);
                }
            }
        }
        catch (Exception e)
        {
            Log.Debug("botanist property scan failed: " + e.Message);
        }
    }

    private static void ProcessBotanist(Botanist botanist)
    {
        if (!Enabled() || botanist == null) return;

        float now = UnityEngine.Time.realtimeSinceStartup;
        if (NextCheck.TryGetValue(botanist.Pointer, out float next) && now < next) return;
        NextCheck[botanist.Pointer] = now + 0.5f;

        try
        {
            if (botanist.Fired) return;
            if (!botanist.CanWork()) return;

            MoveItemBehaviour move = botanist.MoveItemBehaviour;
            if (move != null && move.Active) return;
            if (botanist.IsAnyWorkInProgress()) return;

            BotanistConfiguration config = botanist.configuration;
            if (config == null) return;

            Property property = botanist.AssignedProperty;
            if (property == null) return;

            if (LoggedStartup.Add(botanist.Pointer))
                Log.Msg("[The Employees Know] botanist scheduler for " + SafeName(botanist) + ": "
                    + Count(config.AssignedRacks) + " rack(s), "
                    + Count(config.AssignedSpawnStations) + " spawn station(s).");

            if (TryDryingRacks(botanist, config, property)) return;
            if (TrySpawnStations(botanist, config, property)) return;
        }
        catch (Exception e)
        {
            Log.Debug("botanist scheduler failed: " + e.Message);
        }
    }

    private static bool TryDryingRacks(Botanist botanist, BotanistConfiguration config, Property property)
    {
        Il2CppSystem.Collections.Generic.List<DryingRack> racks = config.AssignedRacks;
        if (racks == null || racks.Count == 0) return false;

        MoveItemBehaviour move = botanist.MoveItemBehaviour;
        if (move == null) return false;

        List<StorageRef> storages = null;

        for (int i = 0; i < racks.Count; i++)
        {
            DryingRack rack = racks[i];
            if (!CanLoadRack(botanist, rack)) continue;

            if (storages == null)
            {
                storages = GetStorages(property);
                if (storages == null || storages.Count == 0) return false;
            }

            if (!BotanistSupply.FindDryableForSlot(botanist, storages, rack.InputSlot, rack.ItemCapacity,
                    out StorageRef source, out ItemSlot sourceSlot, out QualityItemInstance dryable, out int amount))
                continue;

            if (BotanistDryableMover.TryMove(botanist, move, source, sourceSlot, dryable, rack, amount))
            {
                LogPhase2(botanist, "load drying rack from " + source.Name + " (" + amount + " item(s)).");
                return true;
            }
        }

        return false;
    }

    private static bool TrySpawnStations(Botanist botanist, BotanistConfiguration config, Property property)
    {
        UseSpawnStationBehaviour behaviour = botanist._useSpawnStationBehaviour;
        if (behaviour == null || !IsFree(behaviour)) return false;

        Il2CppSystem.Collections.Generic.List<MushroomSpawnStation> stations = config.AssignedSpawnStations;
        if (stations == null || stations.Count == 0) return false;

        for (int i = 0; i < stations.Count; i++)
        {
            MushroomSpawnStation station = stations[i];
            if (station == null) continue;

            bool ready;
            try { ready = behaviour.IsStationReady(station); }
            catch { continue; }
            if (!ready) continue;

            try
            {
                behaviour.AssignStation(station);
                if (!behaviour.Active && !behaviour.Started) behaviour.Enable();
                LogPhase2(botanist, "use spawn station.");
                return true;
            }
            catch (Exception e)
            {
                Log.Debug("botanist scheduler spawn station failed: " + e.Message);
            }
        }

        MoveItemBehaviour move = botanist.MoveItemBehaviour;
        if (move == null) return false;

        MushroomSpawnStation target = null;
        for (int i = 0; i < stations.Count; i++)
        {
            if (NeedsSpawnFill(botanist, stations[i]))
            {
                target = stations[i];
                break;
            }
        }

        if (target == null) return false;

        List<StorageRef> storages = GetStorages(property);
        if (storages == null || storages.Count == 0) return false;

        return TryFillSpawnStation(botanist, target, storages, move);
    }

    private static bool NeedsSpawnFill(Botanist botanist, MushroomSpawnStation station)
    {
        if (station == null) return false;
        try
        {
            if (station.NPCUserObject != null)
            {
                var self = botanist.NetworkObject;
                if (self == null || station.NPCUserObject.Pointer != self.Pointer) return false;
            }

            if (!station.DoesStationHaveOutputSpace()) return false;

            if (station.GrainBagSlot != null && station.GrainBagSlot.ItemInstance == null
                && !station.GrainBagSlot.IsLocked && !station.GrainBagSlot.IsAddLocked)
                return true;

            if (station.SyringeSlot != null && station.SyringeSlot.ItemInstance == null
                && !station.SyringeSlot.IsLocked && !station.SyringeSlot.IsAddLocked)
                return true;
        }
        catch { }
        return false;
    }

    private static bool TryFillSpawnStation(Botanist botanist, MushroomSpawnStation station,
        List<StorageRef> storages, MoveItemBehaviour move)
    {
        try
        {
            if (station.NPCUserObject != null)
            {
                var self = botanist.NetworkObject;
                if (self == null || station.NPCUserObject.Pointer != self.Pointer) return false;
            }

            if (!station.DoesStationHaveOutputSpace()) return false;

            ItemSlot destination = null;
            if (station.GrainBagSlot != null && station.GrainBagSlot.ItemInstance == null)
                destination = station.GrainBagSlot;
            else if (station.SyringeSlot != null && station.SyringeSlot.ItemInstance == null)
                destination = station.SyringeSlot;

            if (destination == null) return false;
            if (destination.IsLocked || destination.IsAddLocked) return false;

            ITransitEntity stationTransit = station.TryCast<ITransitEntity>();
            if (stationTransit == null) return false;

            for (int s = 0; s < storages.Count; s++)
            {
                StorageRef storage = storages[s];
                if (!storage.IsValid || storage.OutputSlots == null) continue;
                if (!BotanistSupply.CanReach(botanist, storage)) continue;

                for (int i = 0; i < storage.OutputSlots.Count; i++)
                {
                    ItemSlot sourceSlot = storage.OutputSlots[i];
                    if (sourceSlot == null) continue;

                    ItemInstance item;
                    try
                    {
                        if (sourceSlot.ItemInstance == null || sourceSlot.IsLocked || sourceSlot.IsRemovalLocked)
                            continue;
                        item = sourceSlot.ItemInstance;
                        if (item.Definition == null) continue;
                        if (!destination.DoesItemMatchHardFilters(item)) continue;
                    }
                    catch { continue; }

                    if (BotanistDryableMover.TryMoveTo(botanist, move, storage, sourceSlot, item, stationTransit, "spawn station"))
                    {
                        LogPhase2(botanist, "load spawn station from " + storage.Name + ".");
                        return true;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Debug("botanist scheduler spawn station fill failed: " + e.Message);
        }

        return false;
    }

    private static bool CanLoadRack(Botanist botanist, DryingRack rack)
    {
        if (rack == null) return false;
        try
        {
            if (rack.IsOpen) return false;

            var user = rack.NPCUserObject;
            if (user != null)
            {
                var self = botanist.NetworkObject;
                if (self == null || user.Pointer != self.Pointer) return false;
            }

            if (!rack.IsAcceptingItems) return false;
            if (rack.InputSlot == null) return false;
            if (rack.InputSlot.IsLocked || rack.InputSlot.IsAddLocked) return false;
            if (rack.InputSlot.IsAtCapacity) return false;
            if (rack.OutputSlot != null && rack.OutputSlot.IsAtCapacity) return false;
            if (rack.DryingOperations != null && rack.DryingOperations.Count > 0) return false;
            return true;
        }
        catch { return false; }
    }

    private static List<StorageRef> GetStorages(Property property)
    {
        if (property == null) return null;

        float now = UnityEngine.Time.realtimeSinceStartup;
        if (StorageCache.TryGetValue(property.Pointer, out StorageCacheEntry entry) && now < entry.Until)
            return entry.Storages;

        if (entry == null)
        {
            entry = new StorageCacheEntry();
            StorageCache[property.Pointer] = entry;
        }

        if (entry.Storages == null) entry.Storages = new List<StorageRef>();
        StorageIndex.CollectForProperty(property, true, entry.Storages);
        entry.Until = now + 1f;
        return entry.Storages;
    }

    private static bool IsFree(NpcBehaviour behaviour)
    {
        try { return behaviour != null && !behaviour.Active && !behaviour.Started; }
        catch { return false; }
    }

    private static int Count<T>(Il2CppSystem.Collections.Generic.List<T> list)
    {
        try { return list == null ? 0 : list.Count; }
        catch { return 0; }
    }

    private static void LogPhase2(Botanist botanist, string message)
    {
        if (Phase2Logged.Add(botanist.Pointer))
            Log.Msg("[The Employees Know] botanist " + SafeName(botanist) + ": " + message);
        else
            Log.Debug("botanist " + SafeName(botanist) + ": " + message);
    }

    private static string SafeName(Botanist botanist)
    {
        try { return botanist == null ? "?" : (botanist.FullName ?? "?"); }
        catch { return "?"; }
    }

    private static bool Enabled()
    {
        if (!Net.IsServer) return false;
        return Config.Enabled != null && Config.Enabled.Value
            && Config.EnableBotanist != null && Config.EnableBotanist.Value;
    }
}
