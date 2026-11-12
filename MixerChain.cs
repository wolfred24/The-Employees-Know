using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Property;

namespace SmartStorage;

internal static class MixerChain
{
    private const float CacheSeconds = 1.0f;

    private sealed class Entry
    {
        internal readonly HashSet<IntPtr> Destinations = new HashSet<IntPtr>();
        internal readonly Dictionary<IntPtr, MixingStation> BySource = new Dictionary<IntPtr, MixingStation>();
        internal float Until;
    }

    private static readonly Dictionary<IntPtr, Entry> Cache = new Dictionary<IntPtr, Entry>();

    internal static void Reset()
    {
        Cache.Clear();
    }

    internal static bool IsDestination(Property property, MixingStation station)
    {
        if (property == null || station == null) return false;
        try
        {
            Entry entry = Get(property);
            return entry.Destinations.Contains(station.Pointer);
        }
        catch (Exception e)
        {
            Log.Debug("MixerChain.IsDestination failed: " + e.Message);
            return false;
        }
    }

    internal static MixingStation DestinationOf(MixingStation station)
    {
        if (station == null) return null;
        try
        {
            MixingStationConfiguration config = ConfigOf(station);
            BuildableItem selected = config == null || config.Destination == null ? null : config.Destination.SelectedObject;
            return selected == null ? null : selected.TryCast<MixingStation>();
        }
        catch
        {
            return null;
        }
    }

    internal static MixingStationConfiguration ConfigOf(MixingStation station)
    {
        if (station == null) return null;
        try
        {
            MixingStationConfiguration config = station.Configuration == null
                ? null
                : station.Configuration.TryCast<MixingStationConfiguration>();
            if (config != null) return config;
        }
        catch { }

        try
        {
            var prop = AccessTools.Property(typeof(MixingStation), "stationConfiguration");
            return prop == null ? null : prop.GetValue(station, null) as MixingStationConfiguration;
        }
        catch
        {
            return null;
        }
    }

    private static Entry Get(Property property)
    {
        IntPtr key = property.Pointer;
        float now = UnityEngine.Time.realtimeSinceStartup;

        if (Cache.TryGetValue(key, out Entry cached) && now < cached.Until)
            return cached;

        var entry = new Entry { Until = now + CacheSeconds };
        Cache[key] = entry;

        var items = property.BuildableItems;
        if (items == null) return entry;

        for (int i = 0; i < items.Count; i++)
        {
            BuildableItem item = items[i];
            if (item == null) continue;
            MixingStation station = item.TryCast<MixingStation>();
            if (station == null) continue;

            MixingStation destination = DestinationOf(station);
            if (destination == null) continue;

            entry.Destinations.Add(destination.Pointer);
            entry.BySource[station.Pointer] = destination;
        }

        return entry;
    }
}
