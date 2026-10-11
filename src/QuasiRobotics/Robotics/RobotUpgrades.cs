using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics;

// A Robotics mini-node: a stat bonus for every roster robot while it is owned.
public sealed class RobotUpgrade
{
    public string Id, Parent;
    public int X, Y, Health, Resists, Accuracy, ActionPoints, HealthRegen;
    // Expanded Bays: each step adds one more set of robot slots.
    public int SlotSteps;
    // The device the node belongs to in the tree.
    public string Department = RoboticsModule.PrintyardId;
}

// Bonuses ride on the game's own perk parameters, so every native formula
// picks them up: IMaxHealth (max health), IResists (all resists),
// FAccuracyBonus (accuracy, 0.01 per point) and monster action points.
public static class RobotUpgrades
{
    public const string FusionWelds = "robo_fusion_welds";
    public const string RedundancySystems = "robo_redundancy_systems";
    public const string AblativePlating = "robo_ablative_plating";
    public const string ReinforcedAlloys = "robo_reinforced_alloys";
    public const string TargetingSystems = "robo_targeting_systems";
    public const string EnhancedOptics = "robo_enhanced_optics";
    public const string Overclocker = "robo_overclocker";
    public static string ExpandedBays(int tier) => "robo_expanded_bays_" + tier;
    public const string RestorativeNanobots = "robo_restorative_nanobots";
    public const string BolsteredSwarm = "robo_bolstered_swarm";
    // Drop Pods nodes: effects rather than stats (see RobotCombatPatches,
    // DropPods and RobotDeployment.Repair).
    public const string RapidResponseUnit = "robo_rapid_response_unit";
    public const string FoamCanisters = "robo_foam_canisters";
    public const string ElectricFence = "robo_electric_fence";
    public const string FlatPackFurniture = "robo_flat_pack_furniture";

    public static readonly RobotUpgrade[] All =
    {
        new RobotUpgrade { Id = FusionWelds, X = 0, Y = 1, Parent = RoboticsModule.PrintyardId, Health = 10 },
        new RobotUpgrade { Id = RedundancySystems, X = 1, Y = 1, Parent = FusionWelds, Health = 20 },
        new RobotUpgrade { Id = AblativePlating, X = 0, Y = 2, Parent = FusionWelds, Resists = 4 },
        new RobotUpgrade { Id = ReinforcedAlloys, X = 1, Y = 2, Parent = AblativePlating, Resists = 12 },
        new RobotUpgrade { Id = TargetingSystems, X = 0, Y = 3, Parent = AblativePlating, Accuracy = 6 },
        new RobotUpgrade { Id = EnhancedOptics, X = 1, Y = 3, Parent = TargetingSystems, Accuracy = 12 },
        new RobotUpgrade { Id = Overclocker, X = 0, Y = 4, Parent = TargetingSystems, ActionPoints = 1 },
        // Top row, left to right; Expanded Bays II also leads down to Salvage Drones.
        new RobotUpgrade { Id = ExpandedBays(1), X = 1, Y = 0, Parent = RoboticsModule.PrintyardId, SlotSteps = 1 },
        new RobotUpgrade { Id = ExpandedBays(2), X = 2, Y = 0, Parent = ExpandedBays(1), SlotSteps = 1 },
        new RobotUpgrade { Id = ExpandedBays(3), X = 3, Y = 0, Parent = ExpandedBays(2), SlotSteps = 1 },
        new RobotUpgrade { Id = ExpandedBays(4), X = 4, Y = 0, Parent = ExpandedBays(3), SlotSteps = 1 },
        new RobotUpgrade { Id = ExpandedBays(5), X = 5, Y = 0, Parent = ExpandedBays(4), SlotSteps = 1 },
        // Down from Salvage Drones to Drop Pods.
        new RobotUpgrade { Id = RestorativeNanobots, X = 3, Y = 1, Parent = RoboticsModule.SalvageDronesId, Department = RoboticsModule.SalvageDronesId, HealthRegen = 1 },
        new RobotUpgrade { Id = BolsteredSwarm, X = 3, Y = 2, Parent = RestorativeNanobots, Department = RoboticsModule.SalvageDronesId, HealthRegen = 2 },
        // Around Drop Pods: Rapid Response Unit on its right, then the rest around that.
        new RobotUpgrade { Id = RapidResponseUnit, X = 4, Y = 3, Parent = RoboticsModule.DropPodsId, Department = RoboticsModule.DropPodsId },
        new RobotUpgrade { Id = FoamCanisters, X = 4, Y = 2, Parent = RapidResponseUnit, Department = RoboticsModule.DropPodsId },
        new RobotUpgrade { Id = ElectricFence, X = 5, Y = 3, Parent = RapidResponseUnit, Department = RoboticsModule.DropPodsId },
        new RobotUpgrade { Id = FlatPackFurniture, X = 4, Y = 4, Parent = RapidResponseUnit, Department = RoboticsModule.DropPodsId },
    };
    public static RobotUpgrade Find(string id) => All.FirstOrDefault(u => u.Id == id);

    // The campaign's MagnumProgression, kept current by the load/new-game patches
    // (it is a global component, so it is loaded on the ship and in missions).
    private static MagnumProgression progression;
    public static void Track(MagnumProgression value) => progression = value;
    public static bool Owns(string id) { var owner = progression; return owner != null && owner.IsPerkPurchased(id); }

