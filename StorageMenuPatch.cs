using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.UI;

namespace SmartStorage;

internal static class StorageMenuPatch
{
    internal static void OpenPostfix(StorageMenu __instance, StorageEntity __0)
    {
        if (Config.Enabled == null || !Config.Enabled.Value) return;

        try
        {
            StorageToggleUI.Show(__instance, __0);
        }
        catch (System.Exception e)
        {
            Log.Warning("[The Employees Know] Storage menu toggle failed: " + e.Message);
        }
    }
}
