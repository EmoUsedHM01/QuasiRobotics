using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics.Patches;

// "Disable friendly fire" in Mod Config Menu (default on). Units on the player's
// side (the operator, robots, turrets and other allies) no longer hit each other:
//  - bullets fly through friendly units as if they were not there, so there is no
//    damage number, no on-hit effect and no "turned traitor" reaction;
//  - explosions skip friendly units in the blast, and allies no longer hurt
//    themselves with their own explosions;
//  - friendly melee swings are cancelled before they land.
// The operator can still hit allies on purpose with the game's own force-attack
// key ("Attack / throw at tile", Shift + click by default), whatever it is bound to.
// Enemies still hurt their own allies as normal.
public static class FriendlyFire
{
    public static bool Enabled => Plugin.Settings != null && Plugin.Settings.DisableFriendlyFire;

    // Only the player's side is protected; enemies still hit their own allies.
    public static bool Blocks(CreatureAlliance attacker, CreatureAlliance target) =>
        attacker.HasFlag(CreatureAlliance.PlayerAlliance) || target.HasFlag(CreatureAlliance.PlayerAlliance);

    // The whole rule, kept free of game objects so it can be tested.
    public static bool Decide(bool attackerIsPlayer, bool sameCreature, bool allied,
        CreatureAlliance attacker, CreatureAlliance target, bool forced)
    {
        // A unit caught in its own blast: allies are spared, the operator is not.
        if (sameCreature) return !attackerIsPlayer && attacker.HasFlag(CreatureAlliance.PlayerAlliance);
        if (!allied || !Blocks(attacker, target)) return false;
        // The operator's force attack goes through on purpose.
        return !(attackerIsPlayer && forced);
    }

    public static bool ShouldBlock(Creature attacker, Creature target, bool forced)
    {
        if (!Enabled || (object)attacker == null || (object)target == null) return false;
        if (attacker.CreatureData == null || target.CreatureData == null) return false;
        bool same = ReferenceEquals(attacker, target);
        bool allied = !same && (attacker.IsAlly(target) || target.IsAlly(attacker));
        return Decide(attacker is Player, same, allied,
            attacker.CreatureData.CreatureAlliance, target.CreatureData.CreatureAlliance, forced);
    }

    public static bool ShouldBlock(Creature attacker, Creature target) =>
        ShouldBlock(attacker, target, attacker is Player && ForceAttack.Active);
}

// The game's own force-attack binding. "PreciseCursorShoot" is
// "Attack / throw at tile" (LeftShift + left click by default) and
// "HighlightPreciseShoot" is its modifier on its own (LeftShift). Reading the
// bindings rather than a key code means a rebind in the Keybindings page is followed.
public static class ForceAttack
{
    public const string AttackKey = "PreciseCursorShoot";
    public const string ModifierKey = "HighlightPreciseShoot";

    // Whether the operator's latest attack order was a force attack. Shots and
    // blasts land a few frames after the click, so the order is remembered until
    // the next one.
    public static bool Latched { get; private set; }

    public static bool Active => Latched || Held();

    public static void OnInput(bool forceAttackDown, bool otherOrderDown)
    {
        if (forceAttackDown) Latched = true;
        else if (otherOrderDown) Latched = false;
    }

    public static void Reset() => Latched = false;

    static bool Held()
    {
        try
        {
            var input = SingletonMonoBehaviour<InputController>.Instance;
            return (object)input != null && (input.IsKey(ModifierKey) || input.IsKey(AttackKey));
        }
        catch (Exception)
        {
            return false;
        }
    }
}

// Remembers whether each operator order is a force attack.
[HarmonyPatch(typeof(PlayerInteractionSystem), nameof(PlayerInteractionSystem.ProcessInput))]
public static class ForceAttackInputPatch
{
    [HarmonyPrefix]
    public static void Prefix()
    {
        try
        {
            var input = SingletonMonoBehaviour<InputController>.Instance;
            if ((object)input == null) return;
            ForceAttack.OnInput(input.IsKeyDown(ForceAttack.AttackKey),
                input.IsKeyDown("PrimaryCursorAction") || input.IsKeyDown("Shoot"));
        }
        catch (Exception)
        {
        }
    }
}

// Bullets pass through friendly units: no collision means no hit roll, no damage
// number, no on-hit effects and no traitor reaction.
[HarmonyPatch(typeof(BallisticSystem), "ShouldCollideWithCreature")]
public static class FriendlyProjectilePatch
{
    [HarmonyPostfix]
    public static void Postfix(int projectileEntityId, Creature creature, ref bool __result,
        Entities ____cacheEntities, Creatures ____cacheCreatures)
    {
        if (!__result || !FriendlyFire.Enabled || ____cacheEntities == null || ____cacheCreatures == null) return;
        if (!____cacheEntities.Has<Ballistic>(projectileEntityId)) return;
        var owner = ____cacheCreatures.GetCreature(____cacheEntities.GetRef<Ballistic>(projectileEntityId).OwnerUid);
        if (FriendlyFire.ShouldBlock(owner, creature)) __result = false;
    }
}

