using System;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics;

public static class ArEquipmentStorage
{
    // Monster inventories use Endless, which deliberately ignores equipped storage.
    // Change only a recovered inventory opened in the cargo editor. These flags are saved
    // by the game and travel with that same inventory through deployment and recovery.
    public static void Enable(Inventory inventory, IFloorItemsGetter cargo)
    {
        if (inventory == null || cargo?.GetActualFloorItems() == null)
            throw new InvalidOperationException("AR equipment storage requires a cargo overflow destination.");
        inventory.InitializeItemsOnFloor(cargo);
        AccessTools.Field(typeof(Inventory), "_backpackMode").SetValue(inventory, Inventory.BackpackMode.Normal);
        inventory.CanHaveVest = true;
        // Reuse native callbacks: upgraded backpack height, servo arms, bonus vest slots,
        // reshuffling and excess items all follow the same rules as mercenaries.
        if (!inventory.BackpackSlot.Empty)
            AccessTools.Method(typeof(Inventory), "BackpackSlotOnOnItemSwitched").Invoke(
                inventory, new object[] { inventory.BackpackSlot.First, inventory.BackpackSlot.First });
        AccessTools.Method(typeof(Inventory), "UpdateTotalBonusVestSize").Invoke(inventory, null);
    }
}
