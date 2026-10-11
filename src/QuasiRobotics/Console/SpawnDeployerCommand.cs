using System;
using System.Collections.Generic;
using MGSC;

namespace QuasiRobotics.Console;

[ConsoleCommand(new string[] { "qr_deployer" })]
public class SpawnDeployerCommand
{
	[Inject(false)]
	private readonly Creatures _creatures;

	[Inject(false)]
	private readonly MapGrid _mapGrid;

	[Inject(false)]
	private readonly Mercenaries _mercenaries;

	[Inject(false)]
	private readonly PerkFactory _perkFactory;

	[Inject(false)]
	private readonly Difficulty _difficulty;

	[Inject(false)]
	private readonly RaidMetadata _raidMetadata;

	[Inject(false)]
	private readonly TurnController _turnController;

	private const string DefaultPackageId = "packaged_security_robot";

	private const int SearchRadius = 14;

	public static string Help(string command, bool verbose)
	{
		return "qr_deployer [packageId] - gives the nearest hostile creature a packaged AR, so you can watch it deploy one. Defaults to packaged_security_robot.\nqr_deployer spawn <mobClassId> [packageId] - spawns a hostile creature beside you and arms that instead. Note the game cannot spawn most humans from the console: their body type comes from the faction spawn system, and spawning one directly throws inside the game's own GenerateMonster. Robots spawn fine.\nEither way it only deploys once it is in a fight and can see you, so give it a turn.";
	}

	public string Execute(string[] tokens)
	{
		if (_creatures == null || _creatures.Player == null)
		{
			return "No player in this scene.";
		}
		if (tokens != null && tokens.Length != 0 && string.Equals(tokens[0], "spawn", StringComparison.OrdinalIgnoreCase))
		{
			if (tokens.Length < 2)
			{
				return "Give a mob class id: qr_deployer spawn <mobClassId> [packageId].";
			}
			string packageId = ((tokens.Length > 2 && !string.IsNullOrEmpty(tokens[2])) ? tokens[2] : "packaged_security_robot");
			return SpawnAndArm(tokens[1], packageId);
		}
		string packageId2 = ((tokens != null && tokens.Length != 0 && !string.IsNullOrEmpty(tokens[0])) ? tokens[0] : "packaged_security_robot");
		Monster monster = FindNearestHostile();
		if (monster == null)
		{
			return "No hostile creature within " + 14 + " tiles. Get closer to one, or use: qr_deployer spawn <mobClassId>";
		}
		return Arm(monster, packageId2);
	}

	private string SpawnAndArm(string mobClassId, string packageId)
	{
		if (!TryFindFreeCellBesidePlayer(out var found))
		{
			return "No free cell next to you to spawn into. Move somewhere more open.";
		}
		Monster monster;
		try
		{
			monster = CreatureSystem.SpawnMonsterFromMobClass(_mercenaries, _perkFactory, _difficulty, _creatures, _raidMetadata, _turnController, mobClassId, found, -1, spawnEquipment: true, -1, isLeader: false, 5);
		}
		catch (Exception ex)
		{
			Plugin.Logger.LogException(ex);
			return "The game could not build '" + mobClassId + "' (" + ex.GetType().Name + "). Most human mob classes cannot be spawned from the console at all - their body type comes from the faction spawn system. Try a robot id, or just run 'ar_deployer' to arm an enemy already on the map.";
		}
		if (monster == null)
		{
			return "Could not spawn '" + mobClassId + "'. Check the id against the game's own spawn command.";
		}
		return Arm(monster, packageId);
	}

	private string Arm(Monster monster, string packageId)
	{
		BasePickupItem basePickupItem = SingletonMonoBehaviour<ItemFactory>.Instance.CreateForInventory(packageId, randomizeConditionAndCapacity: false, isSingleItem: true);
		if (basePickupItem == null)
		{
			return "Could not build '" + packageId + "'. Check the item id.";
		}
		if (!TryGivePackage(monster, basePickupItem, out var storageName))
		{
			return "Nothing in " + monster.CreatureData?.ToString() + "'s inventory would take '" + packageId + "' - no storage with room. Try a creature that carries a backpack.";
		}
		return "Gave " + monster.CreatureData?.ToString() + " a '" + packageId + "' in its " + storageName + ". It deploys once it is in a fight and can see you.";
	}

	private Monster FindNearestHostile()
	{
		CellPosition position = _creatures.Player.CreatureData.Position;
		List<Creature> list = new List<Creature>();
		_creatures.CollectCreaturesInRadius(position, 14, list, _creatures.Player, includeSelf: false, includePlayer: false);
		Monster result = null;
		int num = int.MaxValue;
		foreach (Creature item in list)
		{
			Monster monster = item as Monster;
			if (!(monster == null) && monster.CreatureData != null && monster.CreatureData.Health != null && monster.CreatureData.Health.Alive && (monster.CreatureData.CreatureAlliance & CreatureAlliance.PlayerAlliance) == 0)
			{
				CellPosition position2 = monster.CreatureData.Position;
				int num2 = position2.X - position.X;
				int num3 = position2.Y - position.Y;
				int num4 = num2 * num2 + num3 * num3;
				if (num4 < num)
				{
					num = num4;
					result = monster;
				}
			}
		}
		return result;
	}

	private bool TryFindFreeCellBesidePlayer(out CellPosition found)
	{
		found = default(CellPosition);
		CellPosition position = _creatures.Player.CreatureData.Position;
		for (int i = -1; i <= 1; i++)
		{
			for (int j = -1; j <= 1; j++)
			{
				if (i != 0 || j != 0)
				{
					MapCell cell = _mapGrid.GetCell(position.X + i, position.Y + j);
					if (cell != null && CreatureSystem.IsValidForSpawn(cell, _creatures))
					{
						found = new CellPosition(position.X + i, position.Y + j);
						return true;
					}
				}
			}
		}
		return false;
	}

	private static bool TryGivePackage(Monster monster, BasePickupItem package, out string storageName)
	{
		storageName = null;
		Inventory inventory = monster.CreatureData.Inventory;
		if (inventory == null)
		{
			return false;
		}
		List<ItemStorage> list = new List<ItemStorage>();
		if (inventory.BackpackStore != null)
		{
			list.Add(inventory.BackpackStore);
		}
		foreach (ItemStorage storage in inventory.Storages)
		{
			if (storage != null && !list.Contains(storage))
			{
				list.Add(storage);
			}
		}
		foreach (ItemStorage item in list)
		{
			if (item.TryPutItem(package, default(CellPosition)))
			{
				storageName = ((item == inventory.BackpackStore) ? "backpack" : "inventory");
				return true;
			}
		}
		return false;
	}
}
