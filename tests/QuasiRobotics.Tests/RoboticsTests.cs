using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MGSC;
using QuasiRobotics;
using UnityEngine;

partial class Program
{
    static void RoboticsTests(Harmony harmony)
    {
        var perks=new ConfigRecordCollection<MagnumPerkRecord>();
        var scanner=new MagnumPerkRecord{ModuleId="navigation",Parents=new List<string>(),Modifiers=new List<MagnumParameterModifier>(),UpgradePrice=new List<string>()};
        SetId(scanner,RoboticsModule.IconSourcePerkId);
        perks.AddRecord(scanner.Id,scanner);
        RoboticsModule.RegisterPerk(perks);
        RoboticsModule.RegisterPerk(perks);
        var printyard=perks.GetRecord(RoboticsModule.PrintyardId);
        var salvage=perks.GetRecord(RoboticsModule.SalvageDronesId);
        Check(printyard!=null && salvage!=null && perks.Count==22,"Printyard, Salvage Drones, Drop Pods and 18 upgrades register once");
        Check(salvage.ModuleId=="robotics" && salvage.IsDepartment && salvage.Modifiers.Count==0 && salvage.Parents.SequenceEqual(new[]{RobotUpgrades.RedundancySystems,RobotUpgrades.ExpandedBays(2)})
            && salvage.PositionInGraph.X==2 && salvage.PositionInGraph.Y==1,"Salvage Drones is a passive at column 3, row 2, linked from Redundancy Systems and Expanded Bays II");
        Check(salvage.UpgradePrice.SequenceEqual(new[]{"construction_equipment","tool_container","tool_container","ledger_book"}),"Salvage Drones costs a Construction Drone, 2 Toolbox and a Ledger");
        Check(Localization.Get("mgperk.salvage_drones.name")=="Salvage Drones","Salvage Drones name resolves");
        Check(!new ModSettings().StartWithSalvageDrones,"Start with Salvage Drones defaults to off");
        Check(printyard.ModuleId=="robotics" && printyard.IsDepartment && printyard.Enabled,"Printyard is a Robotics room device");
        Check(printyard.Parents.Count==0 && printyard.PositionInGraph.X==0 && printyard.PositionInGraph.Y==0,"Printyard is the first, top-left upgrade");
        Check(printyard.UpgradePrice.SequenceEqual(new[]{"welding_machine_device","robot_parts_container","robot_resurrect_device"}),"Printyard costs a Welding Machine, Robot Parts and a Reanimatron");
        Check(printyard.Modifiers!=null && printyard.Modifiers.Count==0,"Printyard has no stat modifiers to break native tooltips");
        var anchor=RoboticsModule.AnchorPosition(new Vector3(385,315,0),new Vector3(745,510,0),new Vector3(1113,362,0));
        Check(Mathf.Abs(anchor.x-320)<2 && Mathf.Abs(anchor.y-505)<2,"Robotics point lands on the marked spot of the ship view");
        // The six native rooms as seen on screen (bow left), y up.
        var rooms=new List<Vector3>{new Vector3(1190,-345,0),new Vector3(288,-445,0),new Vector3(1390,-493,0),new Vector3(648,-640,0),new Vector3(1195,-667,0),new Vector3(1015,-493,0)};
        foreach(var flip in new[]{1f,-1f})
        {
            var shown=rooms.Select(r=>new Vector3(r.x*flip,r.y,r.z)).ToList();
            var order=RoboticsModule.OrderFromBow(shown);
            var spot=RoboticsModule.AnchorPosition(shown[order[0]],shown[order[1]],shown[order[2]]);
            Check(order[0]==1 && order[1]==3 && order[2]==5 && Mathf.Abs(spot.x-224*flip)<5 && Mathf.Abs(spot.y+634)<5,
                "Robotics sits under the MAGNUM lettering "+(flip>0?"":"with the ship view mirrored"));
        }
        Check(Localization.Get("ui.magnum.module.robotics.name")=="Robotics" && Localization.Get("ui.magnum.module.robotics.shortdesc")=="HIS Droid R&D"
            && Localization.Get("mgperk.printyard.name")=="Printyard","Robotics room and Printyard names resolve");
        Check(!new ModSettings().StartWithPrintyard,"Start with Printyard defaults to off");
        Check(!new ModSettings().UninstallMode && !Uninstaller.Active,"Uninstall mode defaults to off");
        UninstallTests();
        UpgradeTests(harmony,perks);
    }
    static void UninstallTests()
    {
        var progression=new MagnumProgression();
        Check(!RoboticsModule.HasPrintyard(progression) && !RoboticsModule.HasPrintyard((MagnumProgression)null),"Robots stay locked without Printyard");
        progression.AddPerk(RoboticsModule.PrintyardId,silent:true);
        progression.AddPerk("navigation_scanner",silent:true);
        var departments=(List<MagnumDepartment>)AccessTools.Field(typeof(MagnumProgression),"_departments").GetValue(progression);
        foreach(var id in new[]{RoboticsModule.PrintyardId,"navigation_scanner"})
        { var d=new MagnumDepartment(); AccessTools.Field(typeof(MagnumDepartment),"_departmentId").SetValue(d,id); departments.Add(d); }
        Check(RoboticsModule.HasPrintyard(progression),"owning Printyard unlocks Robots");
        Check(Uninstaller.RemovePrintyard(progression) && !progression.IsPerkPurchased(RoboticsModule.PrintyardId)
            && progression.IsPerkPurchased("navigation_scanner") && departments.Count==1,"uninstall removes only Printyard and its department");
        Check(!Uninstaller.RemovePrintyard(progression),"Printyard is refunded only once");
        progression.AddPerk(RoboticsModule.SalvageDronesId,silent:true);
        Check(Uninstaller.RemoveDevices(progression).SequenceEqual(new[]{RoboticsModule.SalvageDronesId}) && !RoboticsModule.HasSalvageDrones(progression),
            "uninstall removes Salvage Drones too");
        Check(!RoboticsModule.GrantStartingDevice(progression,RoboticsModule.PrintyardId,false) && !RoboticsModule.HasPrintyard(progression),"loaded saves keep vanilla state when Start with Printyard is off");

        var roster=new RobotRoster();
        var cargo=new ItemStorage(ItemStorageSource.ShipCargo,8,8);
        var package=RobotPackage(2); cargo.AddItemAndReshuffleOptional(package);
        var robot=roster.Register(package,TestRobot,TestRank);
        roster.Register(package,TestRobot,TestRank);
        var gear=Item("gear_test",2); robot.Data.Inventory.BackpackStore.AddItemAndReshuffleOptional(gear);
        roster.SelectedIds.Add(robot.Identity.Id); roster.PendingSpawnIds.Add(robot.Identity.Id); roster.DeployedIds.Add(robot.Identity.Id);
        var items=Uninstaller.UnwindRoster(roster,mob=>Item("pkg_"+mob,1));
        Check(roster.Robots.Count==0 && roster.SelectedIds.Count==0 && roster.PendingSpawnIds.Count==0 && roster.DeployedIds.Count==0,"uninstall empties the roster");
        Check(items.Contains(gear) && items.Count(i=>i.Id=="pkg_cyborg_battle")==2,"each robot returns as a stock package with its gear");

        var recovered=RobotPackage(1); cargo.AddItemAndReshuffleOptional(recovered);
        var inventory=new Inventory(4,4,0,Inventory.BackpackMode.Endless,false);
        var stored=Item("stored_test",4); inventory.BackpackStore.AddItemAndReshuffleOptional(stored);
        recovered.Add(new QM_ArRecovery.Packages.ArLoadoutComponent{MobClassId="cyborg_battle",Inventory=inventory});
        var moved=new List<BasePickupItem>();
        Check(Uninstaller.StripStorages(new[]{cargo},moved)==1,"uninstall finds recovered packages in cargo, legacy ones too");
        Check(QuasiRobotics.Packages.ArLoadout.Find(recovered)==null && recovered.Storage==cargo && moved.Contains(stored) && stored.Storage==null,
            "recovered package becomes stock and its gear goes to cargo");
        Check(!Uninstaller.StripLoadout(Item("plain_test",1),moved),"ordinary items are left alone");
    }
    static void UpgradeTests(Harmony harmony,ConfigRecordCollection<MagnumPerkRecord> perks)
    {
        foreach(var type in new[]{typeof(RobotUpgradeIntPatch),typeof(RobotUpgradeFloatPatch)}) harmony.CreateClassProcessor(type).Patch();
        var expected=new Dictionary<string,(int x,int y,string parent)>{
            [RobotUpgrades.FusionWelds]=(0,1,RoboticsModule.PrintyardId),[RobotUpgrades.RedundancySystems]=(1,1,RobotUpgrades.FusionWelds),
            [RobotUpgrades.AblativePlating]=(0,2,RobotUpgrades.FusionWelds),[RobotUpgrades.ReinforcedAlloys]=(1,2,RobotUpgrades.AblativePlating),
            [RobotUpgrades.TargetingSystems]=(0,3,RobotUpgrades.AblativePlating),[RobotUpgrades.EnhancedOptics]=(1,3,RobotUpgrades.TargetingSystems),
            [RobotUpgrades.Overclocker]=(0,4,RobotUpgrades.TargetingSystems)};
        foreach(var kv in expected)
        {
            var r=perks.GetRecord(kv.Key);
            Check(r!=null && !r.IsDepartment && r.DepartmentId==RoboticsModule.PrintyardId && r.ModuleId=="robotics"
                && r.PositionInGraph.X==kv.Value.x && r.PositionInGraph.Y==kv.Value.y && r.Parents.SequenceEqual(new[]{kv.Value.parent})
                && r.UpgradePrice.SequenceEqual(RoboticsModule.PriceOf(kv.Key)) && perks.GetRecord(kv.Value.parent).Childs.Contains(kv.Key),
                kv.Key+" sits where drawn, linked to its parent, costs its own price");
        Check(RoboticsModule.PriceOf(RobotUpgrades.FusionWelds).SequenceEqual(new[]{"welding_machine_device","welding_machine_device","tool_container"}),"Fusion Welds costs 2 Welding Machine and a Toolbox");
        Check(RoboticsModule.PriceOf(RobotUpgrades.AblativePlating).Count(i=>i=="armor_plates")==30,"Ablative Plating takes 30 Armor Plate");
        Check(RoboticsModule.PriceOf(RobotUpgrades.ExpandedBays(5)).Length==33 && RoboticsModule.PriceOf(RobotUpgrades.ExpandedBays(5)).Last()=="human_poop","Eight fucking bears costs 8 of each and a Feces");
        Check(RobotUpgrades.All.All(u=>RoboticsModule.PriceOf(u.Id).Length>0),"every upgrade has a price");
        }
        Check(perks.GetRecord(RobotUpgrades.RedundancySystems).Childs.Contains(RoboticsModule.SalvageDronesId),"Redundancy Systems leads to Salvage Drones");
        Check(Localization.Get("mgperk.robo_overclocker.name")=="Overclocker" && Localization.Get("mgperk.robo_redundancy_systems.subName")=="Backup circuits",
            "upgrade names and subtitles resolve");
        var embedded=typeof(RoboticsModule).Assembly.GetManifestResourceNames();
        Check(RoboticsModule.Art.Count==21 && RoboticsModule.Art.Values.All(a=>embedded.Contains("QuasiRobotics."+a+".png")),"every Robotics node has its own embedded art");
        Check(embedded.Contains("QuasiRobotics.RecreationalHeadFixed.png") && embedded.Contains("QuasiRobotics.qrOverclocker_b.png"),"blocked and fixed variants are embedded");
        var all=RobotUpgrades.Total(id=>true);
        Check(QuasiRobotics.Patches.RoboticsTooltip.Rows(RobotUpgrades.TargetingSystems,null).Single().Value=="+6%"
            && QuasiRobotics.Patches.RoboticsTooltip.Rows(RoboticsModule.PrintyardId,all).Count==6
            && QuasiRobotics.Patches.RoboticsTooltip.Rows(RoboticsModule.PrintyardId,RobotUpgrades.Total(id=>false),2).Single().Value=="2"
            && QuasiRobotics.Patches.RoboticsTooltip.Description(RoboticsModule.SalvageDronesId)!=null,
            "tooltips list each upgrade's bonus, and Printyard lists the bonuses owned");
        for(int tier=1;tier<=5;tier++)
        {
            var bay=perks.GetRecord(RobotUpgrades.ExpandedBays(tier));
            Check(bay!=null && !bay.IsDepartment && bay.PositionInGraph.X==tier && bay.PositionInGraph.Y==0
                && bay.Parents.Single()==(tier==1?RoboticsModule.PrintyardId:RobotUpgrades.ExpandedBays(tier-1))
                && ModText.TryGet("mgperk."+bay.Id+".name",out _),
                "Expanded Bays "+tier+" sits on the top row, linked from the one before");
        }
        Check(RobotUpgrades.Slots(RobotUpgrades.Total(id=>false),2)==2 && RobotUpgrades.Slots(all,2)==12 && RobotUpgrades.Slots(all,3)==18 && RobotUpgrades.Slots(all,1)==6,
            "robot slots: one set from Printyard plus one per Expanded Bays, set size from the slider");
        Check(QuasiRobotics.Patches.RoboticsTooltip.Rows(RobotUpgrades.ExpandedBays(3),null,3).Single().Value=="+3","Expanded Bays lists the slots it adds");
        Check(QuasiRobotics.Patches.RoboticsTooltip.Rows(RobotUpgrades.ExpandedBays(5),null,2).Select(r=>r.Key).SequenceEqual(new[]{"Robot slots","Holy shit"}),"Expanded Bays V says so");
        Check(all.Health==30 && all.Resists==16 && all.Accuracy==18 && all.ActionPoints==1,"owning every upgrade stacks their bonuses");
        var progression=new MagnumProgression();
        progression.AddPerk(RobotUpgrades.FusionWelds,silent:true); progression.AddPerk(RobotUpgrades.TargetingSystems,silent:true);
        RobotUpgrades.Track(progression);
        var robot=DeploymentRobot("upgraded").Data; var other=TestRobot("cyborg_battle");
        Check(PerkSystem.GetPerkParameterSumInt(robot,"IMaxHealth")==10 && PerkSystem.GetPerkParameterSumInt(other,"IMaxHealth")==0,"health bonus applies to roster robots only");
        Check(Mathf.Abs(PerkSystem.GetPerkParameterSumFloat(robot,"FAccuracyBonus")-0.06f)<0.0001f && PerkSystem.GetPerkParameterSumInt(robot,"IResists")==0,"accuracy bonus is +6 points; unowned resists add nothing");
        int before=robot.Health.MaxValue;
        RobotUpgrades.SyncHealth(robot);
        Check(robot.Health.MaxValue==before+10 && robot.Health.Value==robot.Health.MaxValue && robot.PerksMaxHealth==10,"owned health bonus raises max and current health");
        RobotUpgrades.SyncHealth(robot);
        Check(robot.Health.MaxValue==before+10,"health bonus is applied once");
        int ap=-1; RobotUpgradeActionPointsPatch.Prefix(robot,ref ap);
        Check(ap==-1,"no Overclocker, no extra AP");
        progression.AddPerk(RobotUpgrades.Overclocker,silent:true);
        RobotUpgradeActionPointsPatch.Prefix(robot,ref ap);
        Check(ap==robot.BaseActionPoints+1,"Overclocker gives robots +1 AP when they spawn");
        var slotted=new RobotRoster{SlotCapacity=7};
        Check(slotted.Capacity==2,"a tracked campaign's slots come from its upgrades, not the saved number");
        progression.AddPerk(RobotUpgrades.ExpandedBays(1),silent:true);
        Check(slotted.Capacity==4 && slotted.EmptySlots==4,"Expanded Bays I adds two slots");
        Uninstaller.RemoveDevice(progression,RobotUpgrades.FusionWelds);
        RobotUpgrades.SyncHealth(robot);
        Check(robot.Health.MaxValue==before && robot.PerksMaxHealth==0,"losing the upgrade removes its health");
        DropPodTests(perks,progression,robot);
        RobotUpgrades.Track(null);
    }
    static void DropPodTests(ConfigRecordCollection<MagnumPerkRecord> perks,MagnumProgression progression,CreatureData robot)
    {
        var pods=perks.GetRecord(RoboticsModule.DropPodsId);
        Check(pods!=null && pods.IsDepartment && pods.ModuleId=="robotics" && pods.PositionInGraph.X==3 && pods.PositionInGraph.Y==3
            && pods.Parents.SequenceEqual(new[]{RobotUpgrades.BolsteredSwarm}) && perks.GetRecord(RobotUpgrades.BolsteredSwarm).Childs.Contains(RoboticsModule.DropPodsId)
            && pods.UpgradePrice.SequenceEqual(RoboticsModule.DropPodsPrice),"Drop Pods is a device at column 4, row 4, under Bolstered Swarm");
        var expected=new Dictionary<string,(int x,int y,string parent,string department)>{
            [RobotUpgrades.RestorativeNanobots]=(3,1,RoboticsModule.SalvageDronesId,RoboticsModule.SalvageDronesId),
            [RobotUpgrades.BolsteredSwarm]=(3,2,RobotUpgrades.RestorativeNanobots,RoboticsModule.SalvageDronesId),
            [RobotUpgrades.RapidResponseUnit]=(4,3,RoboticsModule.DropPodsId,RoboticsModule.DropPodsId),
            [RobotUpgrades.FoamCanisters]=(4,2,RobotUpgrades.RapidResponseUnit,RoboticsModule.DropPodsId),
            [RobotUpgrades.ElectricFence]=(5,3,RobotUpgrades.RapidResponseUnit,RoboticsModule.DropPodsId),
            [RobotUpgrades.FlatPackFurniture]=(4,4,RobotUpgrades.RapidResponseUnit,RoboticsModule.DropPodsId)};
        foreach(var kv in expected)
        {
            var r=perks.GetRecord(kv.Key);
            Check(r!=null && !r.IsDepartment && r.DepartmentId==kv.Value.department && r.PositionInGraph.X==kv.Value.x && r.PositionInGraph.Y==kv.Value.y
                && r.Parents.SequenceEqual(new[]{kv.Value.parent}) && perks.GetRecord(kv.Value.parent).Childs.Contains(kv.Key)
                && r.UpgradePrice.SequenceEqual(RoboticsModule.PriceOf(kv.Key)) && ModText.TryGet("mgperk."+kv.Key+".name",out _),
                kv.Key+" sits where drawn, linked to its parent");
        }
        Check(Localization.Get("mgperk.drop_pods.name")=="Drop Pods" && Localization.Get("mgperk.robo_rapid_response_unit.name")=="Rapid Response Unit","Drop Pods names resolve");
        Check(QuasiRobotics.Patches.RoboticsTooltip.Rows(RobotUpgrades.BolsteredSwarm,null,2).Single().Value=="+2"
            && QuasiRobotics.Patches.RoboticsTooltip.Rows(RoboticsModule.DropPodsId,null,2).Select(r=>r.Value).SequenceEqual(new[]{"2","48h"})
            && QuasiRobotics.Patches.RoboticsTooltip.Rows(RoboticsModule.DropPodsId,null,2,true).Last().Value=="24h"
            && QuasiRobotics.Patches.RoboticsTooltip.Description(RobotUpgrades.FoamCanisters)!=null,"Drop Pods tooltips list charges, recharge time and node effects");
        Check(RobotUpgrades.Total(id=>true).HealthRegen==3,"regen nodes stack to +3");
        progression.AddPerk(RobotUpgrades.RestorativeNanobots,silent:true);
        Check(PerkSystem.GetPerkParameterSumInt(robot,"IHealthRegen")==1 && PerkSystem.GetPerkParameterSumInt(TestRobot("cyborg_battle"),"IHealthRegen")==0,"Restorative Nanobots gives robots +1 health regen");
        Check(RobotDeployment.RepairTime(7,true)==4 && RobotDeployment.RepairTime(7,false)==7 && RobotDeployment.RepairTime(0,true)==0,"Rapid Response Unit halves repair time");

        var now=new DateTime(2200,1,1);
        var roster=new RobotRoster();
        var ready=DeploymentRobot("pod_ready"); var deployed=DeploymentRobot("pod_out"); var broken=DeploymentRobot("pod_broken"); var fixing=DeploymentRobot("pod_fixing");
        broken.Destroyed=true; fixing.RepairEnd=now.AddHours(3);
        roster.Robots.AddRange(new[]{ready,deployed,broken,fixing}); roster.DeployedIds.Add("pod_out");
        Check(DropPods.Callable(roster,now).Single()==ready,"Drop Pods call only robots aboard, intact and not in repair");
        Check(DropPods.Callable(roster,now,new[]{"pod_ready"}).Count==0,"a robot already in a pod is not called twice");
        Check(DropPods.Ready(roster,now)==2 && DropPods.Use(roster,now,false) && DropPods.Ready(roster,now)==1,"Drop Pods start with 2 charges, a call uses one");
        Check(DropPods.Use(roster,now,true) && !DropPods.Use(roster,now,false) && DropPods.UntilNext(roster,now)==TimeSpan.FromHours(24),"no third call; Flat Pack Furniture charges come back in 24h");
        Check(DropPods.Ready(roster,now.AddHours(24))==1 && DropPods.Ready(roster,now.AddHours(48))==2,"charges come back after 48h, or 24h with Flat Pack Furniture");
        var keyRecord=DropPodsKeybind.CreateRecord();
        var gameKey=new GameKey(keyRecord,0);
        Check(keyRecord.Id==DropPodsKeybind.KeyId && keyRecord.Layout=="Hotkeys" && gameKey.Bind1.Single()==KeyCode.X && gameKey.Bind2.Single()==KeyCode.None
            && DropPodsButton.KeyId==DropPodsKeybind.KeyId && Localization.Get("gamekey."+DropPodsKeybind.KeyId+".desc")=="Call Drop Pods","Drop Pods is a game hotkey, X by default");
        var keys=new ConfigRecordCollection<GameKeyRecord>();
        DropPodsKeybind.Register(keys); DropPodsKeybind.Register(keys);
        Check(keys.Count==1,"the Drop Pods key is registered once");
        var bind1=new List<KeyCode>{KeyCode.X}; var bind2=new List<KeyCode>{KeyCode.F};
        DropPodsKeybind.Set(bind2,bind1,new List<KeyCode>{KeyCode.X},KeyCode.None);
        Check(bind2.Single()==KeyCode.X && bind1.Single()==KeyCode.None,"rebinding to the other slot's key clears that slot");
        Check(!new ModSettings().StartWithDropPods,"Start with Drop Pods is off by default");

        var attacker=TestRobot("cyborg_battle"); attacker.Health=new HealthInfo(50); attacker.CreatureAlliance=CreatureAlliance.VictimFaction;
        robot.CreatureAlliance=CreatureAlliance.PlayerAlliance;
        var hit=new DamageHitInfo();
        Check(ElectricFencePatch.ShouldCounter(attacker,robot,hit,true) && !ElectricFencePatch.ShouldCounter(attacker,robot,hit,false),"Electric Fence? shocks a melee attacker once owned");
        hit.wasMiss=true;
        Check(!ElectricFencePatch.ShouldCounter(attacker,robot,hit,true),"a missed swing is not shocked");
        hit.wasMiss=false; attacker.CreatureAlliance=CreatureAlliance.PlayerAlliance;
        Check(!ElectricFencePatch.ShouldCounter(attacker,robot,hit,true),"allies are never shocked");
        Check(QuasiRobotics.Patches.FriendlyFirePatch.Blocks(CreatureAlliance.PlayerAlliance,CreatureAlliance.PlayerAlliance)
            && !QuasiRobotics.Patches.FriendlyFirePatch.Blocks(CreatureAlliance.VictimFaction,CreatureAlliance.VictimFaction),"friendly fire is blocked on the player's side only");
        Check(new ModSettings().DisableFriendlyFire,"Disable friendly fire defaults to on");
    }
    static void LocalizationTests()
    {
        var english=ModText.Keys(ModText.Fallback).ToList();
        Check(english.Count>100 && ModText.Get("mgperk.printyard.name")=="Printyard","English texts load from Localization/EnglishUS.json");
        foreach (Localization.Lang lang in Enum.GetValues(typeof(Localization.Lang)))
        {
            var missing=english.Where(k=>!ModText.Has(lang,k)).ToList();
            Check(missing.Count==0,lang+".json has every English key (missing: "+string.Join(", ",missing.Take(5))+")");
        }
        Check(ModText.Format("quasirobotics.droppods.inbound_many",3)=="Drop Pods inbound: 3 robots.","texts with numbers fill in");
        Check(ModText.Get("quasirobotics.no_such_key")=="quasirobotics.no_such_key","an unknown key shows as itself");
        var start=new DateTime(2100,1,1);
        var repairRoster=new RobotRoster();
        repairRoster.Robots.Add(new RobotRecord{RepairEnd=start.AddHours(5)});
        repairRoster.Robots.Add(new RobotRecord{RepairEnd=start.AddHours(-1)});
        Check(QuasiRobotics.Patches.RobotRepairNotifyPatch.FinishedBetween(repairRoster,start,start.AddHours(4)).Count==0
            && QuasiRobotics.Patches.RobotRepairNotifyPatch.FinishedBetween(repairRoster,start,start.AddHours(6)).Count==1,"repair notification fires once, when the repair ends");
    }
}
