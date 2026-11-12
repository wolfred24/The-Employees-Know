using System;
using System.Collections.Generic;
using Il2CppScheduleOne.ItemFramework;

namespace SmartStorage;

internal static class SlotFilters
{
    internal static bool HasPositiveWhitelist(ItemSlot slot)
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

    internal static HashSet<string> WhitelistIds(ItemSlot slot)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (slot == null) return result;
        try
        {
            SlotFilter filter = slot.PlayerFilter;
            if (filter == null || filter.Type != SlotFilter.EType.Whitelist) return result;
            var ids = filter.ItemIDs;
            if (ids == null) return result;
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                if (!string.IsNullOrWhiteSpace(id)) result.Add(id);
            }
        }
        catch { }
        return result;
    }

    internal static SlotFilter BuildWhitelist(IEnumerable<string> ids)
    {
        var filter = new SlotFilter
        {
            Type = SlotFilter.EType.Whitelist,
            ItemIDs = new Il2CppSystem.Collections.Generic.List<string>(),
            AllowedQualities = AllQualities(),
        };

        if (ids != null)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string id in ids)
            {
                if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) continue;
                filter.ItemIDs.Add(id);
            }
        }

        return filter;
    }

    internal static SlotFilter BuildNone()
    {
        return new SlotFilter
        {
            Type = SlotFilter.EType.None,
            ItemIDs = new Il2CppSystem.Collections.Generic.List<string>(),
            AllowedQualities = AllQualities(),
        };
    }

    private static Il2CppSystem.Collections.Generic.List<EQuality> AllQualities()
    {
        var qualities = new Il2CppSystem.Collections.Generic.List<EQuality>();
        qualities.Add(EQuality.Trash);
        qualities.Add(EQuality.Poor);
        qualities.Add(EQuality.Standard);
        qualities.Add(EQuality.Premium);
        qualities.Add(EQuality.Heavenly);
        return qualities;
    }
}
