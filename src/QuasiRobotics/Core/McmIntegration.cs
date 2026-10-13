using System;
using System.Collections.Generic;
using HarmonyLib;
using ModConfigMenu;
using ModConfigMenu.Contracts;
using ModConfigMenu.Objects;

namespace QuasiRobotics;

// Mod Configuration Menu (Crynano_ModConfigMenu, a manifest dependency).
// MCM shows the values; QuasiRobotics keeps them in its own config.json.
public static class McmIntegration
{
    public const string StartWithPrintyardKey = "StartWithPrintyard";
    public const string StartWithSalvageDronesKey = "StartWithSalvageDrones";
    public const string StartWithDropPodsKey = "StartWithDropPods";
    public const string DropPodsKeyKey = "DropPodsKey";
    public const string RobotSlotsKey = "RobotSlotsPerUpgrade";
    public const string DisableFriendlyFireKey = "DisableFriendlyFire";
    public const string AllowRobotVestAndBackpackKey = "AllowRobotVestAndBackpack";
    public const string UninstallModeKey = "UninstallMode";

    public static void Register()
    {
        var values = new List<IConfigValue>
        {
            new ConfigValue(StartWithPrintyardKey, Plugin.Settings.StartWithPrintyard, ModText.Get("quasirobotics.mcm.section.robotics"), false,
                ModText.Get("quasirobotics.mcm.start_printyard.tooltip"), ModText.Get("quasirobotics.mcm.start_printyard")),
            new ConfigValue(StartWithSalvageDronesKey, Plugin.Settings.StartWithSalvageDrones, ModText.Get("quasirobotics.mcm.section.robotics"), false,
                ModText.Get("quasirobotics.mcm.start_salvage_drones.tooltip"), ModText.Get("quasirobotics.mcm.start_salvage_drones")),
            new ConfigValue(StartWithDropPodsKey, Plugin.Settings.StartWithDropPods, ModText.Get("quasirobotics.mcm.section.robotics"), false,
                ModText.Get("quasirobotics.mcm.start_drop_pods.tooltip"), ModText.Get("quasirobotics.mcm.start_drop_pods")),
            // Replaced on the page by the game's own key row (DropPodsKeybind).
            new ConfigValue(DropPodsKeyKey, DropPodsKeybind.McmPlaceholder, ModText.Get("quasirobotics.mcm.section.robotics")),
            new ConfigValue(RobotSlotsKey, Plugin.Settings.RobotSlotsPerUpgrade, ModText.Get("quasirobotics.mcm.section.robotics"), 2,
                ModText.Get("quasirobotics.mcm.robot_slots.tooltip"),
                ModText.Get("quasirobotics.mcm.robot_slots"), 1f, 5f),
            new ConfigValue(AllowRobotVestAndBackpackKey, Plugin.Settings.AllowRobotVestAndBackpack, ModText.Get("quasirobotics.mcm.section.robotics"), false,
                ModText.Get("quasirobotics.mcm.allow_vest_backpack.tooltip"),
                ModText.Get("quasirobotics.mcm.allow_vest_backpack")),
            new ConfigValue(DisableFriendlyFireKey, Plugin.Settings.DisableFriendlyFire, ModText.Get("quasirobotics.mcm.section.combat"), true,
                ModText.Get("quasirobotics.mcm.disable_friendly_fire.tooltip"),
                ModText.Get("quasirobotics.mcm.disable_friendly_fire")),
            new ConfigValue(UninstallModeKey, Plugin.Settings.UninstallMode, ModText.Get("quasirobotics.mcm.section.uninstall"), false,
                ModText.Get("quasirobotics.mcm.uninstall_mode.tooltip"),
                ModText.Get("quasirobotics.mcm.uninstall_mode")),
        };
        ModConfigMenuAPI.RegisterModConfig("QuasiRobotics", values, OnSaved);
        DropPodsKeybind.PatchMcm(new Harmony(Plugin.HarmonyId + ".mcm"));
    }

    private static bool OnSaved(Dictionary<string, object> config, out string feedback)
    {
        feedback = string.Empty;
        try
        {
            if (config.TryGetValue(StartWithPrintyardKey, out var value))
                Plugin.Settings.StartWithPrintyard = Convert.ToBoolean(value);
            if (config.TryGetValue(StartWithSalvageDronesKey, out value))
                Plugin.Settings.StartWithSalvageDrones = Convert.ToBoolean(value);
            if (config.TryGetValue(StartWithDropPodsKey, out value))
                Plugin.Settings.StartWithDropPods = Convert.ToBoolean(value);
            if (config.TryGetValue(RobotSlotsKey, out value))
            {
                RobotUpgrades.SlotsPerSet = (int)Math.Round(Convert.ToDouble(value));
                Plugin.Settings.RobotSlotsPerUpgrade = RobotUpgrades.SlotsPerSet;
            }
            if (config.TryGetValue(AllowRobotVestAndBackpackKey, out value))
                Plugin.Settings.AllowRobotVestAndBackpack = Convert.ToBoolean(value);
            if (config.TryGetValue(DisableFriendlyFireKey, out value))
                Plugin.Settings.DisableFriendlyFire = Convert.ToBoolean(value);
            if (config.TryGetValue(UninstallModeKey, out value))
                Plugin.Settings.UninstallMode = Convert.ToBoolean(value);
            Plugin.Settings.Save(Plugin.ConfigDirectories.ConfigPath);
            return true;
        }
        catch (Exception ex)
        {
            feedback = "QuasiRobotics could not save its settings: " + ex.Message;
            return false;
        }
    }
}
