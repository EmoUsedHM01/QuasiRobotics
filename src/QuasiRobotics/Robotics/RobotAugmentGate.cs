using System;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics;

// With the MCM option on, robots only take Robotic augments (the game's
// "CyberneticAug" item class, shown as Bionic arm/leg/... in tooltips).
// Recreational ARs can take any augment. Implants are not affected.
public static class RobotAugmentGate
{
    public const string RecreationalMobId = "cyborg_recreation";

    public static bool Allows(string mobClassId, ItemClass itemClass, bool gateOn) =>
        !gateOn || mobClassId == RecreationalMobId || itemClass == ItemClass.CyberneticAug;

    public static bool Allows(CreatureData robot, BasePickupItem item)
    {
        if (robot == null || item == null || !RobotUpgrades.IsRobot(robot)) return true;
        var record = item.Record<AugmentationRecord>();
        if (record == null) return true;
        return Allows(robot.MobClassId, record.ItemClass, Plugin.Settings?.RobotsBionicAugmentsOnly ?? true);
    }
}

// Grey out "Augment" in the item menu for an augment the robot can't take.
[HarmonyPatch(typeof(CommonContextMenu), nameof(CommonContextMenu.SetupCommand))]
public static class RobotAugmentMenuPatch
{
    [HarmonyPrefix]
    public static void Prefix(int bindedVal, ref bool interactable)
    {
        if (bindedVal != (int)ContextMenuCommand.Augment || !interactable) return;
        try
        {
            foreach (var view in UI.GetActiveViews())
                if (view is ScreenWithShipCargo cargo)
                {
                    var robot = RobotRosterView.Field<Mercenary>(cargo, "_merc")?.CreatureData;
                    var item = RobotRosterView.Field<ItemSlot>(cargo, "_contextMenuItemSlot")?.Item;
                    if (!RobotAugmentGate.Allows(robot, item)) interactable = false;
                }
        }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
}

// Safety net for any other path into the install.
[HarmonyPatch(typeof(AugmentationSystem), nameof(AugmentationSystem.Augment))]
public static class RobotAugmentInstallPatch
{
    [HarmonyPrefix]
    public static bool Prefix(Mercenary mercenary, BasePickupItem item) => RobotAugmentGate.Allows(mercenary?.CreatureData, item);
}
