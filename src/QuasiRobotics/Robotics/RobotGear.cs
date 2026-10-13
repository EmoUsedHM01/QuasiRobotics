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
