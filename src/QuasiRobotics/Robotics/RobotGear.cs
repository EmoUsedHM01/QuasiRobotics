using System.Runtime.CompilerServices;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics;

// Robots cannot wear a vest or backpack unless the MCM option allows it.
// Every equip path (drag, swap, context menu, auto-equip) asks the slot's
// IsValidItem, so refusing there blocks them all. Gear already worn stays
// until the player takes it off.
public static class RobotGear
{
    static readonly ConditionalWeakTable<ItemStorage, object> Blocked = new();
    static readonly object Marker = new();

    public static void Track(Inventory inventory)
    {
        if (inventory == null) return;
        Blocked.GetValue(inventory.VestSlot, _ => Marker);
        Blocked.GetValue(inventory.BackpackSlot, _ => Marker);
    }
    public static bool Refuses(ItemStorage storage, bool allowed) =>
        !allowed && storage != null && Blocked.TryGetValue(storage, out _);
}

[HarmonyPatch(typeof(ItemStorage), nameof(ItemStorage.IsValidItem))]
public static class RobotGearSlotPatch
{
    [HarmonyPostfix]
    public static void Postfix(ItemStorage __instance, ref bool __result)
    {
        if (__result && RobotGear.Refuses(__instance, Plugin.Settings?.AllowRobotVestAndBackpack ?? false)) __result = false;
    }
}

// Hide a robot's vest and backpack slots on every inventory screen while the
// option is off. A slot that still holds gear stays visible so it can be taken
// off. Slot views are reused for mercenaries, so only slots hidden here are
// shown again.
public static class RobotGearSlotView
{
    static readonly ConditionalWeakTable<ItemSlot, object> Hidden = new();
    static readonly object Marker = new();

    public static bool ShouldHide(ItemStorage storage, bool allowed) =>
        storage != null && storage.Empty && RobotGear.Refuses(storage, allowed);

    public static void Apply(ItemSlot slot, ItemStorage storage)
    {
        if (slot == null) return;
        if (ShouldHide(storage, Plugin.Settings?.AllowRobotVestAndBackpack ?? false))
        {
            Hidden.GetValue(slot, _ => Marker);
            slot.gameObject.SetActive(false);
        }
        else if (Hidden.TryGetValue(slot, out _))
        {
            Hidden.Remove(slot);
            slot.gameObject.SetActive(true);
        }
    }
}

[HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.Initialize),
    new[] { typeof(BasePickupItem), typeof(ItemStorage), typeof(ItemSlot.DisplayMode) })]
public static class RobotGearSlotViewPatch
{
    [HarmonyPostfix] public static void Postfix(ItemSlot __instance, ItemStorage itemStorage) => RobotGearSlotView.Apply(__instance, itemStorage);
}

[HarmonyPatch(typeof(ItemSlot), nameof(ItemSlot.InitializeEmpty), new[] { typeof(ItemStorage) })]
public static class RobotGearEmptySlotViewPatch
{
    [HarmonyPostfix] public static void Postfix(ItemSlot __instance, ItemStorage storage) => RobotGearSlotView.Apply(__instance, storage);
}