    public static RobotUpgrade Total(Func<string, bool> owned)
    {
        var total = new RobotUpgrade();
        foreach (var upgrade in All.Where(u => owned(u.Id)))
        {
            total.Health += upgrade.Health; total.Resists += upgrade.Resists;
            total.Accuracy += upgrade.Accuracy; total.ActionPoints += upgrade.ActionPoints;
            total.SlotSteps += upgrade.SlotSteps; total.HealthRegen += upgrade.HealthRegen;
        }
        return total;
    }
    public static RobotUpgrade Current
    {
        get { var owner = progression; return Total(id => owner != null && owner.IsPerkPurchased(id)); }
    }
    // Robot slots: Printyard gives one set, each Expanded Bays one more. A set
    // is the "Robot slots per upgrade" setting (1-5, default 2): 2 to 12 slots.
    // Copied from the settings at startup and on MCM save (Plugin itself needs
    // the game running, so it is not read here).
    private static int slotsPerSet = 2;
    public static int SlotsPerSet { get => slotsPerSet; set => slotsPerSet = Math.Max(1, Math.Min(5, value)); }
    public static int Slots(RobotUpgrade owned, int perSet) => perSet * (1 + (owned?.SlotSteps ?? 0));
    public static int SlotCapacity(int fallback) => progression == null ? fallback : Slots(Current, SlotsPerSet);

    public static bool IsRobot(CreatureData data) => RobotIdentity.Find(data) != null;

    // Max health lives in HealthInfo, so apply changes the way the game applies
    // IMaxHealth perks: PerksMaxHealth remembers what is already included.
    public static void SyncHealth(CreatureData data)
    {
        if (data?.Health == null || !IsRobot(data)) return;
        int now = PerkSystem.GetPerkParameterSumInt(data, "IMaxHealth");
        int change = now - data.PerksMaxHealth;
        data.PerksMaxHealth = now;
        if (change != 0) data.Health.IncreaseMaxHealth(change);
        // New plating arrives intact: a bonus raises current health too.
        if (change > 0 && !data.Health.Dead) data.Health.Restore(change, ignoreHealMultiplier: true);
    }

}

[HarmonyPatch(typeof(PerkSystem), nameof(PerkSystem.GetPerkParameterSumInt))]
public static class RobotUpgradeIntPatch
{
    [HarmonyPostfix]
    public static void Postfix(CreatureData creatureData, string parameterName, ref int __result)
    {
        if (parameterName != "IMaxHealth" && parameterName != "IResists" && parameterName != "IHealthRegen") return;
        if (!RobotUpgrades.IsRobot(creatureData)) return;
        var bonus = RobotUpgrades.Current;
        __result += parameterName == "IMaxHealth" ? bonus.Health : parameterName == "IResists" ? bonus.Resists : bonus.HealthRegen;
    }
}
// Health regen: the game only applies IHealthRegen to the player, at the start
// of each turn. Robots get the same, at the start of their turn.
[HarmonyPatch(typeof(Monster), nameof(Monster.ProcessActionPoint))]
public static class RobotHealthRegenPatch
{
    [HarmonyPrefix]
    public static void Prefix(Monster __instance)
    {
        if (__instance.ActionPointsProcessed != 0 || __instance.ApRefundGranted) return;
        Regen(__instance.CreatureData);
    }
    public static int Regen(CreatureData data)
    {
        if (data?.Health == null || data.Health.Dead || !RobotUpgrades.IsRobot(data)) return 0;
        int amount = data.GetHealthRegenBonus();
        if (amount > 0) data.Health.Restore(amount);
        return amount;
    }
}
[HarmonyPatch(typeof(PerkSystem), nameof(PerkSystem.GetPerkParameterSumFloat))]
public static class RobotUpgradeFloatPatch
{
    [HarmonyPostfix]
    public static void Postfix(CreatureData creatureData, string parameterName, ref float __result)
    {
        if (parameterName != "FAccuracyBonus" || !RobotUpgrades.IsRobot(creatureData)) return;
        __result += RobotUpgrades.Current.Accuracy / 100f;
    }
}
// Monsters take their action points when spawned (missions and floor transfers).
[HarmonyPatch(typeof(CreatureSystem), nameof(CreatureSystem.SpawnMonster))]
public static class RobotUpgradeActionPointsPatch
{
    [HarmonyPrefix]
    public static void Prefix(CreatureData creatureData, ref int customActionPoints)
    {
        if (customActionPoints > 0 || !RobotUpgrades.IsRobot(creatureData)) return;
        int bonus = RobotUpgrades.Current.ActionPoints;
        if (bonus > 0) customActionPoints = creatureData.BaseActionPoints + bonus;
    }
}
[HarmonyPatch(typeof(MagnumProgression), nameof(MagnumProgression.OnAfterLoad))]
public static class RobotUpgradeTrackLoadPatch
{
    [HarmonyPostfix] public static void Postfix(MagnumProgression __instance) => RobotUpgrades.Track(__instance);
}
[HarmonyPatch(typeof(MagnumProgression), nameof(MagnumProgression.InitDepartments))]
public static class RobotUpgradeTrackNewPatch
{
    [HarmonyPostfix] public static void Postfix(MagnumProgression __instance) => RobotUpgrades.Track(__instance);
}
[HarmonyPatch(typeof(MagnumProgression), nameof(MagnumProgression.AddPerk))]
public static class RobotUpgradeTrackBuyPatch
{
    [HarmonyPostfix] public static void Postfix(MagnumProgression __instance) => RobotUpgrades.Track(__instance);
}
