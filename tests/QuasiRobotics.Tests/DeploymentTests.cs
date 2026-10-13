using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using HarmonyLib;
using MGSC;
using QuasiRobotics;
using QuasiRobotics.Patches;

partial class Program
{
    static RobotRecord DeploymentRobot(string id)
    {
        var data=TestRobot("cyborg_battle");
        data.BaseHealth=75; data.Health=new HealthInfo(75); data.BodyTypeId="robot_body";
        RobotIdentity.Bind(data,new RobotIdentity {Id=id,Name=id,DefaultLocalizationId="cyborg_battle",Rank=TestRank()});
        return new RobotRecord {Data=data};
    }
    static bool SkipArmStumpForRepairFixture() => false;
    static void DeploymentTests(Harmony harmony)
    {
        var now=new DateTime(2200,1,1);
        var owner=new Mercenaries(); var roster=RobotRoster.For(owner);
        var a=DeploymentRobot("a"); var b=DeploymentRobot("b"); var c=DeploymentRobot("c");
        roster.Robots.AddRange(new[]{a,b,c});
        Check(RobotDeployment.Select(roster,a,now),"select registered robot for departure");
        Check(!RobotDeployment.Select(roster,a,now),"same robot cannot be selected twice");
        Check(!RobotDeployment.Select(roster,DeploymentRobot("outsider"),now),"departure rejects robots outside this campaign roster");
        Check(RobotDeployment.Select(roster,b,now) && !RobotDeployment.Select(roster,c,now),"departure obeys capacity even in legacy overfilled roster");
        roster.SelectedIds.Remove(a.Identity.Id);
        Check(roster.Robots.Count==3 && RobotDeployment.Select(roster,c,now),"removing departure selection leaves roster ownership intact");
        c.RepairStart=now; c.RepairEnd=now.AddHours(24);
        RobotDeployment.Begin(roster,now);
        Check(roster.PendingSpawnIds.SequenceEqual(new[]{"b"}) && roster.SelectedIds.Count==0,"departure revalidates repair readiness before spawning");
        RobotDeployment.Begin(roster,now);
        Check(roster.PendingSpawnIds.Count==1,"repeated departure callback cannot queue duplicates");
        roster.PendingSpawnIds.Clear(); roster.DeployedIds.Add("b");
        Check(!RobotDeployment.Select(roster,a,now),"cannot add a second deployment during an active mission");
        b.Data.Inventory=new Inventory(4,4,3,Inventory.BackpackMode.Normal,true);
        AccessTools.Property(typeof(ItemStorage),nameof(ItemStorage.WorkMode)).SetValue(b.Data.Inventory.VestStore,ItemStorageWorkMode.Normal);
        AccessTools.Property(typeof(ItemStorage),nameof(ItemStorage.ValidItemType)).SetValue(b.Data.Inventory.HelmetSlot,new WrappedType(null));
        var pack=Item("contents_test",4); b.Data.Inventory.BackpackStore.AddItemAndReshuffleOptional(pack);
        var vest=Item("contents_test",2); b.Data.Inventory.VestStore.AddItemAndReshuffleOptional(vest);
        var worn=Item("contents_test",1); b.Data.Inventory.HelmetSlot.AddItemAndReshuffleOptional(worn);
        var serialized=SaveToJSON.CreateNode(owner);
        var restoredOwner=new Mercenaries(); restoredOwner.LoadJSON(serialized["Content"]);
        var restored=RobotRoster.For(restoredOwner);
        Check(restored.DeployedIds.SequenceEqual(new[]{"b"}) && restored.Robots.Count==3,"mission deployment IDs and roster survive native save/load");
        var loadedLive=new CreatureData(); loadedLive.LoadJSON(SaveToJSON.CreateNode(b.Data));
        RobotDeployment.Track(restored,loadedLive);
        Check(ReferenceEquals(restored.Robots[1].Data,loadedLive),"loaded mission creature becomes authoritative instead of saved roster copy");
        loadedLive.Inventory.BackpackStore.Items[0].StackCount=3;
        Check(restored.Robots[1].Data.Inventory.BackpackStore.Items[0].StackCount==3,"in-mission inventory edits update roster ownership");
        var corpse=new CreatureData(); corpse.LoadJSON(SaveToJSON.CreateNode(loadedLive));
        corpse.Health=new HealthInfo(75); corpse.BaseHealth=999; // native corpse health is full; stats are incomplete
        corpse.Inventory.HelmetSlot.Remove(corpse.Inventory.HelmetSlot.First);
        corpse.Inventory.VestStore.First.StackCount=1;
        RobotDeployment.TrackCorpse(restored,corpse);
        Check(restored.Robots[1].Destroyed && restored.Robots[1].Data.BaseHealth==75,
            "corpse tracking records destruction without replacing chassis stats with native corpse stats");
        Check(ReferenceEquals(restored.Robots[1].Data.Inventory,corpse.Inventory) && restored.Robots[1].Data.Inventory.HelmetSlot.Empty,
            "looted corpse equipment stays removed from recovery inventory");
        corpse.Inventory.VestStore.First.StackCount=2;
        Check(restored.Robots[1].Data.Inventory.VestStore.First.StackCount==2,"latest corpse edits remain live after tracking");
        var snapshot=SaveToJSON.CreateNode(restoredOwner); var resumedOwner=new Mercenaries(); resumedOwner.LoadJSON(snapshot["Content"]);
        var resumed=RobotRoster.For(resumedOwner);
        Check(resumed.Robots[1].Destroyed && resumed.Robots[1].Data.Inventory.VestStore.First.StackCount==2,
            "destroyed status and latest corpse contents survive save/load");
        var retained=resumed.Robots[1].Data.Inventory;
        RobotDeployment.RetainDestroyedEquipment(retained);
        Check(retained.BackpackStore.Empty && retained.VestStore.First.StackCount==2 && retained.HelmetSlot.Empty,
            "destroyed recovery keeps remaining vest contents and does not restore looted equipment or backpack contents");
        RobotDeployment.RetainDestroyedEquipment(b.Data.Inventory);
        Check(b.Data.Inventory.HelmetSlot.First==worn && b.Data.Inventory.VestStore.First==vest && pack.Storage==null,
            "destroyed recovery preserves equipped item identity and detaches discarded backpack items");
        var sourceInventory=b.Data.Inventory;
        var corpseInventory=RobotDeployment.TransferCorpseInventory(sourceInventory);
        Check(sourceInventory.AllContainers.All(s=>s.Empty) && corpseInventory.HelmetSlot.First!=null && corpseInventory.VestStore.First.StackCount==2,
            "corpse transfer preserves equipment and vest separately with no inventory left on dying actor");
        Check(corpseInventory.HelmetSlot.First.Storage==corpseInventory.HelmetSlot && corpseInventory.CanHaveVest,
            "corpse transfer restores item ownership and vest support");
        int returned=0;
        RobotDeployment.Complete(resumed,true,true,now,(r,t)=>{returned++;r.RepairStart=t;r.RepairEnd=t.AddHours(24);r.Destroyed=false;});
        Check(returned==1 && resumed.Robots.Count==3 && resumed.DeployedIds.Count==0,"success returns destroyed and left-behind deployments without consuming a slot");
        Check(!RobotDeployment.Select(resumed,resumed.Robots[1],now.AddHours(23)),"repairing robots cannot deploy early");
        Check(RobotDeployment.Select(resumed,resumed.Robots[1],now.AddHours(24)),"repairing robots become available exactly when campaign timer ends");
        resumed.SelectedIds.Clear(); resumed.DeployedIds.AddRange(new[]{"a","b"}); resumed.Robots[0].Destroyed=false; resumed.Robots[1].Destroyed=true;
        RobotDeployment.Complete(resumed,false,true,now,(r,t)=>throw new Exception("loss must not repair"));
        Check(resumed.Robots.Count==1 && resumed.Robots[0].Identity.Id=="c" && resumed.EmptySlots==1,
            "mission loss removes every deployed robot, living or destroyed, and preserves robots aboard");
        RobotDeployment.Complete(resumed,false,true,now,(r,t)=>{});
        Check(resumed.Robots.Count==1,"mission completion is idempotent");
        var noSalvage=new RobotRoster(); var wreck=DeploymentRobot("wreck"); var walker=DeploymentRobot("walker");
        wreck.Destroyed=true; noSalvage.Robots.AddRange(new[]{wreck,walker}); noSalvage.DeployedIds.AddRange(new[]{"wreck","walker"});
        var repaired=new List<RobotRecord>();
        RobotDeployment.Complete(noSalvage,true,false,now,(r,t)=>repaired.Add(r));
        Check(!noSalvage.Robots.Contains(wreck) && noSalvage.Robots.Contains(walker) && repaired.Count==1 && repaired[0]==walker,
            "without Salvage Drones destroyed robots stay broken and leave the roster; living robots return");
        var timerSave=SaveToJSON.CreateNode(c); var timerLoad=new RobotRecord();timerLoad.LoadJSON(timerSave);
        Check(timerLoad.IsRepairing(now) && !timerLoad.IsRepairing(now.AddHours(24)),"repair timer survives native save/load");
        var damaged=DeploymentRobot("repair").Data;
        Check(RobotDeployment.RepairHours(damaged,6,12,24)==0,"undamaged robot has no repair delay");
        Check(RobotDeployment.RepairHours(damaged,6,12,24,true)==6,"destroyed flag requires repairs even with native corpse full HP");
        damaged.Health=new HealthInfo(0); damaged.Health.MaxValue=75;
        var wound=new BodyPartWound(); var amputation=new BodyPartWound();
        AccessTools.Property(typeof(BodyPartWound),nameof(BodyPartWound.WoundCategory)).SetValue(wound,WoundCategory.Normal);
        AccessTools.Property(typeof(BodyPartWound),nameof(BodyPartWound.WoundCategory)).SetValue(amputation,WoundCategory.Amputation);
        damaged.EffectsController.Effects.AddRange(new[]{wound,amputation});
        Check(RobotDeployment.RepairHours(damaged,6,12,24)==42,"repair duration matches native health plus per-wound and per-amputation costs");
        AccessTools.Property(typeof(BodyPartWound),nameof(BodyPartWound.WoundSlotId)).SetValue(amputation,"UpgradedArm");
        damaged.WoundSlotMap["UpgradedArm"]=new ImplantSocketData();
        damaged.WoundSlotMap["RobotHead"]=new ImplantSocketData();
        damaged.AugmentationMap["UpgradedArm"]="lost_arm";
        damaged.ImplantActivesMap["removed_implant"]=new Perk();
        RobotDeployment.RemoveAmputatedParts(damaged);
        Check(!damaged.WoundSlotMap.ContainsKey("UpgradedArm") && !damaged.AugmentationMap.ContainsKey("UpgradedArm") && damaged.WoundSlotMap.ContainsKey("RobotHead") && damaged.ImplantActivesMap.Count==0,
            "recovery cannot recreate amputated augments or retain removed implant abilities");
        var metadata=new RaidMetadata {WinCondition=new MissionWinCondition{WinConditionsPassed=true}};
        RobotMissionResultPatch.Prefix(null,metadata,new DungeonFinishedData{Reason=GameFinishedReason.MissionFinished},out bool won);
        Check(won,"mission win uses captured native objective completion");
        metadata.WinCondition.WinConditionsPassed=false;
        RobotMissionResultPatch.Prefix(null,metadata,new DungeonFinishedData{Reason=GameFinishedReason.MissionFinished},out won);
        Check(!won,"evacuation without objective completion and no live operator counts as loss");
        RobotMissionResultPatch.Prefix(null,metadata,new DungeonFinishedData{Reason=GameFinishedReason.PlayerDead},out won);
        Check(!won,"operator death counts as loss");
        RobotMissionResultPatch.Prefix(null,metadata,new DungeonFinishedData{Reason=GameFinishedReason.LeaveStation},out won);
        Check(won,"peaceful station departure returns robots");
        // Exercise the real return/repair implementation with native effect and body records.
        AccessTools.Field(typeof(EffectFactory), "_state").SetValue(Singleton<EffectFactory>.Instance,new State());
        var global=new GlobalSettings {RestoreMercHealthHours=6,RestoreMercWoundInHours=12,RestoreMercAmpInHours=24};
        AccessTools.Property(typeof(Data),"Global").SetValue(null,global);
        foreach(var slot in Data.WoundSlots.Records)
        {
            slot.ImplicitBonusEffects=new Dictionary<string,float>();
            slot.ImplicitPenaltyEffects=new Dictionary<string,float>();
        }
        var mobs=new ConfigRecordCollection<MobClassRecord>();
        mobs.AddRecord("cyborg_battle",new MobClassRecord {BodyTypes=new List<string>{"robot_body"}});
        AccessTools.Property(typeof(Data),"MobClasses").SetValue(null,mobs);
        // Arm-stump creation uses the Unity item factory; its unrelated item generation is tested elsewhere.
        harmony.Patch(AccessTools.Method(typeof(CreatureSystem),nameof(CreatureSystem.SetArmStumpSlot)),
            prefix:new HarmonyMethod(typeof(Program),nameof(SkipArmStumpForRepairFixture)));
        var returning=DeploymentRobot("actual-repair"); returning.Destroyed=true;
        returning.Data.Health=new HealthInfo(0);returning.Data.Health.MaxValue=75;
        returning.Data.Inventory.BackpackStore.AddItemAndReshuffleOptional(Item("contents_test",5));
        var beforeRepair=returning.Data;
        RobotDeployment.Repair(returning,now);
        Check(returning.Data.Health.Alive && returning.Data.Health.IsFull && returning.RepairEnd==now.AddHours(6) && !returning.Destroyed,
            "real repair implementation revives destroyed chassis and starts its campaign timer");
        Check(!ReferenceEquals(beforeRepair,returning.Data) && returning.Data.Inventory.BackpackStore.Empty && beforeRepair.Inventory.BackpackStore.Items.Count==1,
            "real recovery detaches from scene data and only trims returned copy");
        Check(returning.Data.WoundSlotMap.ContainsKey("RobotHead") && !returning.Data.WoundSlotMap.ContainsKey("OrganicHead") && returning.Identity.Id=="actual-repair",
            "real recovery restores robotic defaults and preserves roster identity");
        harmony.Unpatch(AccessTools.Method(typeof(CreatureSystem),nameof(CreatureSystem.SetArmStumpSlot)),
            AccessTools.Method(typeof(Program),nameof(SkipArmStumpForRepairFixture)));
        // Verify persistence hook uses the real State and follows loaded mission data.
        var state=new State(); state.RegisterInState(restoredOwner); state.RegisterInState(new MapObstacles());
        var creatures=new Creatures(); state.RegisterInState(creatures); state.RegisterInState(new MonsterTransfer());
        var monster=(Monster)FormatterServices.GetUninitializedObject(typeof(Monster));
        monster.CreatureData=loadedLive;
        creatures.Monsters.Add(monster);
        RobotDeployment.Sync(state);
        Check(ReferenceEquals(restored.Robots[1].Data,loadedLive) && !restored.Robots[1].Destroyed,
            "living resurrected robot wins over a previously captured corpse");
        foreach(var type in new[]{typeof(RobotDepartureStartPatch),typeof(RobotDepartureSpawnPatch),typeof(RobotMissionSavePatch),
            typeof(RobotMissionFinishSyncPatch),typeof(RobotMissionResultPatch),typeof(RobotMissionFallbackPatch),
            typeof(RobotCorpseInventoryPatch),typeof(RobotCorpseIdentityPatch),typeof(RobotResurrectionPatch),
            typeof(RobotDepartureScreenPatch)})
        {
            harmony.CreateClassProcessor(type).Patch();
            Check(true,"Harmony compiles patch signature: "+type.Name);
        }
        Check(!(bool)AccessTools.Method(typeof(FinishGamePackPatch),"Pack").Invoke(null,new object[]{loadedLive}),
            "roster robot extraction cannot create a duplicate cargo package");
    }
}







