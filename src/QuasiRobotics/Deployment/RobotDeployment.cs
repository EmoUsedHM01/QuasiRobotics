using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics;

public static class RobotDeployment
{
    public static bool Select(RobotRoster roster, RobotRecord robot, DateTime now)
    {
        if (!roster.Robots.Contains(robot) || robot.IsRepairing(now) || roster.DeployedIds.Count != 0
            || roster.SelectedIds.Count >= Math.Max(0, roster.Capacity)
            || roster.SelectedIds.Contains(robot.Identity.Id)) return false;
        roster.SelectedIds.Add(robot.Identity.Id);
        return true;
    }
    public static void Begin(RobotRoster roster, DateTime now)
    {
        if (roster.DeployedIds.Count != 0 || roster.PendingSpawnIds.Count != 0) return;
        roster.PendingSpawnIds = roster.Robots.Where(r => roster.SelectedIds.Contains(r.Identity.Id) && !r.IsRepairing(now))
            .Take(Math.Max(0, roster.Capacity)).Select(r => r.Identity.Id).ToList();
        roster.SelectedIds.Clear();
    }
    public static void Track(RobotRoster roster, CreatureData data)
    {
        var identity = RobotIdentity.Find(data);
        if (identity == null || !roster.DeployedIds.Contains(identity.Id)) return;
        var record = roster.Robots.FirstOrDefault(r => r.Identity.Id == identity.Id);
        if (record != null && (data.Health.Alive || !record.Destroyed))
        {
            record.Data = data;
            record.Destroyed = data.Health.Dead;
        }
    }
    public static void TrackCorpse(RobotRoster roster, CreatureData corpse)
    {
        var id = RobotIdentity.Find(corpse)?.Id;
        if (id == null || !roster.DeployedIds.Contains(id)) return;
        var record = roster.Robots.FirstOrDefault(r => r.Identity.Id == id);
        if (record == null) return;
        // Native corpses have full HP and omit most combat stats. Copy only mutable
        // corpse-owned state onto the original chassis, not the incomplete corpse data.
        record.Data.Inventory = corpse.Inventory;
        record.Data.WoundSlotMap = corpse.WoundSlotMap;
        record.Data.AugmentationMap = corpse.AugmentationMap;
        record.Data.EffectsController = corpse.EffectsController;
        PruneRemovedImplants(record.Data);
        record.Destroyed = true;
    }
    // The roster robot a corpse came from: by its bound identity, or by the
    // roster name key it keeps (LocalizationId "ar_roster_<id>").
    public const string RosterLocalizationPrefix = "ar_roster_";
    public static RobotRecord FindCorpseRobot(RobotRoster roster, CreatureData corpse)
    {
        var id = RobotIdentity.Find(corpse)?.Id;
        if (id == null && corpse?.LocalizationId != null && corpse.LocalizationId.StartsWith(RosterLocalizationPrefix))
            id = corpse.LocalizationId.Substring(RosterLocalizationPrefix.Length);
        return id == null ? null : roster.Robots.FirstOrDefault(r => r.Identity?.Id == id);
    }
    public static void Sync(State state)
    {
        var roster = RobotRoster.For(state.Get<Mercenaries>());
        if (roster.DeployedIds.Count == 0) return;
        // Corpses are real inventory owners too. Looting one must not duplicate items
        // when its robot returns; then prefer any living instance after resurrection.
        var obstacles = state.Get<MapObstacles>();
        if (obstacles != null)
            foreach (var obstacle in obstacles.Obstacles)
                if (obstacle.CorpseStorage != null) TrackCorpse(roster, obstacle.CorpseStorage.CreatureData);
        var creatures = state.Get<Creatures>();
        if (creatures != null)
            foreach (var monster in creatures.Monsters.Where(m => m.CreatureData.Health.Alive)) Track(roster, monster.CreatureData);
        var transfer = state.Get<MonsterTransfer>();
        if (transfer != null)
            foreach (var monster in RobotRosterView.Field<List<MonsterTransfer.MonsterData>>(transfer, "_takenMonster"))
                Track(roster, monster.CreatureData);
    }
    public static void Spawn(State state)
    {
        var roster = RobotRoster.For(state.Get<Mercenaries>());
        Sync(state);
        if (roster.PendingSpawnIds.Count == 0) return;
        if (state.Get<DungeonGameMode>().Scenarios.Values.OfType<BaseDungeonScenario>().Any(s => s.ForbidMonsterTransfer))
        {
            roster.PendingSpawnIds.Clear(); // Restricted story scenes leave selected robots safely aboard.
            return;
        }
        var creatures = state.Get<Creatures>();
        var grid = state.Get<MapGrid>();
        var entities = state.Get<MapEntities>();
        bool Standable(MapCell c) => c.IsFloor && !c.IsObjBlockPass && !c.IsDoor && c.IsPassable(SafeMask)
            && creatures.GetCreature(c.X, c.Y) == null && entities.Get(c.X, c.Y) == null;
        var start = creatures.Player.CreatureData.Position;
        var cells = WalkableCellsNear(grid, start, SpawnRadius).Where(Standable).ToList();
        int walkable = cells.Count;
        // Some shuttles and start areas wall the player in at landing, so the
        // walk search finds too few tiles. Then fall back to the nearest safe
        // tiles, as the game does for followers it carries between floors.
        if (cells.Count < roster.PendingSpawnIds.Count)
            cells.AddRange(grid.BreadthFirstSearch(start, c => true, SpawnRadius)
                .Where(c => Standable(c) && !cells.Contains(c)));
        Plugin.Logger.Log($"Robot landing at {start}: {roster.PendingSpawnIds.Count} robot(s), {walkable} walkable tile(s), {cells.Count} usable.");
        try
        {
        foreach (var id in roster.PendingSpawnIds.ToArray())
        {
            var record = roster.Robots.FirstOrDefault(r => r.Identity.Id == id);
            if (record == null || cells.Count == 0) continue;
            var position = cells[0].Position;
            cells.RemoveAt(0);
            SpawnRobot(state, roster, record, position, () => roster.PendingSpawnIds.Remove(id));
        }
        }
        finally
        {
            // Unspawned robots stay aboard, never unexpectedly appear on a later floor.
            roster.PendingSpawnIds.Clear();
        }
    }
    // Mission start: robots land on the nearest free tiles they can walk to the
    // shuttle from. The search only steps through walkable tiles (no walls,
    // furniture, locked doors, fences or deep water; robots open closed doors), so a tile behind a wall is
    // never picked even when it is close as the crow flies.
    public const int SpawnRadius = 20;
    public static readonly ObstacleType WalkMask = MapCell.AllObstacles
        & ~(ObstacleType.Wall | ObstacleType.Obj | ObstacleType.Deep | ObstacleType.Indestructable
            | ObstacleType.BlockedDoor | ObstacleType.Fence);
    // Tiles a robot may stand on: walkable and free of hazards.
    public static readonly ObstacleType SafeMask = WalkMask
        & ~(ObstacleType.Toxic | ObstacleType.Fire | ObstacleType.Mine | ObstacleType.Gas | ObstacleType.Roots);
    public static List<MapCell> WalkableCellsNear(MapGrid grid, CellPosition start, int radius)
    {
        var origin = grid.GetCell(start);
        // The player's own tile always counts as the start, whatever stands on it.
        return grid.BreadthFirstSearch(start, c => c == origin || c.IsPassable(WalkMask), radius).ToList();
    }
    // Puts a roster robot on the map as a following ally (mission start and Drop Pods).
    public static Monster SpawnRobot(State state, RobotRoster roster, RobotRecord record, CellPosition position, Action committed = null)
    {
        var creatures = state.Get<Creatures>();
        var data = record.Data;
        RobotUpgrades.SyncHealth(data);
        data.Position = position;
        data.UniqueId = state.Get<RaidMetadata>().NextIdCounter;
        data.CreatureAlliance = CreatureAlliance.PlayerAlliance;
        var monster = CreatureSystem.SpawnMonster(creatures, state.Get<TurnController>(), data);
        // Commit as soon as the scene owns the data, before optional AI setup.
        roster.DeployedIds.Add(record.Identity.Id);
        committed?.Invoke();
        monster.IsTransferable = true;
        monster.Behaviour.StartFollowing(creatures.Player);
        CreatureSystem.RefreshResists(data, creatures.Player.Mercenary);
        return monster;
    }
    // Rapid Response Unit doubles repair speed: half the hours, rounded up.
    public static int RepairTime(int hours, bool rapidResponse) => rapidResponse ? (hours + 1) / 2 : hours;
    public static int RepairHours(CreatureData data, int healthHours, int woundHours, int amputationHours, bool destroyed = false)
    {
        int hours = data.Health.IsFull && !destroyed ? 0 : healthHours;
        foreach (var wound in data.EffectsController.OfType<BodyPartWound>())
            hours += wound.IsAmputation ? amputationHours : woundHours;
        return Math.Max(0, hours);
    }
    // salvage: Salvage Drones owned. Without them a destroyed robot stays
    // broken where it fell and leaves the roster; living robots still return.
    public static void Complete(RobotRoster roster, bool success, bool salvage, DateTime now, Action<RobotRecord, DateTime> repair)
    {
        foreach (var record in roster.Robots.Where(r => roster.DeployedIds.Contains(r.Identity.Id)).ToList())
        {
            if (success && (salvage || !record.Destroyed)) repair(record, now);
            else roster.Robots.Remove(record);
        }
        roster.DeployedIds.Clear();
        roster.PendingSpawnIds.Clear();
        roster.SelectedIds.Clear();
    }
    public static void Repair(RobotRecord record, DateTime now)
    {
        int hours = RepairHours(record.Data, Data.Global.RestoreMercHealthHours,
            Data.Global.RestoreMercWoundInHours, Data.Global.RestoreMercAmpInHours, record.Destroyed);
        hours = RepairTime(hours, RobotUpgrades.Owns(RobotUpgrades.RapidResponseUnit));
        // Detach from discarded scene objects and callbacks. Restore the chassis,
        // retaining its current inventory/augment state rather than generating a new robot.
        var restored = new CreatureData();
        restored.LoadJSON(SaveToJSON.CreateNode(record.Data));
        if (record.Destroyed || record.Data.Health.Dead) RetainDestroyedEquipment(restored.Inventory);
        RemoveAmputatedParts(restored);
        restored.EffectsController.Clear();
        restored.EffectsController.Add(new StarvationEffect(restored.StarvationLimit));
        restored.EffectsController.Add(new PainThreshold(restored.PainThresholdLimit, restored.PainThresholdRegen));
        restored.Health = new HealthInfo(Math.Max(1, restored.BaseHealth + restored.PerksMaxHealth));
        restored.MovementState = CreatureMovementState.Normal;
        restored.CreatureAlliance = CreatureAlliance.PlayerAlliance;
        var proxy = new RobotRecord { Data = restored }.CreateProxy();
        AccessTools.Method(typeof(MercenarySystem), "UnlockAllItems").Invoke(null, new object[] { proxy });
        AccessTools.Method(typeof(MercenarySystem), "UnblockAllSlots").Invoke(null, new object[] { proxy });
        AugmentationSystem.RestoreDefaultWoundSlots(proxy);
        AugmentationSystem.ConfigureImplicitEffects(restored);
        AugmentationSystem.RechargeAllImplants(restored);
        CreatureSystem.SetBareHandSlot(restored);
        CreatureSystem.SetArmStumpSlot(restored);
        record.Data = restored;
        RobotUpgrades.SyncHealth(restored);
        record.RepairStart = now;
        record.RepairEnd = now.AddHours(hours);
        record.Destroyed = false;
    }
    public static void RemoveAmputatedParts(CreatureData data)
    {
        foreach (var wound in data.EffectsController.OfType<BodyPartWound>().Where(w => w.IsAmputation).ToList())
        {
            var slots = data.AugmentationMap.TryGetValue(wound.WoundSlotId, out var augmentation)
                ? data.AugmentationMap.Where(kv => kv.Value == augmentation).Select(kv => kv.Key).ToList()
                : new List<string> { wound.WoundSlotId };
            foreach (var slot in slots)
            {
                data.WoundSlotMap.Remove(slot);
                data.AugmentationMap.Remove(slot);
            }
        }
        PruneRemovedImplants(data);
    }
    private static void PruneRemovedImplants(CreatureData data)
    {
        var retained = new HashSet<string>(data.WoundSlotMap.Values.SelectMany(s => s.InstalledImplantsData).Select(i => i.ImplantId));
        foreach (var id in data.ImplantActivesMap.Keys.Where(id => !retained.Contains(id)).ToList())
            data.ImplantActivesMap.Remove(id);
    }
    public static void RetainDestroyedEquipment(Inventory inventory)
    {
        // Native corpse edits stay authoritative. Equipment slots and the vest
        // survive recovery; backpack/other loose storage must be evacuated normally.
        foreach (var storage in inventory.Storages)
        {
            if (ReferenceEquals(storage, inventory.VestStore)) continue;
            foreach (var item in storage.Items.ToArray())
            {
                storage.Remove(item);
                item.Storage = null;
            }
        }
    }
    public static Inventory TransferCorpseInventory(Inventory source)
    {
        // Separate inventory callbacks from the dying scene actor, while retaining
        // the complete slot layout. Ownership moves only after serialization succeeds.
        var destination = new Inventory();
        destination.LoadJSON(SaveToJSON.CreateNode(source));
        foreach (var storage in source.AllContainers)
            foreach (var item in storage.Items.ToArray())
            {
                storage.Remove(item, sendEvent: false);
                item.Storage = null;
            }
        return destination;
    }
}

