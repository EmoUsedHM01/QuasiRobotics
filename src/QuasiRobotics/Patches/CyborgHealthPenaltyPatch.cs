using System;
using System.Collections.Generic;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics.Patches;

[HarmonyPatch(typeof(AugmentationSystem), "ProcessImplicitEffects")]
public static class CyborgHealthPenaltyPatch
{
	private const string MaxHealthEffect = "max_health";

	private static bool _reported;

	[HarmonyPrefix]
	public static void Prefix(ref Dictionary<string, float> effects, CreatureData creatureData, bool isBonus)
	{
		try
		{
			if (Plugin.Settings == null || !Plugin.Settings.RobotsIgnoreAugmentationHealthPenalty || isBonus || effects == null || !effects.ContainsKey("max_health") || creatureData == null || creatureData.CreatureClass != CreatureClass.Cyborg)
			{
				return;
			}
			Dictionary<string, float> dictionary = new Dictionary<string, float>(effects.Count);
			foreach (KeyValuePair<string, float> effect in effects)
			{
				if (!string.Equals(effect.Key, "max_health", StringComparison.Ordinal))
				{
					dictionary[effect.Key] = effect.Value;
				}
			}
			effects = dictionary;
		}
		catch (Exception ex)
		{
			if (!_reported)
			{
				_reported = true;
				Plugin.Logger.LogError("Exempting robots from the augmentation health penalty failed; the game's own penalty applies for the rest of this session.");
				Plugin.Logger.LogException(ex);
			}
		}
	}
}
