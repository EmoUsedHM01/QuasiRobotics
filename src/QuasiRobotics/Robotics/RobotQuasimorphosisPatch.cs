using System;
using System.Linq;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace QuasiRobotics;

// The game adds passive Quasimorphosis each turn from the player's body only
// (IQMorphGain perks such as Skull of Amaxatli, plus Qmorph wound effects).
// With the MCM option on, robots on the map add their own passive gain too,
// scaled by the difficulty's growth the same way.
[HarmonyPatch(typeof(QmorphosController), nameof(QmorphosController.ProcessActionPoint))]
public static class RobotQuasimorphosisPatch
{
    public static int RobotGain(Creatures creatures) =>
        creatures?.Monsters == null ? 0 : creatures.Monsters
            .Where(m => m != null && m.CreatureData != null && m.CreatureData.Health.Alive && RobotUpgrades.IsRobot(m.CreatureData))
            .Sum(m => Mathf.RoundToInt(m.CreatureData.EffectsController.SumEffectsValue((WoundEffectQmorph w) => w.QmorphValue))
                + m.CreatureData.GetQmorphPassiveGain());

    public static int Scaled(int gain, float growth) => Mathf.RoundToInt(gain * growth);

    [HarmonyPostfix]
    public static void Postfix(QmorphosController __instance, RaidMetadata ____raidMetadata, Creatures ____creatures, Difficulty ____difficulty)
    {
        try
        {
            if (!(Plugin.Settings?.RobotsAddQuasimorphosis ?? false) || ____raidMetadata == null || ____raidMetadata.StationVisit) return;
            int gain = Scaled(RobotGain(____creatures), ____difficulty?.Preset?.QmorphLevelGrowth ?? 1f);
            if (gain != 0) __instance.QmorphosValue += gain;
        }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
}
