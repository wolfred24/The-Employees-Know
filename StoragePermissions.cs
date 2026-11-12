using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MelonLoader.Utils;

namespace SmartStorage;

internal static class StoragePermissions
{
    private sealed class SaveData
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, bool> Storages { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, string> ChemistryRecipes { get; set; } = new Dictionary<string, string>();
    }

    private static SaveData _data = new SaveData();
    private static string _loadedFolder;
    private static bool _dirty;
    private static float _saveDue;

    internal static event Action SaveChanged;

    internal static int Count => _data.Storages.Count;
    internal static bool IsLoaded => _loadedFolder != null;

    internal static bool IsAllowed(string guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
            return Config.DefaultStorageAccess == null || Config.DefaultStorageAccess.Value;

        return _data.Storages.TryGetValue(guid, out bool allowed)
            ? allowed
            : (Config.DefaultStorageAccess == null || Config.DefaultStorageAccess.Value);
    }

    internal static void SetAllowed(string guid, bool allowed)
    {
        if (string.IsNullOrWhiteSpace(guid)) return;

        if (_data.Storages.TryGetValue(guid, out bool old) && old == allowed) return;
        _data.Storages[guid] = allowed;
        Touch();
        Log.Debug("storage " + guid + " -> " + (allowed ? "allowed" : "blocked"));
    }

    internal static string GetAppliedChemistryRecipe(string stationGuid)
    {
        if (string.IsNullOrWhiteSpace(stationGuid)) return null;
        return _data.ChemistryRecipes.TryGetValue(stationGuid, out string recipe) ? recipe : null;
    }

    internal static void SetAppliedChemistryRecipe(string stationGuid, string recipeId)
    {
        if (string.IsNullOrWhiteSpace(stationGuid) || string.IsNullOrWhiteSpace(recipeId)) return;
        if (_data.ChemistryRecipes.TryGetValue(stationGuid, out string old) && old == recipeId) return;
        _data.ChemistryRecipes[stationGuid] = recipeId;
        Touch();
    }

    internal static void RemoveAppliedChemistryRecipe(string stationGuid)
    {
        if (string.IsNullOrWhiteSpace(stationGuid)) return;
        if (_data.ChemistryRecipes.Remove(stationGuid)) Touch();
    }

    private static void Touch()
    {
        _dirty = true;
        _saveDue = UnityEngine.Time.realtimeSinceStartup + 1f;
    }

    internal static void SyncToCurrentSave()
    {
        string folder;
        try
        {
            var manager = Il2CppScheduleOne.Persistence.LoadManager.Instance;
            if (manager == null) return;
            folder = manager.LoadedGameFolderPath;
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(folder)) return;
        if (folder == _loadedFolder)
        {
            FlushIfDue();
            return;
        }

        _loadedFolder = folder;
        Load();
    }

    internal static void Reset()
    {
        Flush();
        _data = new SaveData();
        _loadedFolder = null;
        _dirty = false;
        _saveDue = 0f;
    }

    private static string SavePath()
    {
        if (_loadedFolder == null) return null;
        try
        {
            string saveName = new DirectoryInfo(_loadedFolder).Name;
            string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "SmartStorage");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, saveName + ".json");
        }
        catch
        {
            return null;
        }
    }

    private static void Load()
    {
        _data = new SaveData();
        _dirty = false;

        try { SaveChanged?.Invoke(); } catch { }

        string path = SavePath();
        if (path == null) return;

        try
        {
            if (!File.Exists(path))
            {
                Log.Msg("[The Employees Know] No storage permissions saved yet for this save.");
                return;
            }

            SaveData read = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path));
            if (read != null)
                _data = read;
            if (_data.Storages == null)
                _data.Storages = new Dictionary<string, bool>();
            if (_data.ChemistryRecipes == null)
                _data.ChemistryRecipes = new Dictionary<string, string>();

            Log.Msg("[The Employees Know] Loaded " + _data.Storages.Count + " storage permission(s) from " + Path.GetFileName(path) + ".");
        }
        catch (Exception e)
        {
            Log.Error("[The Employees Know] Could not read " + Path.GetFileName(path) + ": " + e.Message + ". Starting empty; the file is left alone.");
        }
    }

    private static void FlushIfDue()
    {
        if (!_dirty) return;
        if (UnityEngine.Time.realtimeSinceStartup < _saveDue) return;
        Flush();
    }

    internal static void Flush()
    {
        if (!_dirty) return;
        _dirty = false;

        string path = SavePath();
        if (path == null) return;

        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true }));
            Log.Debug("wrote " + _data.Storages.Count + " storage permission(s).");
        }
        catch (Exception e)
        {
            Log.Error("[The Employees Know] Could not save storage permissions: " + e.Message);
        }
    }
}
