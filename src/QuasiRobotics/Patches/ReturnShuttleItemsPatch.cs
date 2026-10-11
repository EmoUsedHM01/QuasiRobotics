using System;
using System.Collections.Generic;
using HarmonyLib;
using MGSC;
using QuasiRobotics.Packages;

namespace QuasiRobotics.Patches;

[HarmonyPatch(typeof(MagnumCargoSystem), "ReturnShuttleItems")]
public static class ReturnShuttleItemsPatch
{
	[HarmonyPostfix]
	public static void Postfix(MagnumCargo magnumCargo, SpaceTime spaceTime)
	{
		try
		{
			if (PendingRecoveries.Count == 0 || magnumCargo == null)
			{
				return;
			}
			List<BasePickupItem> list = PendingRecoveries.TakeAll();
			int num = 0;
			foreach (BasePickupItem item in list)
			{
				try
				{
					MagnumCargoSystem.AddCargo(magnumCargo, spaceTime, item, null, splittedItem: false, tabFilter: true);
					num++;
				}
				catch (Exception ex)
				{
					Plugin.Logger.LogError("Delivering one recovered AR to the hold failed; the rest are still delivered.");
					Plugin.Logger.LogException(ex);
				}
			}
			Plugin.Logger.Log("Delivered " + num + " recovered AR" + ((num == 1) ? "" : "s") + " to the cargo hold.");
		}
		catch (Exception ex2)
		{
			Plugin.Logger.LogError("Delivering recovered ARs to the hold failed.");
			Plugin.Logger.LogException(ex2);
		}
	}
}
