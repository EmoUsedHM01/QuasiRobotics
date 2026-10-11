using System;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics.Patches;

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveGame))]
public static class UninstallSavePatch
{
    [HarmonyPrefix]
    public static void Prefix(SaveManager __instance)
    {
        if (!Uninstaller.Enabled) return;
        try { Uninstaller.Begin(RobotRosterView.Field<State>(__instance, "_state")); }
        catch (Exception ex)
        {
            Plugin.Logger.LogError("Uninstall mode could not clean this save.");
            Plugin.Logger.LogException(ex);
        }
    }
    [HarmonyFinalizer]
    public static void Finalizer() => Uninstaller.End();
}

// Safety net while a cleaned save is written: anything the sweep missed still
// saves as vanilla data (a missed package keeps its stock item, not its gear).
[HarmonyPatch(typeof(SaveToJSON), nameof(SaveToJSON.CreateNode))]
public static class UninstallNodePatch
{
    [HarmonyPrefix]
    public static void Prefix(object obj)
    {
        if (!Uninstaller.Active) return;
        if (obj is PickupItem item && Uninstaller.StripLoadout(item, null))
            Plugin.Logger.LogWarning("Uninstall mode removed a robot loadout from " + item.Id + " outside Ship's Cargo; its gear was not kept.");
        else if (obj is MagnumProgression progression) Uninstaller.RemoveDevices(progression);
    }
}