[HarmonyPatch(typeof(SpaceGameMode), "PrepareRaidScreenOnStartRaid")]
public static class RobotDepartureStartPatch
{
    [HarmonyPrefix] public static void Prefix(SpaceGameMode __instance)
    {
        if (RobotRosterView.Field<bool>(__instance, "_finished")) return;
        RobotDeployment.Begin(RobotRoster.For(__instance.Get<Mercenaries>()), __instance.Get<SpaceTime>().Time);
    }
}
[HarmonyPatch(typeof(DungeonBuilder), "TransferMonsters")]
public static class RobotDepartureSpawnPatch
{
    [HarmonyPostfix] public static void Postfix(DungeonBuilder __instance)
    {
        try { RobotDeployment.Spawn(RobotRosterView.Field<State>(__instance, "_state")); }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
}
[HarmonyPatch(typeof(ComponentsLayout), nameof(ComponentsLayout.SerializeGlobalComponents))]
public static class RobotMissionSavePatch
{
    [HarmonyPrefix] public static void Prefix(ComponentsLayout __instance) =>
        RobotDeployment.Sync(RobotRosterView.Field<State>(__instance, "_state"));
}
[HarmonyPatch(typeof(DungeonGameMode), "FinishGame")]
public static class RobotMissionFinishSyncPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.First)] public static void Prefix(DungeonGameMode __instance) =>
        RobotDeployment.Sync(RobotRosterView.Field<State>(__instance, "_state"));
}
[HarmonyPatch(typeof(MissionSystem), nameof(MissionSystem.ProcessFinishedDungeonData))]
public static class RobotMissionResultPatch
{
    [HarmonyPrefix] public static void Prefix(Mercenaries mercenaries, RaidMetadata raidMetadata, DungeonFinishedData finishedData, out bool __state) =>
        __state = Returns(finishedData.Reason, raidMetadata.WinCondition.WinConditionsPassed,
            mercenaries?.MercenaryInRaid?.CreatureData?.Health?.Alive ?? false);
    // Robots come home when the mission is won, or when the operator gets out
    // alive: evacuating early or leaving a Bramfatura through a portal counts
    // as MissionFinished without the win condition passed.
    public static bool Returns(GameFinishedReason reason, bool won, bool operatorAlive) =>
        reason == GameFinishedReason.LeaveStation || (reason == GameFinishedReason.MissionFinished && (won || operatorAlive));
    [HarmonyPostfix] public static void Postfix(Mercenaries mercenaries, MagnumProgression magnumSpaceship, SpaceTime spaceTime, bool __state) =>
        RobotDeployment.Complete(RobotRoster.For(mercenaries), __state, RoboticsModule.HasSalvageDrones(magnumSpaceship), spaceTime.Time, RobotDeployment.Repair);
}
[HarmonyPatch(typeof(MercenarySystem), nameof(MercenarySystem.CloneMercenariesInRaid))]
public static class RobotMissionFallbackPatch
{
    [HarmonyPostfix] public static void Postfix(Mercenaries mercenaries, SpaceTime spaceTime) =>
        RobotDeployment.Complete(RobotRoster.For(mercenaries), false, false, spaceTime.Time, RobotDeployment.Repair);
}


