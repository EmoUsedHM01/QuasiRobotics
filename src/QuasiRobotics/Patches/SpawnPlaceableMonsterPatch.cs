using System;
using System.Collections.Generic;
using HarmonyLib;
using MGSC;
using QuasiRobotics.Packages;

namespace QuasiRobotics.Patches;

[HarmonyPatch(typeof(ItemInteractionSystem), "SpawnPlaceableMonster")]
public static class SpawnPlaceableMonsterPatch
{
	[HarmonyPostfix]
	public static void Postfix(Creatures creatures, BasePickupItem item, Monster __result)
	{
		try
		{
			if (__result == null || item == null)
			{
				return;
			}
			ArLoadoutComponent arLoadoutComponent = ArLoadout.Find(item);
			if (arLoadoutComponent == null)
			{
				return;
			}
			CreatureData creatureData = __result.CreatureData;
			if (creatureData == null)
			{
				return;
			}
			if (string.IsNullOrEmpty(arLoadoutComponent.MobClassId) || !string.Equals(arLoadoutComponent.MobClassId, creatureData.MobClassId, StringComparison.Ordinal))
			{
				Plugin.Logger.LogWarning("Package holds '" + arLoadoutComponent.MobClassId + "' but a " + creatureData.MobClassId + " was spawned; deploying it stock.");
				return;
			}
			Inventory inventory = creatureData.Inventory;
			Dictionary<string, string> augmentationMap = creatureData.AugmentationMap;
			Dictionary<string, ImplantSocketData> woundSlotMap = creatureData.WoundSlotMap;
			Dictionary<string, Perk> implantActivesMap = creatureData.ImplantActivesMap;
			try
			{
				ArLoadout.Apply(arLoadoutComponent, creatureData);
			}
			catch
			{
				creatureData.Inventory = inventory;
				creatureData.AugmentationMap = augmentationMap;
				creatureData.WoundSlotMap = woundSlotMap;
				creatureData.ImplantActivesMap = implantActivesMap;
				try
				{
					AugmentationSystem.ConfigureImplicitEffects(creatureData);
				}
				catch (Exception ex)
				{
					Plugin.Logger.LogWarning("Rolling back a failed transplant could not recompute the AR's effects; it may carry stats from the saved parts.");
					Plugin.Logger.LogException(ex);
				}
				throw;
			}
			arLoadoutComponent.Inventory = null;
			arLoadoutComponent.MobClassId = null;
			try
			{
				__result.ConfigureData(creatureData);
				if (__result.Creature3dView != null)
				{
					__result.Creature3dView.ConfigureInventory(creatureData.Inventory);
					__result.Creature3dView.RefreshItemsRender();
				}
				Mercenary merc = ((creatures != null && creatures.Player != null) ? creatures.Player.Mercenary : null);
				CreatureSystem.RefreshResists(creatureData, merc);
			}
			catch (Exception ex2)
			{
				Plugin.Logger.LogError("A saved loadout was restored onto a deployed " + creatureData.MobClassId + ", but rebinding the creature to it failed. It may show the wrong weapon or ignore equipment changes until the dungeon is reloaded.");
				Plugin.Logger.LogException(ex2);
				return;
			}
			Plugin.Logger.Log("Restored a saved loadout onto a deployed " + creatureData.MobClassId + ".");
		}
		catch (Exception ex3)
		{
			Plugin.Logger.LogError("Restoring a saved AR loadout failed; it is deployed stock.");
			Plugin.Logger.LogException(ex3);
		}
	}
}
