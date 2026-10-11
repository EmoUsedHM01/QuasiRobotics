using System;
using HarmonyLib;
using MGSC;
using QuasiRobotics.Packages;

namespace QuasiRobotics.Patches;

[HarmonyPatch(typeof(GameModeStateMachine), "RunMainMenu")]
public static class MainMenuDiscardPatch
{
	[HarmonyPostfix]
	public static void Postfix()
	{
		try
		{
			PendingRecoveries.DiscardStale("the main menu was reached");
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("Discarding stale packed ARs at the main menu failed.");
			Plugin.Logger.LogException(ex);
		}
	}
}
