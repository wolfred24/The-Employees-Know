using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Property;

namespace SmartStorage;

internal static class ChainMover
{
    private sealed class Watch
    {
        internal float Since;
    }

    private sealed class RouteHolder
    {
        internal TransitRoute Route;
        internal bool Owned;
    }

    private sealed class SourceInfo
    {
        internal BuildableItem Station;
        internal ItemSlot Output;
        internal ITransitEntity Entity;
        internal ITransitEntity Destination;
        internal TransitRoute Route;
    }

    private static readonly Dictionary<IntPtr, Watch> Watches = new Dictionary<IntPtr, Watch>();
    private static readonly Dictionary<IntPtr, RouteHolder> Routes = new Dictionary<IntPtr, RouteHolder>();
    private static readonly Dictionary<IntPtr, float> Reserved = new Dictionary<IntPtr, float>();

    internal static bool TryMove(Chemist chemist, MoveItemBehaviour move, ChemistConfiguration config, Property property)
    {
        if (chemist == null || move == null || config == null) return false;
        if (Config.EnableChainMover == null || !Config.EnableChainMover.Value) return false;

        try { if (move.Active) return false; }
        catch { return false; }

        var sources = new List<BuildableItem>();
        AddSources(sources, config.MixStations);
        AddSources(sources, config.ChemStations);
        AddSources(sources, config.Cauldrons);
        AddSources(sources, config.LabOvens);

        if (sources.Count == 0) return false;

        float now = UnityEngine.Time.realtimeSinceStartup;
        Cleanup(now);

        for (int s = 0; s < sources.Count; s++)
        {
            if (!TryGetSource(sources[s], out SourceInfo info)) continue;

            ItemSlot output = info.Output;
            if (output == null)
            {
                Watches.Remove(info.Station.Pointer);
                continue;
            }

            ItemInstance item = output.ItemInstance;
            if (item == null || item.Definition == null)
            {
                Watches.Remove(info.Station.Pointer);
                continue;
            }

            try
            {
                if (output.IsLocked || output.IsRemovalLocked) continue;
            }
            catch { continue; }

            if (Reserved.TryGetValue(info.Station.Pointer, out float reservedUntil) && now < reservedUntil) continue;

            int capacity;
            try
            {
                // Chain deliveries ignore the player's slot filter. This is intentional:
                // a handler whitelist on a packaging product slot must not block the
                // upstream station from delivering its output.
                capacity = info.Destination.GetInputCapacityForItem(item, chemist, false);
            }
            catch
            {
                capacity = 0;
            }

            if (capacity <= 0)
            {
                Watches.Remove(info.Station.Pointer);
                continue;
            }

            if (!Watches.TryGetValue(info.Station.Pointer, out Watch watch))
            {
                Watches[info.Station.Pointer] = new Watch { Since = now };
                continue;
            }

            float delay = Config.ChainMoveDelaySeconds == null ? 3f : Config.ChainMoveDelaySeconds.Value;
            if (delay < 0.5f) delay = 0.5f;
            if (now - watch.Since < delay) continue;

            TransitRoute route = info.Route;
            bool owned = false;

            if (route == null || !SameEndpoints(route, info.Entity, info.Destination))
            {
                route = new TransitRoute(info.Entity, info.Destination);
                owned = true;
            }

            bool reachable;
            try
            {
                reachable = move.CanGetToSource(route) && move.CanGetToDestination(route);
            }
            catch
            {
                reachable = false;
            }

            if (!reachable)
            {
                if (owned) SafeDestroy(route);
                continue;
            }

            int amount = capacity;
            if (amount < 1) amount = 1;

            StoreRoute(info.Station.Pointer, route, owned);

            try
            {
                move.Initialize(route, item.GetCopy(1), amount, false);
                move.Enable();
                Reserved[info.Station.Pointer] = now + 5f;
                Watches.Remove(info.Station.Pointer);
                Log.Debug("chain move: " + amount + "x " + item.Definition.ID + " from " + SafeName(info.Station) + " to " + SafeName(info.Destination));
                return true;
            }
            catch (Exception e)
            {
                Log.Warning("[The Employees Know] Chain move failed: " + e.Message);
                if (owned)
                {
                    SafeDestroy(route);
                    Routes.Remove(info.Station.Pointer);
                }
                return false;
            }
        }

        return false;
    }

