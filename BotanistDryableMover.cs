using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.ObjectScripts;

namespace SmartStorage;

internal static class BotanistDryableMover
{
    private static readonly Dictionary<IntPtr, TransitRoute> Routes = new Dictionary<IntPtr, TransitRoute>();

    internal static bool TryMove(Botanist botanist, MoveItemBehaviour move, StorageRef source,
        ItemSlot sourceSlot, QualityItemInstance item, DryingRack rack, int amount)
    {
        if (rack == null) return false;
        ITransitEntity destination = rack.TryCast<ITransitEntity>();
        return TryMoveTo(botanist, move, source, sourceSlot, item, destination, "drying rack", amount);
    }

    internal static bool TryMoveTo(Botanist botanist, MoveItemBehaviour move, StorageRef source,
        ItemSlot sourceSlot, ItemInstance item, ITransitEntity destination, string label, int amount = 1)
    {
        if (botanist == null || move == null || source == null || !source.IsValid
            || item == null || item.Definition == null || destination == null) return false;

        try { if (move.Active) return false; }
        catch { return false; }

        TransitRoute route = null;
        bool keep = false;

        try
        {
            route = new TransitRoute(source.Transit, destination);
            if (!move.CanGetToSource(route) || !move.CanGetToDestination(route)) return false;

            string itemId = item.Definition.ID;
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            if (!move.IsTransitRouteValid(route, itemId)) return false;

            if (Routes.TryGetValue(botanist.Pointer, out TransitRoute old) && old != null)
            {
                try { old.Destroy(); } catch { }
                Routes.Remove(botanist.Pointer);
            }

            Routes[botanist.Pointer] = route;
            keep = true;

            if (amount < 1) amount = 1;
            move.Initialize(route, item.GetCopy(1), amount, false);
            move.Enable();
            StorageIndex.Reserve(sourceSlot);

            Log.Debug("botanist " + SafeName(botanist) + " moving " + amount + "x " + itemId + " from "
                + source.Name + " to " + label + ".");
            return true;
        }
        catch (Exception e)
        {
            Log.Debug("botanist item move failed: " + e.Message);
            return false;
        }
        finally
        {
            if (!keep && route != null)
            {
                try { route.Destroy(); } catch { }
            }
        }
    }

    internal static void Reset()
    {
        foreach (TransitRoute route in Routes.Values)
        {
            if (route == null) continue;
            try { route.Destroy(); } catch { }
        }
        Routes.Clear();
    }

    private static string SafeName(Botanist botanist)
    {
        try { return botanist == null ? "?" : (botanist.FullName ?? "?"); }
        catch { return "?"; }
    }
}
