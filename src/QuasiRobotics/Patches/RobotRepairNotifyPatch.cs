using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace QuasiRobotics.Patches;

// "Repairs complete: <robot>" in the ship's notification panel, like the
// game's "Healing complete" for clones. Runs right after the game's own clone
// check, each time ship time moves on.
[HarmonyPatch(typeof(MercenarySystem), nameof(MercenarySystem.Update), typeof(MagnumProgression), typeof(Mercenaries), typeof(SpaceTime), typeof(PerkFactory))]
public static class RobotRepairNotifyPatch
{
    public const string TextKey = "quasirobotics.notification.repair_complete";
    // Ship time at the last check. Unset after a load, so repairs that ended
    // before the save was loaded do not all pop up at once.
    private static DateTime? lastCheck;

    [HarmonyPostfix]
    public static void Postfix(Mercenaries mercenaries, SpaceTime spaceTime)
    {
        try
        {
            var now = spaceTime.Time;
            var since = lastCheck;
            lastCheck = now;
            if (since == null || now < since.Value) return;
            var panel = UI.Staff?.NotificationPanel;
            if (panel == null) return;
            var color = (Color)AccessTools.Field(typeof(NotificationPanel), "_highlightColor").GetValue(panel);
            foreach (var robot in FinishedBetween(RobotRoster.For(mercenaries), since.Value, now))
                panel.AddNotification(ModText.Format(TextKey, robot.DisplayName.WrapInColor(color)));
        }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }

    // Robots whose repair ended after `since` and by `now`.
    public static List<RobotRecord> FinishedBetween(RobotRoster roster, DateTime since, DateTime now) =>
        roster.Robots.Where(r => r.RepairEnd > since && r.RepairEnd <= now).ToList();

    public static void Reset() => lastCheck = null;
}

// A loaded save starts a fresh check.
[HarmonyPatch(typeof(SaveManager), "LoadGame")]
public static class RobotRepairNotifyLoadPatch
{
    [HarmonyPrefix] public static void Prefix() => RobotRepairNotifyPatch.Reset();
}
