using System;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace QuasiRobotics;

// Foam Canisters: robots put out any fire on themselves when their turn ends.
// A monster's turn ends when it stops processing its last action point.
[HarmonyPatch(typeof(Monster), nameof(Monster.IsProcessing), MethodType.Setter)]
public static class FoamCanistersPatch
{
    // Removing Burning normally deals one last burn (BurningEffect.OnRemoved);
    // foam puts it out cleanly.
    public static bool Extinguishing;

    [HarmonyPrefix]
    public static void Prefix(Monster __instance, out bool __state) => __state = __instance.IsProcessing;
    [HarmonyPostfix]
    public static void Postfix(Monster __instance, bool value, bool __state)
    {
        if (!__state || value || __instance.ActionPointsProcessed < __instance.GetActualActionPoints()) return;
        try { Extinguish(__instance.CreatureData); }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
    public static bool Extinguish(CreatureData data)
    {
        if (data?.EffectsController == null || data.Health.Dead || !RobotUpgrades.IsRobot(data)
            || !RobotUpgrades.Owns(RobotUpgrades.FoamCanisters) || !data.EffectsController.HasAnyEffect<BurningEffect>()) return false;
        Extinguishing = true;
        try { data.EffectsController.RemoveAllEffects<BurningEffect>(); }
        finally { Extinguishing = false; }
        return true;
    }
}
[HarmonyPatch(typeof(BurningEffect), nameof(BurningEffect.OnRemoved))]
public static class FoamCanistersBurnPatch
{
    [HarmonyPrefix] public static bool Prefix() => !FoamCanistersPatch.Extinguishing;
}

// Electric Fence?: an enemy that lands a melee hit on a robot takes 1-40 Shock
// damage back (resists apply, as for any hit).
[HarmonyPatch(typeof(Monster), nameof(Monster.ProcessMeleeAttackOnEnemy),
    new[] { typeof(Creature), typeof(DamageHitInfo), typeof(Creature.MeleeAttackInput), typeof(BasePickupItem) },
    new[] { ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal })]
public static class ElectricFencePatch
{
    public const string DamageType = "shock";
    public const int MinDamage = 1, MaxDamage = 40;

    [HarmonyPostfix]
    public static void Postfix(Monster __instance, Creature enemy, ref DamageHitInfo dmgHit)
    {
        try
        {
            if (!ShouldCounter(__instance?.CreatureData, enemy?.CreatureData, dmgHit, RobotUpgrades.Owns(RobotUpgrades.ElectricFence))) return;
            var hit = DamageSystem.CalculateHitInfo(1, 1f, 0f, 0f,
                new DmgInfo { damage = DamageType, minDmg = MinDamage, maxDmg = MaxDamage, critDmg = 1f },
                1f, 0f, autoHit: true, noCrit: true);
            hit.damageDealer = enemy;
            hit.dangerPosition = enemy.CreatureData.Position;
            __instance.Injure(hit);
        }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
    public static bool ShouldCounter(CreatureData attacker, CreatureData robot, DamageHitInfo hit, bool owned) =>
        owned && attacker != null && robot != null && hit != null && !hit.wasMiss && !hit.wasImmune
        && RobotUpgrades.IsRobot(robot) && !RobotUpgrades.IsRobot(attacker)
        && attacker.Health.Alive && !robot.IsAlly(attacker);
}
