using System;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics.Patches;

// Folded in from NoFriendlyFire (NBKRedSpy). Allies no longer hurt the player's
// side: robots, turrets and other player-aligned creatures stop wounding each
// other and the operator. The operator's own shots still land on anyone.
// Toggled by "Disable friendly fire" in Mod Config Menu (default on).
[HarmonyPatch(typeof(Creature), nameof(Creature.Injure))]
public static class FriendlyFirePatch
{
    [HarmonyPrefix]
    public static bool Prefix(Creature __instance, DamageHitInfo hitInfo, ref InjureResult __result)
    {
        if (!Plugin.Settings.DisableFriendlyFire) return true;
        var attacker = hitInfo.damageDealer;
        if (attacker == null || attacker is Player || attacker == __instance || !attacker.IsAlly(__instance)) return true;
        if (!Blocks(attacker.CreatureData.CreatureAlliance, __instance.CreatureData.CreatureAlliance)) return true;
        __result = default;
        return false;
    }

    // Only the player's side is protected; enemies still hit their own allies.
    public static bool Blocks(CreatureAlliance attacker, CreatureAlliance target) =>
        attacker.HasFlag(CreatureAlliance.PlayerAlliance) || target.HasFlag(CreatureAlliance.PlayerAlliance);
}