// Explosions skip friendly units. Marking the unit as already hit makes the game
// pass over it, so knockback, stun, damage, wounds and the damage number are all
// skipped while walls, objects, fire and gas still behave as normal.
[HarmonyPatch(typeof(ExplosionEntity), "ProcessExplosionCell")]
public static class FriendlyExplosionPatch
{
    [HarmonyPrefix]
    public static void Prefix(ExplosionEntity __instance, int x, int y,
        Creatures ____creatures, Creature ____user, List<Creature> ____hittedCreatures)
    {
        if (!FriendlyFire.Enabled || ____creatures == null || ____hittedCreatures == null || (object)____user == null) return;
        var creature = ____creatures.GetCreature(x, y);
        if ((object)creature == null || ____hittedCreatures.Contains(creature)) return;
        if (FriendlyFire.ShouldBlock(____user, creature, ForcedExplosions.IsForced(__instance)))
            ____hittedCreatures.Add(creature);
    }
}

// Which explosions came from an operator force attack. Instant blasts (rockets,
// shrapnel) are made while the order is still latched; timed grenades remember
// the order they were thrown with.
public static class ForcedExplosions
{
    static readonly ConditionalWeakTable<object, object> Forced = new ConditionalWeakTable<object, object>();
    static readonly object Mark = new object();
    internal static bool ExplodingForcedGrenade;

    public static void MarkForced(object entity)
    {
        if (entity != null && !IsForced(entity)) Forced.Add(entity, Mark);
    }

    public static bool IsForced(object entity) => entity != null && Forced.TryGetValue(entity, out _);
}

[HarmonyPatch(typeof(MapEntityFactory), nameof(MapEntityFactory.CreateGrenade),
    new[] { typeof(Creature), typeof(BasePickupItem), typeof(List<CellPosition>), typeof(List<int>), typeof(float), typeof(bool), typeof(UnityEngine.Vector3?), typeof(bool) })]
public static class ForcedGrenadePatch
{
    [HarmonyPostfix]
    public static void Postfix(Creature owner, GrenadeEntity __result)
    {
        if (owner is Player && ForceAttack.Active) ForcedExplosions.MarkForced(__result);
    }
}

[HarmonyPatch(typeof(GrenadeEntity), "Explode")]
public static class ForcedGrenadeExplodePatch
{
    [HarmonyPrefix]
    public static void Prefix(GrenadeEntity __instance) =>
        ForcedExplosions.ExplodingForcedGrenade = ForcedExplosions.IsForced(__instance);

    [HarmonyFinalizer]
    public static void Finalizer() => ForcedExplosions.ExplodingForcedGrenade = false;
}

[HarmonyPatch(typeof(ExplosionFactory), nameof(ExplosionFactory.CreateExplosion),
    new[] { typeof(CellPosition), typeof(ExplosionEntity.ExplosionParameters), typeof(ExplosionEntity.ExplosionVisualParameters), typeof(Creature) })]
public static class ForcedExplosionPatch
{
    [HarmonyPostfix]
    public static void Postfix(Creature user, ExplosionEntity __result)
    {
        if (user is Player && (ForcedExplosions.ExplodingForcedGrenade || ForceAttack.Active))
            ForcedExplosions.MarkForced(__result);
    }
}

// Friendly melee swings are cancelled before they land and count as a miss.
public static class FriendlyMelee
{
    public static bool Allow(Creature attacker, Creature enemy, ref DamageHitInfo dmgHit)
    {
        if (!FriendlyFire.ShouldBlock(attacker, enemy)) return true;
        dmgHit = new DamageHitInfo { wasMiss = true, damageDealer = attacker };
        return false;
    }
}

[HarmonyPatch(typeof(Monster), nameof(Monster.ProcessMeleeAttackOnEnemy),
    new[] { typeof(Creature), typeof(DamageHitInfo), typeof(Creature.MeleeAttackInput), typeof(BasePickupItem) },
    new[] { ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal })]
public static class FriendlyMonsterMeleePatch
{
    [HarmonyPrefix]
    public static bool Prefix(Monster __instance, Creature enemy, ref DamageHitInfo dmgHit) =>
        FriendlyMelee.Allow(__instance, enemy, ref dmgHit);
}

[HarmonyPatch(typeof(Player), nameof(Player.ProcessMeleeAttackOnEnemy),
    new[] { typeof(Creature), typeof(DamageHitInfo), typeof(Creature.MeleeAttackInput), typeof(BasePickupItem) },
    new[] { ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, ArgumentType.Normal })]
public static class FriendlyPlayerMeleePatch
{
    [HarmonyPrefix]
    public static bool Prefix(Player __instance, Creature enemy, ref DamageHitInfo dmgHit) =>
        FriendlyMelee.Allow(__instance, enemy, ref dmgHit);
}

// Safety net for any other damage source (perks, abilities): a friendly hit
// that still reaches Injure does no harm.
[HarmonyPatch(typeof(Creature), nameof(Creature.Injure))]
public static class FriendlyFirePatch
{
    [HarmonyPrefix]
    public static bool Prefix(Creature __instance, DamageHitInfo hitInfo, ref InjureResult __result)
    {
        if (hitInfo == null || !FriendlyFire.ShouldBlock(hitInfo.damageDealer, __instance)) return true;
        __result = default;
        return false;
    }

    public static bool Blocks(CreatureAlliance attacker, CreatureAlliance target) => FriendlyFire.Blocks(attacker, target);
}
