using System;
using HarmonyLib;
using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.Growing;
using Il2CppScheduleOne.ItemFramework;
using Il2CppScheduleOne.Management;
using Il2CppScheduleOne.StationFramework;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppScheduleOne.NPCs.Behaviour;
using Il2CppScheduleOne.Storage;
using Il2CppScheduleOne.UI;

namespace SmartStorage;

internal static class Patcher
{
    internal static void Apply(HarmonyLib.Harmony harmony)
    {
        Patch(harmony, typeof(StorageMenu), "Open", new[] { typeof(StorageEntity), typeof(Il2CppSystem.Action) },
            null, new HarmonyMethod(typeof(StorageMenuPatch), nameof(StorageMenuPatch.OpenPostfix)));

        Patch(harmony, typeof(StationRecipeField), nameof(StationRecipeField.SetRecipe), new[] { typeof(StationRecipe), typeof(bool) },
            null, new HarmonyMethod(typeof(ChemistryFilters), nameof(ChemistryFilters.SetRecipePostfix)));

        Patch(harmony, typeof(Botanist), nameof(Botanist.GetDryableInSupplies), Type.EmptyTypes,
            new HarmonyMethod(typeof(BotanistSupply), nameof(BotanistSupply.GetDryableInSuppliesPrefix)),
            new HarmonyMethod(typeof(BotanistSupply), nameof(BotanistSupply.GetDryableInSuppliesPostfix)));

        Patch(harmony, typeof(Botanist), nameof(Botanist.GetSuppliesAsTransitEntity), Type.EmptyTypes,
            null, new HarmonyMethod(typeof(BotanistSupply), nameof(BotanistSupply.GetSuppliesAsTransitEntityPostfix)));

        Patch(harmony, typeof(GrowContainerBehaviour), nameof(GrowContainerBehaviour.DoesBotanistHaveAccessToRequiredSupplies), new[] { typeof(GrowContainer) },
            new HarmonyMethod(typeof(BotanistSupply), nameof(BotanistSupply.DoesBotanistHaveAccessPrefix)), null);

        Patch(harmony, typeof(GrowContainerBehaviour), nameof(GrowContainerBehaviour.DoSuppliesContainRequiredItem), new[] { typeof(GrowContainer) },
            new HarmonyMethod(typeof(BotanistSupply), nameof(BotanistSupply.DoSuppliesContainPrefix)), null);

        Patch(harmony, typeof(GrowContainerBehaviour), nameof(GrowContainerBehaviour.GetSuppliesSlotContainingRequiredItem), new[] { typeof(Il2CppStringArray) },
            new HarmonyMethod(typeof(BotanistSupply), nameof(BotanistSupply.GetSuppliesSlotPrefix)), null);

        bool botanistHook = Patch(harmony, typeof(Employee), "OnTick", Type.EmptyTypes,
            null, new HarmonyMethod(typeof(BotanistScheduler), nameof(BotanistScheduler.OnEmployeeTick)));
        BotanistScheduler.HookInstalled = botanistHook;

        Patch(harmony, typeof(Chemist), nameof(Chemist.UpdateBehaviour), Type.EmptyTypes,
            null, new HarmonyMethod(typeof(ChemistFetch), nameof(ChemistFetch.UpdateBehaviourPostfix)));

        Patch(harmony, typeof(Packager), nameof(Packager.UpdateBehaviour), Type.EmptyTypes,
            null, new HarmonyMethod(typeof(PackagerFetch), nameof(PackagerFetch.UpdateBehaviourPostfix)));

        Patch(harmony, typeof(Packager), nameof(Packager.GetTransitRouteReady), null,
            null, new HarmonyMethod(typeof(PackagerFetch), nameof(PackagerFetch.GetTransitRouteReadyPostfix)));
    }

    private static bool Patch(HarmonyLib.Harmony harmony, Type type, string name, Type[] args, HarmonyMethod prefix, HarmonyMethod postfix)
    {
        try
        {
            var method = args == null ? AccessTools.Method(type, name) : AccessTools.Method(type, name, args);
            if (method == null)
            {
                Log.Warning("[The Employees Know] Patch target not found: " + type.Name + "." + name + ". The mod will skip this hook.");
                return false;
            }

            harmony.Patch(method, prefix, postfix);
            Log.Msg("[The Employees Know] Patched " + type.Name + "." + name + ".");
            return true;
        }
        catch (Exception e)
        {
            Log.Warning("[The Employees Know] Could not patch " + type.Name + "." + name + ": " + e.Message);
            return false;
        }
    }
}
