using System;
using System.Collections.Generic;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.StationFramework;

namespace SmartStorage;

internal static class CookableItems
{
    private static readonly Dictionary<IntPtr, bool> Cache = new Dictionary<IntPtr, bool>();

    internal static void Reset()
    {
        Cache.Clear();
    }

    internal static bool IsCookable(ItemInstance item)
    {
        if (item == null || item.Definition == null) return false;

        IntPtr key = item.Definition.Pointer;
        if (key != IntPtr.Zero && Cache.TryGetValue(key, out bool known))
            return known;

        bool result = IsCookableUncached(item);
        if (key != IntPtr.Zero) Cache[key] = result;
        try { Log.Debug("cookable " + item.Definition.ID + " = " + result); } catch { }
        return result;
    }

    private static bool IsCookableUncached(ItemInstance item)
    {
        try
        {
            StorableItemDefinition storable = item.Definition.TryCast<StorableItemDefinition>();
            if (storable == null) return false;

            StationItem stationItem = storable.StationItem;
            if (stationItem == null) return false;

            try
            {
                var modules = stationItem.Modules;
                if (modules != null)
                {
                    for (int i = 0; i < modules.Count; i++)
                    {
                        ItemModule module = modules[i];
                        if (module != null && module.TryCast<CookableModule>() != null)
                            return true;
                    }
                }
            }
            catch { }

            try
            {
                if (stationItem.GetComponentInChildren<CookableModule>(true) != null) return true;
                if (stationItem.GetComponent<CookableModule>() != null) return true;
            }
            catch { }

            return false;
        }
        catch (Exception e)
        {
            Log.Debug("cookable check failed for " + (item.Definition != null ? item.Definition.ID : "?") + ": " + e.Message);
            return false;
        }
    }
}
