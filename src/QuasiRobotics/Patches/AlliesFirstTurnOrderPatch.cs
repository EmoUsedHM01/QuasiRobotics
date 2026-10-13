using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics.Patches;

// Optional turn order Player -> Allies -> Enemies. The game runs a round in the
// order creatures joined the map, so allies that arrive with or after the
// player (robots, followers) act after enemies. With the MCM option on, the
// monsters in the round list are reordered: allies of the player first, then
// everyone else, each group keeping its own order. The player and non-creature
// entries keep their places. The round in progress is never touched.
public static class AlliesFirstTurnOrder
{
    static readonly AccessTools.FieldRef<TurnController, List<ITurnable>> Contenders =
        AccessTools.FieldRefAccess<TurnController, List<ITurnable>>("_turnContenders");

    public static bool Enabled => Plugin.Settings?.AlliesActBeforeEnemies ?? false;

    public static void Apply(TurnController controller)
    {
        if (!Enabled || controller == null) return;
        var list = Contenders(controller);
        var player = list.OfType<Player>().FirstOrDefault();
        if (player == null) return;
        var slots = new List<int>();
        for (int i = 0; i < list.Count; i++)
            if (list[i] is Monster) slots.Add(i);
        ReorderSlots(list, slots, t => t is Monster m && m.CreatureData != null && m.IsAlly(player));
    }

    // Stable: the given slots are refilled with allies first, then the rest.
    public static void ReorderSlots<T>(List<T> list, List<int> slots, Func<T, bool> isAlly)
    {
        if (slots.Count < 2) return;
        var items = slots.Select(i => list[i]).ToList();
        var ordered = items.Where(isAlly).Concat(items.Where(t => !isAlly(t))).ToList();
        for (int k = 0; k < slots.Count; k++) list[slots[k]] = ordered[k];
    }
}

[HarmonyPatch(typeof(TurnController), nameof(TurnController.Tick))]
public static class AlliesFirstTurnTickPatch
{
    static readonly AccessTools.FieldRef<TurnController, Queue<ITurnable>> Queue =
        AccessTools.FieldRefAccess<TurnController, Queue<ITurnable>>("_turnQueue");

    // A new round is built from the list when the queue is empty.
    [HarmonyPrefix]
    public static void Prefix(TurnController __instance)
    {
        try { if (Queue(__instance).Count == 0) AlliesFirstTurnOrder.Apply(__instance); }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
}

// Creatures joining mid-round (drop pods, reinforcements) take their place for
// the next round straight away.
[HarmonyPatch(typeof(TurnController), nameof(TurnController.AddContender))]
public static class AlliesFirstTurnAddPatch
{
    [HarmonyPostfix]
    public static void Postfix(TurnController __instance)
    {
        try { AlliesFirstTurnOrder.Apply(__instance); }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
}