// Keep equipment and vest positions on roster corpses. The native monster clone
// collapses every storage into the backpack and disables vests, losing that distinction.
[HarmonyPatch(typeof(Monster), nameof(Monster.CloneInventoryForCorpse))]
public static class RobotCorpseInventoryPatch
{
    [HarmonyPrefix] public static bool Prefix(Monster __instance, ref Inventory __result)
    {
        if (RobotIdentity.Find(__instance.CreatureData) == null) return true;
        __result = RobotDeployment.TransferCorpseInventory(__instance.CreatureData.Inventory);
        foreach (var slot in __result.Slots)
            if (!slot.Empty)
                slot.First.Comp<BreakableItemComponent>()?.MultiplyCondition(
                    RobotRosterView.Field<Difficulty>(__instance, "_difficulty").Preset.KilledMobsItemsCondition);
        return false;
    }
}
[HarmonyPatch(typeof(CreatureSystem), nameof(CreatureSystem.GenerateCorpse))]
public static class RobotCorpseIdentityPatch
{
    [HarmonyPostfix] public static void Postfix(Creature killedMonster, CreatureData __result)
    {
        var identity = RobotIdentity.Find(killedMonster.CreatureData);
        if (identity == null) return;
        RobotIdentity.Bind(__result, identity);
        var roster = RobotRoster.For(RobotRosterView.Field<Mercenaries>(killedMonster, "_mercenaries"));
        RobotDeployment.TrackCorpse(roster, __result);
    }
}

