using System;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics.Patches;

// Robots join missions only through the roster and departure screen, so the
// player cannot place a droid package as a consumable. Enemy deployers still can.
[HarmonyPatch(typeof(ItemInteractionSystem), nameof(ItemInteractionSystem.TryPlaceObject))]
public static class BlockDroidPackageUsePatch
{
    [HarmonyPrefix]
    public static bool Prefix(Creature user, BasePickupItem item, ref bool __result)
    {
        if (!IsBlocked(user is Player, item)) return true;
        __result = false;
        Notify();
        return false;
    }

    public static bool IsBlocked(bool byPlayer, BasePickupItem item) => byPlayer && RobotRoster.IsDroidPackage(item);

    public static void Notify()
    {
        SingletonMonoBehaviour<SoundController>.Instance?.PlayUiSound(SingletonMonoBehaviour<SoundsStorage>.Instance.EmptyAttack);
        UI.Staff?.NotificationPanel?.AddNotification(Localization.Get(ModText.DroidPackageBlocked), preventRepeats: true);
    }
}

// Inventory context menu: the native IsUseRestricted flag hides Use. It is a
// saved field, so it is set only while the menu is built, then restored.
[HarmonyPatch(typeof(InventoryScreen), "DragControllerShowContextMenuCallback")]
public static class HideDroidPackageUsePatch
{
    [HarmonyPrefix]
    public static void Prefix(ItemSlot obj, out BasePickupItem __state)
    {
        __state = null;
        var item = obj?.Item;
        if (!RobotRoster.IsDroidPackage(item) || item.IsUseRestricted) return;
        item.IsUseRestricted = true;
        __state = item;
    }

    [HarmonyFinalizer]
    public static Exception Finalizer(BasePickupItem __state, Exception __exception)
    {
        if (__state != null) __state.IsUseRestricted = false;
        return __exception;
    }
}

// Vest hotbar key.
[HarmonyPatch(typeof(PlayerInteractionSystem), nameof(PlayerInteractionSystem.InteractVestSlot))]
public static class BlockDroidPackageHotbarPatch
{
    [HarmonyPrefix]
    public static bool Prefix(Creatures creatures, int slotIndex)
    {
        var item = creatures?.Player?.CreatureData?.Inventory?.VestStore?.GetItemByIndex(slotIndex - 1);
        if (!RobotRoster.IsDroidPackage(item)) return true;
        BlockDroidPackageUsePatch.Notify();
        return false;
    }
}

// Double-click or drag onto the character.
[HarmonyPatch(typeof(InventoryScreen), nameof(InventoryScreen.InteractWithCharacter))]
public static class BlockDroidPackageInteractPatch
{
    [HarmonyPrefix]
    public static bool Prefix(BasePickupItem item)
    {
        if (!RobotRoster.IsDroidPackage(item)) return true;
        BlockDroidPackageUsePatch.Notify();
        return false;
    }
}