    internal static void Reset()
    {
        foreach (RouteHolder holder in Routes.Values)
        {
            if (holder == null || !holder.Owned) continue;
            SafeDestroy(holder.Route);
        }
        Routes.Clear();
        Watches.Clear();
        Reserved.Clear();
    }

    private static void AddSources<T>(List<BuildableItem> list, Il2CppSystem.Collections.Generic.List<T> stations) where T : BuildableItem
    {
        if (stations == null) return;
        for (int i = 0; i < stations.Count; i++)
        {
            T station = stations[i];
            if (station != null) list.Add(station);
        }
    }

    private static bool TryGetSource(BuildableItem station, out SourceInfo info)
    {
        info = null;
        if (station == null) return false;

        ITransitEntity entity = station.TryCast<ITransitEntity>();
        if (entity == null) return false;

        ItemSlot output = null;
        ITransitEntity destination = null;
        TransitRoute route = null;

        MixingStation mixer = station.TryCast<MixingStation>();
        if (mixer != null)
        {
            output = mixer.OutputSlot;
            MixingStationConfiguration config = MixerChain.ConfigOf(mixer);
            if (config != null)
            {
                destination = DestinationOf(config.Destination);
                route = config.DestinationRoute;
            }
        }
        else
        {
            ChemistryStation chemistry = station.TryCast<ChemistryStation>();
            if (chemistry != null)
            {
                output = chemistry.OutputSlot;
                ChemistryStationConfiguration config = chemistry.Configuration == null ? null : chemistry.Configuration.TryCast<ChemistryStationConfiguration>();
                if (config != null)
                {
                    destination = DestinationOf(config.Destination);
                    route = config.DestinationRoute;
                }
            }
            else
            {
                Cauldron cauldron = station.TryCast<Cauldron>();
                if (cauldron != null)
                {
                    output = cauldron.OutputSlot;
                    CauldronConfiguration config = cauldron.Configuration == null ? null : cauldron.Configuration.TryCast<CauldronConfiguration>();
                    if (config != null)
                    {
                        destination = DestinationOf(config.Destination);
                        route = config.DestinationRoute;
                    }
                }
                else
                {
                    LabOven oven = station.TryCast<LabOven>();
                    if (oven != null)
                    {
                        output = oven.OutputSlot;
                        LabOvenConfiguration config = oven.Configuration == null ? null : oven.Configuration.TryCast<LabOvenConfiguration>();
                        if (config != null)
                        {
                            destination = DestinationOf(config.Destination);
                            route = config.DestinationRoute;
                        }
                    }
                }
            }
        }

        if (output == null || destination == null) return false;
        if (destination.Pointer == entity.Pointer) return false;

        info = new SourceInfo
        {
            Station = station,
            Output = output,
            Entity = entity,
            Destination = destination,
            Route = route,
        };
        return true;
    }

    private static ITransitEntity DestinationOf(ObjectField field)
    {
        if (field == null || field.SelectedObject == null) return null;
        return field.SelectedObject.TryCast<ITransitEntity>();
    }

    private static void StoreRoute(IntPtr key, TransitRoute route, bool owned)
    {
        if (Routes.TryGetValue(key, out RouteHolder old) && old != null && old.Owned && old.Route != null && old.Route != route)
            SafeDestroy(old.Route);

        if (owned)
            Routes[key] = new RouteHolder { Route = route, Owned = true };
        else
            Routes.Remove(key);
    }

    private static void Cleanup(float now)
    {
        if (Reserved.Count == 0) return;
        var expired = new List<IntPtr>();
        foreach (KeyValuePair<IntPtr, float> pair in Reserved)
            if (now >= pair.Value) expired.Add(pair.Key);
        for (int i = 0; i < expired.Count; i++) Reserved.Remove(expired[i]);
    }

    private static bool SameEndpoints(TransitRoute route, ITransitEntity source, ITransitEntity destination)
    {
        try
        {
            return route != null
                && route.Source != null
                && route.Destination != null
                && route.Source.Pointer == source.Pointer
                && route.Destination.Pointer == destination.Pointer;
        }
        catch
        {
            return false;
        }
    }

    private static void SafeDestroy(TransitRoute route)
    {
        if (route == null) return;
        try { route.Destroy(); } catch { }
    }

    private static string SafeName(BuildableItem station)
    {
        try { return station == null ? "?" : (station.name ?? "station"); }
        catch { return "station"; }
    }

    private static string SafeName(ITransitEntity entity)
    {
        try { return entity == null ? "?" : (entity.Name ?? "destination"); }
        catch { return "destination"; }
    }
}
