using System;
using System.Collections.Generic;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics;

public static class ArStorageLayout
{
    public static void ResizeBackpack(Inventory inventory)
    {
        var storage = inventory.BackpackStore;
        var pack = inventory.BackpackSlot.First;
        var record = pack?.Record<BackpackRecord>();
        int capacity = record != null ? record.Width * ItemInteractionSystem.GetBackpackHeight(pack)
            : (int)AccessTools.Field(typeof(Inventory), "_defaultBackpackWidth").GetValue(inventory)
              * (int)AccessTools.Field(typeof(Inventory), "_defaultBackpackHeight").GetValue(inventory);
        int occupied = 0;
        foreach (var item in storage.Items) occupied += item.InventoryWidthSize;
        // Native storage is rectangular; round partial rows up and retain existing contents.
        int rows = Math.Max(1, (Math.Max(capacity, occupied) + 3) / 4);
        if (storage.Width == 4 && storage.Height == rows) return;
        var excess = new List<BasePickupItem>();
        storage.ResizeAndReshuffle(4, rows, ref excess);
        // Fragmentation from two-cell items may need another row. Never discard overflow.
        while (excess.Count > 0)
        {
            storage.ExpandHeight(1);
            for (int i = excess.Count - 1; i >= 0; i--)
                if (storage.TryPutItem(excess[i], CellPosition.Zero)) excess.RemoveAt(i);
        }
    }
}
