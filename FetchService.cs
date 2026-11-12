using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.NPCs.Behaviour;

namespace SmartStorage;

internal static class FetchService
{
    private static readonly Dictionary<IntPtr, TransitRoute> Routes = new Dictionary<IntPtr, TransitRoute>();

    internal static bool TryAssign(Employee employee, MoveItemBehaviour move, FetchCandidate candidate)
    {
        if (employee == null || move == null || candidate == null) return false;
        if (candidate.Source == null || !candidate.Source.IsValid) return false;
        if (candidate.Target == null || candidate.Target.Entity == null) return false;
        if (candidate.Item == null || candidate.Item.Definition == null) return false;

        try
        {
            if (move.Active) return false;
        }
        catch { return false; }

        TransitRoute route = null;
        bool keep = false;

        try
        {
            route = new TransitRoute(candidate.Source.Transit, candidate.Target.Entity);

            if (!move.CanGetToSource(route) || !move.CanGetToDestination(route))
                return false;

            string itemId = candidate.Item.Definition.ID;
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            if (!move.IsTransitRouteValid(route, itemId)) return false;

            int amount = candidate.Capacity;
            if (amount < 1) amount = 1;

            if (Routes.TryGetValue(employee.Pointer, out TransitRoute old) && old != null)
            {
                try { old.Destroy(); } catch { }
                Routes.Remove(employee.Pointer);
            }

            Routes[employee.Pointer] = route;
            keep = true;

            move.Initialize(route, candidate.Item.GetCopy(1), amount, false);
            move.Enable();
            StorageIndex.Reserve(candidate.SourceSlot);

            Log.Debug("assigned " + amount + "x " + itemId + " from " + candidate.Source.Name
                + " to " + SafeName(candidate.Target.Entity) + " for " + SafeName(employee));
            return true;
        }
        catch (Exception e)
        {
            Log.Warning("[The Employees Know] Could not assign a fetch to " + SafeName(employee) + ": " + e.Message);
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

    private static string SafeName(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase obj)
    {
        try
        {
            if (obj == null) return "?";
            var employee = obj.TryCast<Employee>();
            if (employee != null) return employee.FullName ?? "?";
            var transit = obj.TryCast<ITransitEntity>();
            if (transit != null) return transit.Name ?? "?";
            return "?";
        }
        catch { return "?"; }
    }
}
