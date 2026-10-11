using System;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics;

// Weapon limbs (e.g. the Judge ABC on Combat ARs) sit in the bare hands slot.
// The game only falls back to them when the current weapon slot is empty, so a
// robot holding a dry gun it cannot reload kept trying to use it. Robots now
// switch to their best usable weapon, using an empty slot to reach the limb.
public static class RobotWeaponFallback
{
    static readonly WeaponSlotType[] Order =
        { WeaponSlotType.Primary, WeaponSlotType.Secondary, WeaponSlotType.ServoArm, WeaponSlotType.Additional };

    // 3 = gun that can fire now, 2 = gun it can reload, 1 = melee, 0 = useless.
    public static int Score(bool melee, bool broken, bool canAttack, bool reloadable) =>
        broken ? 0 : melee ? 1 : canAttack ? 3 : reloadable ? 2 : 0;

    public static int Score(Inventory inv, BasePickupItem item)
    {
        var record = item?.Record<WeaponRecord>();
        var weapon = item?.Comp<WeaponComponent>();
        if (record == null || weapon == null) return 0;
        bool broken = item.Comp<BreakableItemComponent>()?.IsBroken ?? false;
        bool reloadable = !record.IsMelee && !weapon.CanAttack && !broken
            && (ReloadWeaponSystem.CanReload(inv, item, inv.Storages, currentSlotDependent: false)
                || ReloadLauncherSystem.CanReload(null, item, inv.Storages));
        return Score(record.IsMelee, broken, weapon.CanAttack, reloadable);
    }

    static ItemStorage Slot(Inventory inv, WeaponSlotType type) => type switch
    {
        WeaponSlotType.Primary => inv.PrimarySlot,
        WeaponSlotType.Secondary => inv.SecondarySlot,
        WeaponSlotType.ServoArm => inv.ServoArmSlot,
        _ => inv.AdditionalSlot,
    };

    public static bool Apply(Monster monster)
    {
        var data = monster?.CreatureData;
        var inv = data?.Inventory;
        if (inv == null || data.Health.Dead || !RobotUpgrades.IsRobot(data)
            || inv.AccessMask.HasFlag(InventoryAccessMask.WeaponSlots)) return false;
        // Only step in when the current weapon is useless; a melee pick next
        // to an enemy is the game's own choice.
        int current = Score(inv, inv.CurrentWeapon);
        if (current > 0) return false;
        var limb = inv.BareHandsSlot.First ?? inv.ArmStumpSlot.First;
        int limbScore = Score(inv, limb);
        WeaponSlotType? best = null;
        int bestScore = current;
        foreach (var type in Order)
        {
            var item = Slot(inv, type).First;
            // An empty slot means "use the limb"; it works even for a slot the
            // robot does not have, since that slot is always empty.
            if (item != null && !inv.IsSlotAvailable(type)) continue;
            int score = item == null ? limbScore : Score(inv, item);
            if (score > bestScore) { best = type; bestScore = score; }
        }
        if (best == null || best == inv.CurrentSlotType) return false;
        inv.SetCurrentWeaponSlot(best.Value);
        monster.Creature3dView?.RefreshItemsRender();
        return true;
    }

    public static bool CurrentIsUsableGun(Monster monster)
    {
        var inv = monster?.CreatureData?.Inventory;
        if (inv == null || !RobotUpgrades.IsRobot(monster.CreatureData)) return false;
        var item = inv.CurrentWeapon;
        return item?.Record<WeaponRecord>() is { IsMelee: false } && Score(inv, item) >= 2;
    }
}

[HarmonyPatch(typeof(Monster), nameof(Monster.ProcessActionPoint))]
public static class RobotWeaponFallbackTurnPatch
{
    [HarmonyPrefix]
    public static void Prefix(Monster __instance)
    {
        try { RobotWeaponFallback.Apply(__instance); }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
}

// The attack state re-picks a weapon every action point and only looks at the
// slot items, so it can pick the dry gun again.
[HarmonyPatch(typeof(FightState), "PickBestWeapon", new[] { typeof(CellPosition) })]
public static class RobotWeaponFallbackPickPatch
{
    [HarmonyPostfix]
    public static void Postfix(Monster ____owner)
    {
        try { RobotWeaponFallback.Apply(____owner); }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
}

// With two dry guns in hand the AI decides "all weapons are faulty" and walks
// up to punch, never checking the limb. Not true when the limb can shoot.
[HarmonyPatch(typeof(FightState), "HasToMelee")]
public static class RobotWeaponFallbackMeleePatch
{
    [HarmonyPostfix]
    public static void Postfix(Monster ____owner, ref bool __result)
    {
        try { if (__result && RobotWeaponFallback.CurrentIsUsableGun(____owner)) __result = false; }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
}
