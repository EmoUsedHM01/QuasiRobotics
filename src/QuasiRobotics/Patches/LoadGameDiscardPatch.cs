using System;
using HarmonyLib;
using MGSC;
using QuasiRobotics.Packages;

namespace QuasiRobotics.Patches;

[HarmonyPatch(typeof(SaveManager), "LoadGame")]
public static class LoadGameDiscardPatch
{
	[HarmonyPostfix]
	public static void Postfix()
	{
		try
		{
			PendingRecoveries.DiscardStale("a saved game was loaded");
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("Discarding stale packed ARs on load failed.");
			Plugin.Logger.LogException(ex);
		}
	}
}
