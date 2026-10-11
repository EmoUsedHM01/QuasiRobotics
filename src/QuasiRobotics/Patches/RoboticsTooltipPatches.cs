using System.Collections.Generic;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics.Patches;

// Robotics tooltips in the vanilla layout: description under the captions,
// then Status, then one row per bonus, then the price. Native tooltips build
// those rows from MagnumParameter modifiers, which Robotics perks do not have.
[HarmonyPatch(typeof(TooltipFactory), nameof(TooltipFactory.BuildMagnumPerkTooltip))]
public static class RoboticsTooltipPatch
{
    private static MagnumPerkRecord current;
    private static bool written;

    [HarmonyPrefix]
    public static void Prefix(MagnumPerkRecord record)
    {
        current = RoboticsTooltip.IsRobotics(record?.Id) ? record : null;
        written = false;
    }
    // Status was not shown: the description and rows still appear, at the end.
    [HarmonyPostfix]
    public static void Postfix(TooltipFactory __instance)
    {
        if (current != null && !written) { WriteDescription(__instance); WriteRows(__instance); }
        current = null;
    }
    public static void WriteDescription(TooltipFactory factory)
    {
        var text = RoboticsTooltip.Description(current.Id);
        if (!string.IsNullOrEmpty(text)) factory.AddPanelToTooltip().SetMultilineName(text).SetNameColor(Colors.AltGreen);
    }
    public static void WriteRows(TooltipFactory factory)
    {
        written = true;
        foreach (var row in RoboticsTooltip.Rows(current.Id, RobotUpgrades.Current))
            factory.AddPanelToTooltip().SetIcon("common_datadisk").SetName(row.Key).SetValue(row.Value);
    }
    public static bool Pending => current != null && !written;
}
// Vanilla order: the description sits above Status, the bonus rows below it.
[HarmonyPatch(typeof(TooltipFactory), "AddMagnumPerkStatus")]
public static class RoboticsTooltipStatusPatch
{
    [HarmonyPrefix]
    public static void Prefix(TooltipFactory __instance) { if (RoboticsTooltipPatch.Pending) RoboticsTooltipPatch.WriteDescription(__instance); }
    [HarmonyPostfix]
    public static void Postfix(TooltipFactory __instance) { if (RoboticsTooltipPatch.Pending) RoboticsTooltipPatch.WriteRows(__instance); }
}

public static class RoboticsTooltip
{
    public static bool IsRobotics(string id) => id == RoboticsModule.PrintyardId || id == RoboticsModule.SalvageDronesId
        || id == RoboticsModule.DropPodsId || RobotUpgrades.Find(id) != null;

    public static string Description(string id) =>
        id == RoboticsModule.PrintyardId ? ModText.Get("quasirobotics.desc.printyard")
        : id == RoboticsModule.SalvageDronesId ? ModText.Get("quasirobotics.desc.salvage_drones")
        : id == RoboticsModule.DropPodsId ? ModText.Get("quasirobotics.desc.drop_pods")
        : id == RobotUpgrades.RapidResponseUnit ? ModText.Get("quasirobotics.desc.robo_rapid_response_unit")
        : id == RobotUpgrades.FoamCanisters ? ModText.Get("quasirobotics.desc.robo_foam_canisters")
        : id == RobotUpgrades.ElectricFence ? ModText.Get("quasirobotics.desc.robo_electric_fence")
        : id == RobotUpgrades.FlatPackFurniture ? ModText.Get("quasirobotics.desc.robo_flat_pack_furniture")
        : null;

    // Label and value, as vanilla prints parameter rows. A device lists the
    // bonuses its upgrades give now (like the Scanner); an upgrade lists its own.
    public static List<KeyValuePair<string, string>> Rows(string id, RobotUpgrade owned) =>
        Rows(id, owned, RobotUpgrades.SlotsPerSet, RobotUpgrades.Owns(RobotUpgrades.FlatPackFurniture));
    public static List<KeyValuePair<string, string>> Rows(string id, RobotUpgrade owned, int slotsPerSet, bool flatPack = false)
    {
        var bonus = id == RoboticsModule.PrintyardId ? owned : RobotUpgrades.Find(id);
        var rows = new List<KeyValuePair<string, string>>();
        if (id == RoboticsModule.DropPodsId)
        {
            rows.Add(Row(ModText.Get("quasirobotics.tooltip.charges"), DropPods.Charges.ToString()));
            rows.Add(Row(ModText.Get("quasirobotics.tooltip.recharge_time"), ModText.Format("quasirobotics.tooltip.hours", DropPods.Reload(flatPack))));
            return rows;
        }
        if (bonus == null) return rows;
        // Printyard always lists the robot slots it gives with the bays owned.
        if (id == RoboticsModule.PrintyardId) rows.Add(Row(ModText.Get("quasirobotics.tooltip.robot_slots"), RobotUpgrades.Slots(owned, slotsPerSet).ToString()));
        else if (bonus.SlotSteps != 0) rows.Add(Row(ModText.Get("quasirobotics.tooltip.robot_slots"), "+" + bonus.SlotSteps * slotsPerSet));
        if (id == RobotUpgrades.ExpandedBays(5)) rows.Add(Row(ModText.Get("quasirobotics.tooltip.holy_shit"), ""));
        if (bonus.Health != 0) rows.Add(Row(ModText.Get("quasirobotics.tooltip.robot_health"), "+" + bonus.Health));
        if (bonus.Resists != 0) rows.Add(Row(ModText.Get("quasirobotics.tooltip.robot_resistances"), "+" + bonus.Resists));
        if (bonus.Accuracy != 0) rows.Add(Row(ModText.Get("quasirobotics.tooltip.robot_accuracy"), "+" + bonus.Accuracy + "%"));
        if (bonus.ActionPoints != 0) rows.Add(Row(ModText.Get("quasirobotics.tooltip.robot_action_points"), "+" + bonus.ActionPoints));
        if (bonus.HealthRegen != 0) rows.Add(Row(ModText.Get("quasirobotics.tooltip.robot_health_regen"), "+" + bonus.HealthRegen));
        if (id == RobotUpgrades.RapidResponseUnit) rows.Add(Row(ModText.Get("quasirobotics.tooltip.robot_repair_speed"), "x2"));
        if (id == RobotUpgrades.ElectricFence) rows.Add(Row(ModText.Get("quasirobotics.tooltip.melee_counter_damage"), ModText.Format("quasirobotics.tooltip.shock_damage", ElectricFencePatch.MinDamage, ElectricFencePatch.MaxDamage)));
        if (id == RobotUpgrades.FlatPackFurniture) rows.Add(Row(ModText.Get("quasirobotics.tooltip.drop_pods_recharge_time"), "-" + ModText.Format("quasirobotics.tooltip.hours", DropPods.ReloadHours - DropPods.FlatPackReloadHours)));
        return rows;
    }
    private static KeyValuePair<string, string> Row(string label, string value) => new KeyValuePair<string, string>(label, value);
}
