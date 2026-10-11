using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using MGSC;
using QuasiRobotics.Packages;

namespace QuasiRobotics.Patches;

[HarmonyPatch(typeof(DungeonGameMode), "FinishGame")]
public static class FinishGamePackPatch
{
	private static readonly FieldInfo StateField = AccessTools.Field(typeof(DungeonGameMode), "_state");

	private static readonly FieldInfo FinishedField = AccessTools.Field(typeof(DungeonGameMode), "_finished");

	private static readonly FieldInfo TakenMonstersField = AccessTools.Field(typeof(MonsterTransfer), "_takenMonster");

	[HarmonyPrefix]
	public static void Prefix(DungeonGameMode __instance, DungeonFinishedData dungeonFinishedData)
	{
		try
		{
			if (!IsExtractionToOrbit(__instance, dungeonFinishedData) || !(StateField.GetValue(__instance) is State state))
			{
				return;
			}
			Creatures creatures = state.Get<Creatures>();
			MapGrid mapGrid = state.Get<MapGrid>();
			MonsterTransfer monsterTransfer = state.Get<MonsterTransfer>();
			if (creatures != null && mapGrid != null && monsterTransfer != null)
			{
				PendingRecoveries.Clear();
				monsterTransfer.TakeTransferableMonsters(creatures, mapGrid);
				object value = TakenMonstersField.GetValue(monsterTransfer);
				PackAll(value as IEnumerable);
				if (value is IList list)
				{
					list.Clear();
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogError("Packing ARs at extraction failed; any followers are left behind exactly as they would be without this mod.");
			Plugin.Logger.LogException(ex);
		}
	}

	private static bool IsExtractionToOrbit(DungeonGameMode mode, DungeonFinishedData data)
	{
		if (FinishedField != null && (bool)FinishedField.GetValue(mode))
		{
			return false;
		}
		if (data == null)
		{
			return false;
		}
		if (data.Reason != GameFinishedReason.FinishReached)
		{
			return false;
		}
		if (!string.IsNullOrEmpty(data.To.LocationUniqueId))
		{
			return false;
		}
		Player player = (StateField.GetValue(mode) as State)?.Get<Creatures>()?.Player;
		if (player?.CreatureData?.Health == null || !player.CreatureData.Health.Alive)
		{
			return false;
		}
		foreach (BaseScenario value in mode.Scenarios.Values)
		{
			BaseDungeonScenario baseDungeonScenario = value as BaseDungeonScenario;
			if (baseDungeonScenario != null && baseDungeonScenario.ForbidMonsterTransfer)
			{
				return false;
			}
		}
		return true;
	}

	private static void PackAll(IEnumerable takenMonsters)
	{
		if (takenMonsters == null)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		FieldInfo fieldInfo = null;
		foreach (object takenMonster in takenMonsters)
		{
			num++;
			try
			{
				if (fieldInfo == null)
				{
					fieldInfo = AccessTools.Field(takenMonster.GetType(), "CreatureData");
				}
				if (fieldInfo?.GetValue(takenMonster) is CreatureData data && Pack(data))
				{
					num2++;
				}
			}
			catch (Exception ex)
			{
				Plugin.Logger.LogError("Packing one AR failed; it is left behind, and the remaining followers are still packed.");
				Plugin.Logger.LogException(ex);
			}
		}
		Plugin.Logger.Log("Extraction to orbit: " + num + " follower(s) eligible to transfer, " + num2 + " packed for the cargo hold.");
	}

	private static bool Pack(CreatureData data)
	{
		if (RobotIdentity.Find(data) != null) return false;
		if (!ArPackageCatalogue.TryGetPackageId(data.MobClassId, out var itemId))
		{
			return false;
		}
		BasePickupItem basePickupItem = SingletonMonoBehaviour<ItemFactory>.Instance.CreateForInventory(itemId, randomizeConditionAndCapacity: false, isSingleItem: true);
		if (!(basePickupItem is PickupItem pickupItem))
		{
			Plugin.Logger.LogWarning("Could not build package '" + itemId + "' for a " + data.MobClassId + "; it is left behind.");
			return false;
		}
		pickupItem.SetMaxStack(1);
		pickupItem.Add(ArLoadout.Capture(data));
		PendingRecoveries.Add(basePickupItem);
		return true;
	}
}
