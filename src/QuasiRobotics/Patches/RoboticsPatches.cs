using System;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics.Patches;

[HarmonyPatch(typeof(SpaceshipScreen), "InitModulePoints")]
public static class RoboticsRoomPatch
{
    [HarmonyPrefix]
    public static void Prefix(SpaceshipScreen __instance)
    {
        try { RoboticsModule.EnsureRoom(RobotRosterView.Field<Spaceship>(__instance, "_spaceship")); }
        catch (Exception ex)
        {
            Plugin.Logger.LogError("Robotics room could not be added to the ship view.");
            Plugin.Logger.LogException(ex);
        }
    }
}

// Optionally have Printyard installed (Mod Config Menu): new campaigns, and
// loaded campaigns that do not own it yet, so the mod can join mid-campaign.
// The Robotics room itself needs nothing: native OnAfterLoad adds any missing
// department, and the ship anchor and camera are added at runtime.
[HarmonyPatch(typeof(MagnumProgression), nameof(MagnumProgression.InitDepartments))]
public static class StartWithPrintyardPatch
{
    [HarmonyPostfix]
    public static void Postfix(MagnumProgression __instance) => RoboticsModule.GrantStartingDevices(__instance);
}
[HarmonyPatch(typeof(MagnumProgression), nameof(MagnumProgression.OnAfterLoad))]
public static class LoadedPrintyardPatch
{
    [HarmonyPostfix]
    public static void Postfix(MagnumProgression __instance)
    {
        foreach (var id in RoboticsModule.GrantStartingDevices(__instance))
            Plugin.Logger.Log("Start with " + id + ": installed it in a loaded campaign.");
    }
}

[HarmonyPatch(typeof(MagnumDepartment), nameof(MagnumDepartment.GetDescription))]
public static class PrintyardDescriptionPatch
{
    [HarmonyPostfix]
    public static void Postfix(MagnumDepartment __instance, ref string __result)
    {
        if (!string.IsNullOrEmpty(__result)) return;
        if (__instance.Record?.Id == RoboticsModule.PrintyardId) __result = Localization.Get(ModText.PrintyardStatus);
        else if (__instance.Record?.Id == RoboticsModule.SalvageDronesId) __result = Localization.Get(ModText.SalvageDronesStatus);
        else if (__instance.Record?.Id == RoboticsModule.DropPodsId)
        {
            var mode = SingletonMonoBehaviour<SpaceGameMode>.Instance;
            var time = mode?.Get<SpaceTime>() ?? DropPods.DungeonState()?.Get<SpaceTime>();
            var mercs = mode?.Get<Mercenaries>() ?? DropPods.DungeonState()?.Get<Mercenaries>();
            if (time != null && mercs != null) __result = DropPods.Status(RobotRoster.For(mercs), time.Time);
        }
    }
}
