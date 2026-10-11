using System;
using System.Reflection;
using HarmonyLib;
using MGSC;
using QuasiRobotics.Packages;
using UnityEngine;

namespace QuasiRobotics.Patches;

[HarmonyPatch(typeof(ItemSlot), "Initialize")]
public static class ItemSlotBadgePatch
{
	private static readonly FieldInfo ModifiedIconField = AccessTools.Field(typeof(ItemSlot), "_modifiedIcon");

	private static bool _reported;

	[HarmonyPostfix]
	public static void Postfix(ItemSlot __instance, BasePickupItem item)
	{
		try
		{
			if (item != null && ArLoadout.Find(item) != null)
			{
				Component component = ModifiedIconField?.GetValue(__instance) as Component;
				if (component != null)
				{
					component.gameObject.SetActive(value: true);
				}
			}
		}
		catch (Exception ex)
		{
			if (!_reported)
			{
				_reported = true;
				Plugin.Logger.LogError("Badging recovered AR packages failed; they will look like stock packages for the rest of this session.");
				Plugin.Logger.LogException(ex);
			}
		}
	}
}
