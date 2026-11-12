using System;
using System.Collections.Generic;
using Il2CppScheduleOne.EntityFramework;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.ObjectScripts;
using Il2CppScheduleOne.Property;
using Il2CppScheduleOne.StationFramework;

namespace SmartStorage;

internal static class ChemistryFilters
{
    private static float _nextScan;

    internal static void Reset()
    {
        _nextScan = 0f;
    }

    internal static void Tick()
    {
        if (!Enabled()) return;
        if (!Net.IsServer) return;

        float now = UnityEngine.Time.realtimeSinceStartup;
        if (now < _nextScan) return;

        float interval = Config.ChemistryFilterRefreshSeconds == null ? 1f : Config.ChemistryFilterRefreshSeconds.Value;
        if (interval < 0.25f) interval = 0.25f;
        _nextScan = now + interval;

        try
        {
            var seen = new HashSet<IntPtr>();
            ScanProperties(seen);
            ScanBusinesses(seen);
        }
        catch (Exception e)
        {
            Log.Debug("chemistry filter scan failed: " + e.Message);
        }
    }

    internal static void SetRecipePostfix(StationRecipeField __instance, StationRecipe recipe)
    {
        if (!Enabled() || !Net.IsServer) return;
        try
        {
            var config = __instance == null || __instance.ParentConfig == null
                ? null
                : __instance.ParentConfig.TryCast<ChemistryStationConfiguration>();
            ChemistryStation station = config == null ? null : config.Station;
            if (station != null) ApplyIfNeeded(station, recipe);
        }
        catch (Exception e)
        {
            Log.Debug("chemistry recipe postfix failed: " + e.Message);
        }
    }

    internal static void ApplyIfNeeded(ChemistryStation station, StationRecipe recipe = null)
    {
        if (station == null || !Enabled() || !Net.IsServer) return;
        if (!StoragePermissions.IsLoaded) return;

        string guid = StationGuid(station);
        if (string.IsNullOrWhiteSpace(guid)) return;

        recipe = recipe ?? GetRecipe(station);
        string currentId = RecipeId(recipe);
        string appliedId = StoragePermissions.GetAppliedChemistryRecipe(guid);

        if (currentId == null)
        {
            if (appliedId != null)
            {
                ClearFilters(station);
                StoragePermissions.RemoveAppliedChemistryRecipe(guid);
                Log.Debug("chemistry station " + SafeName(station) + ": recipe cleared, filters reset.");
            }
            return;
        }

        // The player may have removed one of the filters by hand. The stored recipe id
        // still matches, so leave the slots alone until the recipe changes.
        if (appliedId == currentId) return;

        if (!ApplyFilters(station, recipe)) return;

        StoragePermissions.SetAppliedChemistryRecipe(guid, currentId);
        Log.Debug("chemistry station " + SafeName(station) + ": recipe " + currentId + " applied to ingredient slots.");
    }

    internal static StationRecipe GetRecipe(ChemistryStation station)
    {
        if (station == null) return null;
        try
        {
            var config = station.Configuration == null ? null : station.Configuration.TryCast<ChemistryStationConfiguration>();
            return config == null || config.Recipe == null ? null : config.Recipe.SelectedRecipe;
        }
        catch
        {
            return null;
        }
    }

    internal static string RecipeId(StationRecipe recipe)
    {
        if (recipe == null) return null;
        try
        {
            string id = recipe.RecipeID;
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }
        catch { }

        try
        {
            string name = recipe.name;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }

    internal static HashSet<string> GroupIds(StationRecipe.IngredientQuantity group)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (group == null) return result;

        try
        {
            var items = group.Items;
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                    AddDefinition(result, items[i]);
            }
        }
        catch { }

        try { AddDefinition(result, group.Item); }
        catch { }

        return result;
    }

    private static void ApplyIfNeededFromScan(ChemistryStation station, HashSet<IntPtr> seen)
    {
        if (station == null || !seen.Add(station.Pointer)) return;
        ApplyIfNeeded(station);
    }

    private static void ScanProperties(HashSet<IntPtr> seen)
    {
        var properties = Property.OwnedProperties;
        if (properties == null) return;

        for (int p = 0; p < properties.Count; p++)
        {
            Property property = properties[p];
            if (property == null) continue;
            ScanBuildables(property.BuildableItems, seen);
        }
    }

    private static void ScanBusinesses(HashSet<IntPtr> seen)
    {
        var businesses = Business.Businesses;
        if (businesses == null) return;

        for (int b = 0; b < businesses.Count; b++)
        {
            Business business = businesses[b];
            if (business == null) continue;
            ScanBuildables(business.BuildableItems, seen);
        }
    }

    private static void ScanBuildables(Il2CppSystem.Collections.Generic.List<BuildableItem> items, HashSet<IntPtr> seen)
    {
        if (items == null) return;
        for (int i = 0; i < items.Count; i++)
        {
            BuildableItem item = items[i];
            if (item == null) continue;
            ChemistryStation station = item.TryCast<ChemistryStation>();
            if (station == null) continue;
            ApplyIfNeededFromScan(station, seen);
        }
    }

    private static bool ApplyFilters(ChemistryStation station, StationRecipe recipe)
    {
        try
        {
            var slots = station.IngredientSlots;
            if (slots == null || slots.Length == 0) return false;

            var ingredients = recipe == null ? null : recipe.Ingredients;
            int groups = ingredients == null ? 0 : ingredients.Count;
            int count = Math.Min(slots.Length, 3);

            for (int i = 0; i < count; i++)
            {
                HashSet<string> ids = i < groups
                    ? GroupIds(ingredients[i])
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                SlotFilter filter = SlotFilters.BuildWhitelist(ids);
                slots[i].SetPlayerFilter(filter, false);
            }

            if (groups > count)
                Log.Debug("chemistry station " + SafeName(station) + ": recipe has " + groups + " ingredient groups, the station only has " + count + " slots.");

            return true;
        }
        catch (Exception e)
        {
            Log.Debug("chemistry filter apply failed: " + e.Message);
            return false;
        }
    }

    private static void ClearFilters(ChemistryStation station)
    {
        try
        {
            var slots = station.IngredientSlots;
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                slots[i].SetPlayerFilter(SlotFilters.BuildNone(), false);
            }
        }
        catch (Exception e)
        {
            Log.Debug("chemistry filter clear failed: " + e.Message);
        }
    }

    private static void AddDefinition(HashSet<string> ids, ItemDefinition definition)
    {
        if (definition == null) return;
        try
        {
            string id = definition.ID;
            if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
        }
        catch { }
    }

    private static string StationGuid(ChemistryStation station)
    {
        try
        {
            if (station == null || station.Pointer == IntPtr.Zero) return null;
            var transit = new ITransitEntity(station.Pointer);
            string guid = transit.GUID.ToString();
            return string.IsNullOrWhiteSpace(guid) ? null : guid;
        }
        catch
        {
            return null;
        }
    }

    private static string SafeName(ChemistryStation station)
    {
        try { return station == null ? "?" : (station.Name ?? "chemistry station"); }
        catch { return "chemistry station"; }
    }

    private static bool Enabled()
    {
        return Config.Enabled != null && Config.Enabled.Value
            && Config.AutoFilterChemistrySlots != null && Config.AutoFilterChemistrySlots.Value;
    }
}
