using System;
using MelonLoader;
using UnityEngine;
using Il2CppScheduleOne.UI;

[assembly: MelonInfo(typeof(SmartStorage.Main), "The Employees Know", "0.2.0", "EmployeeOfTheMonth")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace SmartStorage;

public class Main : MelonMod
{
    public override void OnInitializeMelon()
    {
        Log.Logger = LoggerInstance;
        Config.Init();
        L10n.Init();

        StoragePermissions.SaveChanged += ResetCaches;
        Patcher.Apply(HarmonyInstance);

        Log.Msg("[The Employees Know] Ready. Employee storage access is " + ((Config.Enabled != null && Config.Enabled.Value) ? "on" : "off") + ".");
    }

    public override void OnUpdate()
    {
        try { StoragePermissions.SyncToCurrentSave(); }
        catch (Exception e) { Log.Debug("save sync failed: " + e.Message); }

        try { ChemistryFilters.Tick(); }
        catch (Exception e) { Log.Debug("chemistry filter tick failed: " + e.Message); }

        try { BotanistScheduler.Tick(); }
        catch (Exception e) { Log.Debug("botanist scheduler tick failed: " + e.Message); }

        try
        {
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F8))
                DumpDiagnostics();
        }
        catch { }
    }

    public override void OnGUI()
    {
        if (StorageToggleUI.NativeOk) return;
        if (Config.Enabled == null || !Config.Enabled.Value) return;

        try
        {
            StorageMenu menu = StorageMenu.Instance;
            if (menu == null || !menu.IsOpen) return;

            StorageRef current = StorageToggleUI.Current;
            if (current == null) return;

            bool allowed = StoragePermissions.IsAllowed(current.Guid);
            const float x = 30f;
            const float y = 120f;
            const float w = 380f;
            const float h = 28f;

            GUI.Label(new UnityEngine.Rect(x, y, w, 42f), L10n.Get("StorageHint"));
            bool next = GUI.Toggle(new UnityEngine.Rect(x, y + 42f, w, h), allowed, L10n.Get("StorageAccessLabel"));
            if (next != allowed)
            {
                StoragePermissions.SetAllowed(current.Guid, next);
                Log.Msg("[The Employees Know] " + (current.Name ?? "storage") + ": " + (next ? "employees allowed" : "employees blocked") + ".");
            }
        }
        catch { }
    }

    public override void OnApplicationQuit()
    {
        try { StoragePermissions.Flush(); } catch { }
        ResetCaches();
    }

    private static void ResetCaches()
    {
        FetchService.Reset();
        BotanistSupply.Reset();
        ChemistFetch.Reset();
        PackagerFetch.Reset();
        ChainMover.Reset();
        MixerChain.Reset();
        ChemistryFilters.Reset();
        CookableItems.Reset();
        BotanistScheduler.Reset();
        BotanistDryableMover.Reset();
        StorageIndex.ClearReservations();
    }

    private static void DumpDiagnostics()
    {
        Log.Msg("[The Employees Know] debug: permissions=" + StoragePermissions.Count + " host=" + Net.IsServer);
        try
        {
            var properties = Il2CppScheduleOne.Property.Property.Properties;
            if (properties == null) return;
            for (int p = 0; p < properties.Count; p++)
            {
                var property = properties[p];
                if (property == null) continue;
                var storages = new System.Collections.Generic.List<StorageRef>();
                StorageIndex.CollectForProperty(property, false, storages);
                int allowed = 0;
                for (int i = 0; i < storages.Count; i++)
                    if (StoragePermissions.IsAllowed(storages[i].Guid)) allowed++;
                Log.Msg("[The Employees Know] " + property.PropertyName + ": " + allowed + "/" + storages.Count + " storage(s) allowed.");
            }
        }
        catch (Exception e)
        {
            Log.Debug("diagnostics failed: " + e.Message);
        }
    }
}
