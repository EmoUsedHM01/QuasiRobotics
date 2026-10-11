using System;
using System.Collections.Generic;
using HarmonyLib;
using MGSC;
using QuasiRobotics;
using QuasiRobotics.Packages;
using QuasiRobotics.Patches;

partial class Program
{
    static CreatureData TestRobot(string mob) => new CreatureData {
        MobClassId=mob, LocalizationId=mob, Inventory=new Inventory(4,4,0,Inventory.BackpackMode.Endless,false),
        Health=new HealthInfo(0), EffectsController=new EffectsController(), Perks=new List<Perk>(),
        WoundSlotMap=new Dictionary<string,ImplantSocketData>(), Immunity=new List<string>()
    };
    static Perk TestRank() => new Perk { PerkId="rank_0", PerkType=PerkType.Rank, NextPerkId="rank_1",MaxExp=100 };
    static PickupItem RobotPackage(short count)
    {
        var item=Item("ar_test",count);
        var device=new PlaceableDeviceRecord { TargetObjectId="cyborg_battle" };
        AccessTools.Property(typeof(PlaceableDeviceRecord),"Usage").SetValue(device, Enum.Parse(AccessTools.Property(typeof(PlaceableDeviceRecord),"Usage").PropertyType,"AllySpawn"));
        item.Add(device,true);
        return item;
    }
    static void RobotTests(Harmony harmony)
    {
        foreach(var type in new[]{typeof(RobotSavePatch),typeof(RobotLoadPatch),typeof(RobotNamePatch),typeof(RobotRankPatch),typeof(RobotRankExpPatch),typeof(RobotLevelUpPatch)})
            harmony.CreateClassProcessor(type).Patch();
        var owner=new Mercenaries(); var roster=RobotRoster.For(owner);
        Check(roster.SlotCapacity==2 && roster.EmptySlots==2,"new rosters start with two empty slots");
        foreach (var id in new[]{"cyborg_worker", "cyborg_battle", "cyborg_recreation"})
            Check(RobotRoster.IsDroid(id), id+" is an eligible droid model");
        foreach (var id in new[]{"turret_ballistic", "turret_fire", "turret_energy", "quest_turret_emp", "flauros_pact_turret", "drone_security", "jungle_spider", "unknown", null})
        {
            var excluded=RobotPackage(1);
            excluded.Record<PlaceableDeviceRecord>().TargetObjectId=id;
            var excludedCargo=new ItemStorage(ItemStorageSource.ShipCargo,8,8);
            excludedCargo.AddItemAndReshuffleOptional(excluded);
            bool denied=false;
            try { roster.Register(excluded,TestRobot,TestRank); } catch(InvalidOperationException) { denied=true; }
            Check(!RobotRoster.CanRegister(excluded) && denied && excluded.Storage==excludedCargo && roster.Robots.Count==0,
                (id??"null")+" cannot appear in picker or be consumed by registration");
        }
        var cargo=new ItemStorage(ItemStorageSource.ShipCargo,8,8);
        var package=RobotPackage(2); cargo.AddItemAndReshuffleOptional(package);
        Check(RobotRoster.CanRegister(package),"stock robot package can register");
        var first=roster.Register(package,TestRobot,TestRank);
        Check(roster.EmptySlots==1,"registering first robot fills one slot");
        Check(package.StackCount==1 && cargo.Items.Contains(package),"registration consumes one stock package");
        var second=roster.Register(package,TestRobot,TestRank);
        Check(package.Storage==null && !cargo.Items.Contains(package),"last package removed from cargo");
        Check(roster.Robots.Count==2 && owner.Values.Count==0,"robot records never enter operator roster");
        Check(first.Identity.Id!=second.Identity.Id && !ReferenceEquals(first.Data.Inventory,second.Data.Inventory),"same robot model creates independent records");
        bool rejected=false; try{roster.Register(package,TestRobot,TestRank);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && roster.Robots.Count==2,"consumed package cannot register twice");
        var failed=RobotPackage(1); cargo.AddItemAndReshuffleOptional(failed);
        bool generated=false;
        rejected=false; try{roster.Register(failed,id=>{generated=true; return TestRobot(id);},TestRank);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && !generated && roster.EmptySlots==0 && failed.Storage==cargo && failed.StackCount==1,
            "full roster rejects registration before generation or package consumption");
        roster.SlotCapacity=4;
        Check(roster.EmptySlots==2,"capacity upgrade exposes additional empty slots");
        rejected=false; try{roster.Register(failed,id=>throw new Exception("fixture"),TestRank);}catch(Exception){rejected=true;}
        Check(rejected && failed.Storage==cargo && failed.StackCount==1 && roster.Robots.Count==2,"generation failure preserves package and roster");
        rejected=false; try{roster.Register(failed,TestRobot,()=>new Perk{PerkId="rank_2"});}catch(InvalidOperationException){rejected=true;}
        Check(rejected && cargo.Items.Contains(failed) && roster.Robots.Count==2,"invalid rank cannot consume package");
        var recovered=RobotPackage(1); cargo.AddItemAndReshuffleOptional(recovered);
        var inventory=new Inventory(6,5,4,Inventory.BackpackMode.Normal,true);
        var content=Item("contents_test",9); inventory.BackpackStore.AddItemAndReshuffleOptional(content);
        var badge=new ArLoadoutComponent{MobClassId="cyborg_battle",Inventory=inventory};
        badge.Augmentations["arm"]="custom_arm";
        recovered.Add(badge);
        var recoveredRobot=roster.Register(recovered,TestRobot,TestRank);
        Check(ReferenceEquals(recoveredRobot.Data.Inventory,inventory) && content.Storage==inventory.BackpackStore,"recovered inventory transferred with item ownership intact");
        Check(badge.Inventory==null && recovered.Storage==null,"consumed badge retains no duplicate inventory");
        Check(recoveredRobot.Data.AugmentationMap["arm"]=="custom_arm","recovered augments preserved");
        first.Rename("  Atlas  "); second.Rename("Atlas");
        Check(first.DisplayName=="Atlas" && first.Identity.Id!=second.Identity.Id,"duplicate display names do not merge identities");
        Check(Localization.Get("monster."+first.Data.LocalizationId+".name")=="Atlas","mission name lookup resolves renamed robot");
        Check(first.Data.RankPerk.PerkId=="rank_0" && !first.Data.RankPerk.AddExp(500) && first.Data.RankPerk.CurrentExp==0,"robot rank cannot gain experience");
        PerkSystem.LevelUpPerk(new Mercenary {CreatureData=first.Data},first.Data.RankPerk,null);
        Check(first.Data.RankPerk.PerkId=="rank_0","direct native rank promotion blocked");
        var ordinaryRank=TestRank();
        Check(!RobotIdentity.IsFixedRank(ordinaryRank),"ordinary operator ranks are not locked");
        recoveredRobot.Data.Inventory.BackpackStore.Items[0].StackCount=13;
        recoveredRobot.Data.AugmentationMap["arm"]="edited_arm";
        var json=SaveToJSON.CreateNode(owner);
        var loaded=new Mercenaries(); loaded.LoadJSON(json["Content"]);
        var restored=RobotRoster.For(loaded);
        Check(restored.SlotCapacity==4 && restored.EmptySlots==1,"upgraded capacity and occupied slots survive save/load");
        Check(restored.Robots.Count==3 && loaded.Values.Count==0,"roster round-trips in campaign save separately from operators");
        Check(restored.Robots[0].Identity.Id==first.Identity.Id && restored.Robots[0].DisplayName=="Atlas","identity and name survive save/load");
        Check(restored.Robots[2].Data.Inventory.BackpackStore.Items[0].StackCount==13 && restored.Robots[2].Data.AugmentationMap["arm"]=="edited_arm","live equipment and augment edits survive save/load");
        Check(restored.Robots[2].Data.Inventory.BackpackStore.Items[0].Storage==restored.Robots[2].Data.Inventory.BackpackStore,"loaded robot inventory ownership restored");
        Check(!restored.Robots[0].Data.RankPerk.AddExp(500) && restored.Robots[0].Data.RankPerk.PerkId=="rank_0","loaded robot rank remains locked");
        var creatureJson=SaveToJSON.CreateNode(first.Data);
        var missionData=new CreatureData(); missionData.LoadJSON(creatureJson);
        Check(RobotIdentity.Find(missionData).Id==first.Identity.Id && Localization.Get("monster."+missionData.LocalizationId+".name")=="Atlas","standalone mission creature save retains name and identity");
        Check(RobotRoster.For(new Mercenaries()).Robots.Count==0,"new campaigns do not inherit another roster");
        loaded.LoadJSON(new SimpleJSON.JSONObject());
        Check(RobotRoster.For(loaded).Robots.Count==0,"loading legacy save clears previous roster");
        Check(RobotIdentity.CleanName("  <b>A\nB</b>  ")=="bAB/b" && RobotIdentity.CleanName(new string('x',80)).Length==48,"names cannot inject rich text or unbounded layout");
        Check(RobotEditorCommandPatch.IsProgressionCommand((int)ContextMenuCommand.ApplySkull),"pact command blocked for robot editors");
        var legacyNode=SaveToJSON.CreateNode(roster);
        legacyNode.Remove("SlotCapacity");
        var legacyRoster=new RobotRoster(); legacyRoster.LoadJSON(legacyNode);
        Check(legacyRoster.SlotCapacity==2 && legacyRoster.Robots.Count==3 && legacyRoster.EmptySlots==0,
            "pre-slot saves retain all existing robots and default to two slots without permitting extras");
        RobotDismantleTests();
        var droidPackage=RobotPackage(1);
        var turretPackage=RobotPackage(1); turretPackage.Record<PlaceableDeviceRecord>().TargetObjectId="turret_ballistic";
        Check(BlockDroidPackageUsePatch.IsBlocked(true,droidPackage),"player cannot place a droid package in a mission");
        Check(!BlockDroidPackageUsePatch.IsBlocked(false,droidPackage),"enemy deployers can still place droid packages");
        Check(!BlockDroidPackageUsePatch.IsBlocked(true,turretPackage) && !BlockDroidPackageUsePatch.IsBlocked(true,Item("ammo_test",1)),"turrets and other items stay usable");
        HideDroidPackageUsePatch.Prefix(null,out var noSlot);
        Check(noSlot==null,"context menu hook ignores empty slots");
        droidPackage.IsUseRestricted=true;
        HideDroidPackageUsePatch.Finalizer(droidPackage,null);
        Check(!droidPackage.IsUseRestricted,"temporary Use restriction is never left on a saved package");
        RobotBodyTests(harmony, first.Data);
    }
    static void RobotDismantleTests()
    {
        var roster=new RobotRoster();
        var cargo=new ItemStorage(ItemStorageSource.ShipCargo,8,8);
        var package=RobotPackage(2); cargo.AddItemAndReshuffleOptional(package);
        var robot=roster.Register(package,TestRobot,TestRank);
        var kept=roster.Register(package,TestRobot,TestRank);
        var gear=Item("gear_test",3); robot.Data.Inventory.BackpackStore.AddItemAndReshuffleOptional(gear);
        var arm=new PickupItem("arm_gun_test",1,1); var armRecord=new WeaponRecord{IsImplicit=true}; SetId(armRecord,"arm_gun_test"); arm.Add(armRecord,true);
        robot.Data.Inventory.BackpackStore.AddItemAndReshuffleOptional(arm);
        roster.SelectedIds.Add(robot.Identity.Id);
        string askedFor=null; BasePickupItem dismantled=null;
        var parts=new List<BasePickupItem>{Item("part_test",2)};
        bool rejected=false;
        try { roster.Dismantle(robot,mob=>null,p=>parts); } catch(InvalidOperationException) { rejected=true; }
        Check(rejected && roster.Robots.Contains(robot) && gear.Storage==robot.Data.Inventory.BackpackStore,"dismantle without a package changes nothing");
        var result=roster.Dismantle(robot,mob=>{askedFor=mob; return Item("ar_test",1);},p=>{dismantled=p; return parts;});
        Check(askedFor=="cyborg_battle" && dismantled!=null && dismantled.Id=="ar_test","dismantle breaks down the robot's own package");
        Check(result.Contains(gear) && gear.Storage==null && result.Contains(parts[0]),"dismantle returns gear and parts for cargo");
        Check(!result.Contains(arm),"implicit arm weapons are not handed out as loot");
        Check(!roster.Robots.Contains(robot) && roster.Robots.Contains(kept) && !roster.SelectedIds.Contains(robot.Identity.Id) && roster.EmptySlots==1,
            "dismantle frees only that robot's slot and departure selection");
        roster.DeployedIds.Add(kept.Identity.Id);
        rejected=false;
        try { roster.Dismantle(kept,mob=>Item("ar_test",1),p=>parts); } catch(InvalidOperationException) { rejected=true; }
        Check(rejected && roster.Robots.Contains(kept),"deployed robots cannot be dismantled");
        Check(ModText.TryGet(ModText.DismantleConfirm,out var text) && text.Contains("{0}") && Localization.Get(ModText.DismantleButton)=="Dismantle",
            "dismantle dialog text resolves through localization ("+ModText.CurrentLang+"; "+string.Join(" | ",ModText.LoadErrors)+")");
    }
    static void RobotBodyTests(Harmony harmony, CreatureData robot)
    {
        harmony.CreateClassProcessor(typeof(RobotRestoreBodyPatch)).Patch();
        var slots=new ConfigRecordCollection<WoundSlotRecord>();
        slots.AddRecord("OrganicHead",new WoundSlotRecord {SlotType="Head",ImplantSocketsDefault=0});
        slots.AddRecord("RobotHead",new WoundSlotRecord {SlotType="Head",ImplantSocketsDefault=1});
        slots.AddRecord("RobotArm",new WoundSlotRecord {SlotType="Arm",ImplantSocketsDefault=1});
        slots.AddRecord("UpgradedArm",new WoundSlotRecord {SlotType="Arm",ImplantSocketsDefault=3});
        var bodies=new ConfigRecordCollection<BodyTypeRecord>();
        bodies.AddRecord("robot_body",new BodyTypeRecord {WoundSlots=new List<string>{"RobotHead","RobotArm"}});
        var profiles=new ConfigRecordCollection<MercenaryProfileRecord>();
        profiles.AddRecord("operator_profile",new MercenaryProfileRecord {WoundSlots=new List<string>{"OrganicHead"}});
        AccessTools.Property(typeof(Data),"WoundSlots").SetValue(null,slots);
        AccessTools.Property(typeof(Data),"BodyTypes").SetValue(null,bodies);
        AccessTools.Property(typeof(Data),"MercenaryProfiles").SetValue(null,profiles);
        robot.BodyTypeId="robot_body";
        robot.WoundSlotMap.Clear();
        var upgraded=new ImplantSocketData {TotalSockets=3};
        robot.WoundSlotMap["UpgradedArm"]=upgraded;
        robot.AugmentationMap["UpgradedArm"]="installed_arm";
        var proxy=new Mercenary {CreatureData=robot,ProfileId="operator_profile"};
        AugmentationSystem.RestoreDefaultWoundSlots(proxy);
        Check(robot.WoundSlotMap.ContainsKey("RobotHead") && !robot.WoundSlotMap.ContainsKey("OrganicHead") && robot.WoundSlotMap["RobotHead"].TotalSockets==1,
            "robot restoration uses robotic body despite operator editor profile");
        Check(ReferenceEquals(robot.WoundSlotMap["UpgradedArm"],upgraded) && !robot.WoundSlotMap.ContainsKey("RobotArm"),
            "restoration preserves installed augmentation and sockets");
        robot.WoundSlotMap.Remove("UpgradedArm"); robot.AugmentationMap.Remove("UpgradedArm");
        AugmentationSystem.RestoreDefaultWoundSlots(proxy);
        Check(robot.WoundSlotMap.ContainsKey("RobotArm"),"removed arm augmentation restores base robotic arm");
        var human=TestRobot("human");
        AugmentationSystem.RestoreDefaultWoundSlots(new Mercenary {CreatureData=human,ProfileId="operator_profile"});
        Check(human.WoundSlotMap.ContainsKey("OrganicHead"),"ordinary operator restoration stays organic");
        Mercenary tooltipMerc=proxy; RobotRankTooltipPatch.Prefix(ref tooltipMerc);
        Check(tooltipMerc==null,"robot rank tooltip receives no rank owner");
        tooltipMerc=new Mercenary{CreatureData=human}; RobotRankTooltipPatch.Prefix(ref tooltipMerc);
        Check(tooltipMerc!=null,"ordinary operator rank tooltip remains available");
    }
}


