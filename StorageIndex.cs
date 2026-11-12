using System;
using System.Collections.Generic;
using Il2CppScheduleOne.Delivery;
using Il2CppScheduleOne.DevUtilities;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Property;
using UnityEngine;

namespace SmartStorage;

internal sealed class StorageRef
{
    internal PlaceableStorageEntity Placeable;
    internal ITransitEntity Transit;
    internal string Guid;
    internal string Name;
    internal Vector3 Position;
    internal Il2CppSystem.Collections.Generic.List<ItemSlot> OutputSlots;
    internal int Priority = 1;
    internal bool IsDeliveryVehicle;

    internal bool IsValid
    {
        get
        {
            try
            {
                if (Transit == null) return false;
                if (Transit.IsDestroyed) return false;
                if (!IsDeliveryVehicle && (Placeable == null || Placeable.Pointer == IntPtr.Zero)) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

internal sealed class TargetRef
{
    internal ITransitEntity Entity;
    internal ItemSlot Slot;
    internal HashSet<string> AllowedItemIds;
    internal Func<ItemInstance, bool> ItemFilter;
}

internal sealed class FetchCandidate
{
    internal StorageRef Source;
    internal ItemSlot SourceSlot;
    internal ItemInstance Item;
    internal TargetRef Target;
    internal int Capacity;
    internal float Distance;
    internal bool TargetEmpty;
}

internal static class StorageIndex
{
    private static readonly Dictionary<IntPtr, float> Reserved = new Dictionary<IntPtr, float>();

    internal static bool TryMapStorageEntity(Il2CppScheduleOne.Storage.StorageEntity entity, out StorageRef storage)
    {
        storage = null;
        if (entity == null) return false;

        try
        {
            var direct = entity.TryCast<PlaceableStorageEntity>();
            if (direct != null)
                return TryBuild(direct, out storage);
        }
        catch { }

        try
        {
            var component = entity.GetComponent<PlaceableStorageEntity>();
            if (component != null)
                return TryBuild(component, out storage);
        }
        catch { }

        try
        {
            var properties = Property.Properties;
            if (properties == null) return false;

            for (int p = 0; p < properties.Count; p++)
            {
                Property property = properties[p];
                if (property == null) continue;
                var items = property.BuildableItems;
                if (items == null) continue;

                for (int i = 0; i < items.Count; i++)
                {
                    BuildableItem item = items[i];
                    if (item == null) continue;
                    var placeable = item.TryCast<PlaceableStorageEntity>();
                    if (placeable == null || placeable.StorageEntity == null) continue;
                    if (placeable.StorageEntity.Pointer != entity.Pointer) continue;
                    if (TryBuild(placeable, out storage)) return true;
                }
            }
        }
        catch (Exception e)
        {
            Log.Debug("TryMapStorageEntity failed: " + e.Message);
        }

        return false;
    }

    internal static bool TryBuild(PlaceableStorageEntity placeable, out StorageRef storage)
    {
        storage = null;
        if (placeable == null || placeable.Pointer == IntPtr.Zero) return false;

        try
        {
            var transit = new ITransitEntity(placeable.Pointer);
            string guid = transit.GUID.ToString();
            if (string.IsNullOrWhiteSpace(guid)) return false;

            Vector3 position = placeable.transform.position;
            var points = transit.AccessPoints;
            if (points != null && points.Length > 0 && points[0] != null)
                position = points[0].position;

            storage = new StorageRef
            {
                Placeable = placeable,
                Transit = transit,
                Guid = guid,
                Name = string.IsNullOrWhiteSpace(transit.Name) ? placeable.name : transit.Name,
                Position = position,
                OutputSlots = transit.OutputSlots,
                Priority = 1,
                IsDeliveryVehicle = false,
            };
            return true;
        }
        catch (Exception e)
        {
            Log.Debug("TryBuild storage failed: " + e.Message);
            return false;
        }
    }

    internal static bool TryBuildDeliveryDock(LoadingDock dock, out StorageRef storage)
    {
        storage = null;
        if (dock == null || dock.Pointer == IntPtr.Zero) return false;

        try
        {
            var transit = dock.TryCast<ITransitEntity>();
            if (transit == null) return false;

            string guid = transit.GUID.ToString();
            if (string.IsNullOrWhiteSpace(guid)) guid = "dock:" + dock.Pointer;

            Vector3 position = dock.transform.position;
            var points = transit.AccessPoints;
            if (points != null && points.Length > 0 && points[0] != null)
                position = points[0].position;

            bool prefer = Config.PreferDeliveryVehicles == null || Config.PreferDeliveryVehicles.Value;

            storage = new StorageRef
            {
                Placeable = null,
                Transit = transit,
                Guid = guid,
                Name = string.IsNullOrWhiteSpace(transit.Name) ? "Delivery vehicle" : transit.Name,
                Position = position,
                OutputSlots = transit.OutputSlots,
                Priority = prefer ? 0 : 1,
                IsDeliveryVehicle = true,
            };
            return true;
        }
        catch (Exception e)
        {
            Log.Debug("TryBuildDeliveryDock failed: " + e.Message);
            return false;
        }
    }

    internal static void CollectForProperty(Property property, bool allowedOnly, List<StorageRef> result)
    {
        result.Clear();
        if (property == null) return;

        var seen = new HashSet<IntPtr>();
        try
        {
            var items = property.BuildableItems;
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    BuildableItem item = items[i];
                    if (item == null) continue;
                    var placeable = item.TryCast<PlaceableStorageEntity>();
                    if (placeable == null || placeable.Pointer == IntPtr.Zero) continue;
                    if (!seen.Add(placeable.Pointer)) continue;
                    if (!TryBuild(placeable, out StorageRef storage)) continue;
                    if (allowedOnly && !StoragePermissions.IsAllowed(storage.Guid)) continue;
                    result.Add(storage);
                }
            }
        }
        catch (Exception e)
        {
            Log.Debug("CollectForProperty failed: " + e.Message);
        }

        try
        {
            if (Config.AllowDeliveryVehicleAccess == null || Config.AllowDeliveryVehicleAccess.Value)
            {
                var manager = NetworkSingleton<DeliveryManager>.Instance;
                var deliveries = manager == null ? null : manager.Deliveries;
                if (deliveries != null)
                {
                    for (int i = 0; i < deliveries.Count; i++)
                    {
                        DeliveryInstance delivery = deliveries[i];
                        if (delivery == null) continue;
                        Property destination = delivery.Destination;
                        if (destination == null || destination.Pointer != property.Pointer) continue;
                        if (delivery.Status != EDeliveryStatus.Arrived) continue;

                        LoadingDock dock = delivery.LoadingDock;
                        if (dock == null || dock.Pointer == IntPtr.Zero) continue;
                        if (!seen.Add(dock.Pointer)) continue;
                        if (!TryBuildDeliveryDock(dock, out StorageRef storage)) continue;
                        if (storage.OutputSlots == null || storage.OutputSlots.Count == 0) continue;
                        result.Add(storage);
                        Log.Debug("delivery source: " + storage.Name + ", slots=" + storage.OutputSlots.Count);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Log.Debug("CollectForProperty delivery failed: " + e.Message);
        }
    }

    internal static bool FindBestForBotanist(Botanist botanist, string[] itemIds, List<StorageRef> allStorages, out StorageRef chosenStorage, out ItemSlot chosenSlot)
    {
        chosenStorage = null;
        chosenSlot = null;
        if (botanist == null || itemIds == null || itemIds.Length == 0) return false;

        CleanupReservations();
        Log.Debug("botanist supply search: " + itemIds.Length + " id(s), " + allStorages.Count + " storage(s).");

        if (Config.PreferVanillaSupply == null || Config.PreferVanillaSupply.Value)
        {
            try
            {
                var config = botanist.configuration;
                var selected = config == null || config.Supplies == null ? null : config.Supplies.SelectedObject;
                var preferred = selected == null ? null : selected.TryCast<PlaceableStorageEntity>();
                if (preferred != null && TryBuild(preferred, out StorageRef preferredRef)
                    && StoragePermissions.IsAllowed(preferredRef.Guid)
                    && TryFindSlotIn(preferredRef, itemIds, out ItemSlot preferredSlot))
                {
                    bool reachable;
                    try { reachable = botanist.Movement.CanGetTo(preferredRef.Transit, 1f); }
                    catch { reachable = true; }
                    if (reachable)
                    {
                        chosenStorage = preferredRef;
                        chosenSlot = preferredSlot;
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Log.Debug("vanilla supply check failed: " + e.Message);
            }
        }

        StorageRef best = null;
        ItemSlot bestSlot = null;
        float bestDistance = float.MaxValue;
        int bestPriority = int.MaxValue;

        for (int i = 0; i < allStorages.Count; i++)
        {
            StorageRef storage = allStorages[i];
            if (!storage.IsValid) continue;
            if (!TryFindSlotIn(storage, itemIds, out ItemSlot slot)) continue;

            bool reachable;
            try { reachable = botanist.Movement.CanGetTo(storage.Transit, 1f); }
            catch { reachable = true; }
            if (!reachable) continue;

            float distance = (botanist.transform.position - storage.Position).sqrMagnitude;
            int priority = storage.Priority;
            if (best == null || priority < bestPriority || (priority == bestPriority && distance < bestDistance))
            {
                best = storage;
                bestSlot = slot;
                bestDistance = distance;
                bestPriority = priority;
            }
        }

        if (best == null) return false;
        chosenStorage = best;
        chosenSlot = bestSlot;
        return true;
    }

    internal static List<FetchCandidate> FindCandidates(Employee employee, List<TargetRef> targets, bool requireReachable)
    {
        var result = new List<FetchCandidate>();
        if (employee == null || targets == null || targets.Count == 0) return result;

        Property property;
        try { property = employee.AssignedProperty; }
        catch { return result; }
        if (property == null) return result;

        var storages = new List<StorageRef>();
        CollectForProperty(property, true, storages);
        if (storages.Count == 0) return result;

        CleanupReservations();

        for (int t = 0; t < targets.Count; t++)
        {
            TargetRef target = targets[t];
            if (target == null || target.Entity == null || target.Slot == null) continue;

            try
            {
                if (target.Slot.IsLocked || target.Slot.IsAddLocked) continue;
            }
            catch { continue; }

            for (int s = 0; s < storages.Count; s++)
            {
                StorageRef storage = storages[s];
                if (!storage.IsValid || storage.OutputSlots == null) continue;

                for (int i = 0; i < storage.OutputSlots.Count; i++)
                {
                    ItemSlot sourceSlot = storage.OutputSlots[i];
                    if (!UsableSource(sourceSlot)) continue;

                    ItemInstance item = sourceSlot.ItemInstance;
                    if (item == null || item.Definition == null) continue;

                    if (target.ItemFilter != null)
                    {
                        bool accepted;
                        try { accepted = target.ItemFilter(item); }
                        catch { accepted = false; }
                        if (!accepted) continue;
                    }

                    string itemId;
                    try { itemId = item.Definition.ID; }
                    catch { continue; }
                    if (string.IsNullOrWhiteSpace(itemId)) continue;
                    if (target.AllowedItemIds != null && !target.AllowedItemIds.Contains(itemId)) continue;

                    int capacity;
                    bool targetEmpty;
                    try
                    {
                        capacity = target.Slot.GetCapacityForItem(item, true);
                        targetEmpty = target.Slot.ItemInstance == null;
                    }
                    catch { continue; }

                    if (capacity <= 0) continue;

                    float distance = (employee.transform.position - storage.Position).sqrMagnitude;
                    if (requireReachable)
                    {
                        bool reachable;
                        try { reachable = employee.Movement.CanGetTo(storage.Transit, 1f); }
                        catch { reachable = true; }
                        if (!reachable) continue;
                    }

                    result.Add(new FetchCandidate
                    {
                        Source = storage,
                        SourceSlot = sourceSlot,
                        Item = item,
                        Target = target,
                        Capacity = capacity,
                        Distance = distance,
                        TargetEmpty = targetEmpty,
                    });
                }
            }
        }

        result.Sort((a, b) =>
        {
            int byEmpty = (a.TargetEmpty ? 0 : 1).CompareTo(b.TargetEmpty ? 0 : 1);
            if (byEmpty != 0) return byEmpty;
            int byPriority = a.Source.Priority.CompareTo(b.Source.Priority);
            if (byPriority != 0) return byPriority;
            int byDistance = a.Distance.CompareTo(b.Distance);
            if (byDistance != 0) return byDistance;
            int byCapacity = b.Capacity.CompareTo(a.Capacity);
            if (byCapacity != 0) return byCapacity;
            return string.CompareOrdinal(a.Source.Guid, b.Source.Guid);
        });

        return result;
    }

    internal static void Reserve(ItemSlot slot)
    {
        if (slot == null) return;
        Reserved[slot.Pointer] = Time.realtimeSinceStartup + 5f;
    }

    internal static void ClearReservations()
    {
        Reserved.Clear();
    }

    private static bool UsableSource(ItemSlot slot)
    {
        if (slot == null) return false;
        try
        {
            if (slot.ItemInstance == null || slot.ItemInstance.Definition == null) return false;
            if (slot.IsLocked || slot.IsRemovalLocked) return false;
            return !IsReserved(slot);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsReserved(ItemSlot slot)
    {
        if (slot == null) return true;
        float now = Time.realtimeSinceStartup;
        if (!Reserved.TryGetValue(slot.Pointer, out float until)) return false;
        if (now < until) return true;
        Reserved.Remove(slot.Pointer);
        return false;
    }

    private static void CleanupReservations()
    {
        if (Reserved.Count == 0) return;
        float now = Time.realtimeSinceStartup;
        var gone = new List<IntPtr>();
        foreach (KeyValuePair<IntPtr, float> pair in Reserved)
            if (now >= pair.Value) gone.Add(pair.Key);
        for (int i = 0; i < gone.Count; i++) Reserved.Remove(gone[i]);
    }

    private static bool TryFindSlotIn(StorageRef storage, string[] itemIds, out ItemSlot slot)
    {
        slot = null;
        if (storage == null || storage.OutputSlots == null) return false;

        for (int i = 0; i < storage.OutputSlots.Count; i++)
        {
            ItemSlot candidate = storage.OutputSlots[i];
            if (!UsableSource(candidate)) continue;

            ItemInstance item = candidate.ItemInstance;
            if (item == null || item.Definition == null) continue;

            string id;
            try { id = item.Definition.ID; }
            catch { continue; }
            if (string.IsNullOrWhiteSpace(id)) continue;

            for (int j = 0; j < itemIds.Length; j++)
            {
                if (string.IsNullOrWhiteSpace(itemIds[j])) continue;
                if (!string.Equals(id, itemIds[j], StringComparison.OrdinalIgnoreCase)) continue;
                slot = candidate;
                return true;
            }
        }

        return false;
    }
}