[HarmonyPatch(typeof(ItemInteractionSystem), nameof(ItemInteractionSystem.UseResurrectKit))]
public static class RobotResurrectionPatch
{
    // The game revives into a fresh body with its default Fist, then pours the
    // corpse's items into it one by one, which drops the vest and backpack out of
    // their slots. A roster robot keeps its own inventory instead: the game is
    // handed an empty one to empty out, and the robot's is put back afterwards.
    private static Inventory keptInventory;

    [HarmonyPrefix] public static void Prefix(Mercenaries mercenaries, MapObstacles mapObstacles, BasePickupItem item, MapCell cell, out CreatureData __state)
    {
        keptInventory = null;
        var device = item.Record<PlaceableDeviceRecord>();
        __state = device == null ? null : ItemInteractionSystem.TakeValidCorpse(mapObstacles, device, cell.Position)?.CorpseStorage?.CreatureData;
        if (__state?.Inventory == null || RobotDeployment.FindCorpseRobot(RobotRoster.For(mercenaries), __state) == null) return;
        keptInventory = __state.Inventory;
        __state.Inventory = new Inventory();
    }
    // creature is an out parameter: it must be taken by ref to be read here.
    [HarmonyPostfix] public static void Postfix(Mercenaries mercenaries, Creatures creatures, ref Monster creature,
        ItemInteractionSystem.ResurrectResult __result, CreatureData __state)
    {
        var inventory = keptInventory;
        keptInventory = null;
        if (__result != ItemInteractionSystem.ResurrectResult.Success || creature == null || __state == null)
        {
            // Failed or blocked: the corpse keeps its items.
            if (inventory != null && __state != null) __state.Inventory = inventory;
            return;
        }
        var roster = RobotRoster.For(mercenaries);
        var record = RobotDeployment.FindCorpseRobot(roster, __state);
        if (record == null) return;
        var identity = record.Identity;
        var data = creature.CreatureData;
        if (inventory != null) data.Inventory = inventory;
        RobotIdentity.Bind(data, identity);
        data.WoundSlotMap = __state.WoundSlotMap;
        data.AugmentationMap = __state.AugmentationMap;
        data.ImplantActivesMap = record.Data.ImplantActivesMap;
        AugmentationSystem.ConfigureImplicitEffects(data);
        creature.ConfigureData(data);
        creature.Creature3dView.ConfigureInventory(data.Inventory);
        creature.Creature3dView.RefreshItemsRender();
        creature.IsTransferable = true;
        creature.Behaviour.StartFollowing(creatures.Player);
        RobotUpgrades.SyncHealth(data);
        creature.ActionPoints = data.BaseActionPoints + RobotUpgrades.Current.ActionPoints;
        CreatureSystem.RefreshResists(data, creatures.Player.Mercenary);
        RobotDeployment.Track(roster, data);
        Plugin.Logger.Log("Reanimatron revived roster robot " + record.DisplayName + ".");
    }
}
