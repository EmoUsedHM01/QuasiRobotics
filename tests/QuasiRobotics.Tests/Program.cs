using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Collections.Generic;
using HarmonyLib;
using MGSC;
using QuasiRobotics;
using QuasiRobotics.Packages;
using QuasiRobotics.Patches;
partial class Program
{
    static int checks;
    static void Main()
    {
        AppDomain.CurrentDomain.AssemblyResolve += (s,e) => {
            string name=new AssemblyName(e.Name).Name+".dll";
            foreach(string dir in new[]{@"C:\Program Files (x86)\Steam\steamapps\common\Quasimorph\Quasimorph_Data\Managed",
                @"C:\Program Files (x86)\Steam\steamapps\workshop\content\2059170\3469678797"})
            {
                string p=Path.Combine(dir,name);
                if(File.Exists(p)) return Assembly.LoadFrom(p);
            }
            return null;
        };
        Run();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Run()
    {
        var harmony=new Harmony("ar.tests");
        // No Unity renderer is running; use ordinary reference identity for engine objects.
        harmony.Patch(AccessTools.Method(typeof(UnityEngine.Object),"op_Equality"),transpiler:new HarmonyMethod(typeof(Program),nameof(Equality)));
        harmony.Patch(AccessTools.Method(typeof(UnityEngine.Object),"op_Inequality"),transpiler:new HarmonyMethod(typeof(Program),nameof(Inequality)));
        foreach (var type in new[]{typeof(PackageMergePatch),typeof(PackageStackLimitPatch),typeof(PackageLoadedPatch),typeof(PackageCopyPatch),typeof(PackageEditorLockPatch),typeof(PackageMaxStackPatch),typeof(PackageFullStackPatch)})
            harmony.CreateClassProcessor(type).Patch();
        // Fill native serializer caches exactly as TypeCache.Initialize does, without engine logging.
        foreach(var assembly in new[]{typeof(Data).Assembly,typeof(Plugin).Assembly})
            foreach(var type in assembly.GetTypes())
                AccessTools.Method(typeof(TypeCache),"CacheTypeForSaveSystem").Invoke(null,new object[]{type});
        LoadFromJSON.AddCustomTypeResolver(name=>typeof(Data).Assembly.GetType(name)??typeof(Plugin).Assembly.GetType(name));
        var items=new ItemsCollection();
        foreach(string id in new[]{"ar_test","contents_test"})
        {
            var record=new CompositeItemRecord(id);
            SetId(record,id);
            record.Records.Add(new TrashRecord { MaxStack=20 });
            items.AddRecord(id,record);
        }
        AccessTools.Property(typeof(Data),"Items").SetValue(null,items);
        AccessTools.Field(typeof(SingletonMonoBehaviour<ItemFactory>),"s_instance").SetValue(null,FormatterServices.GetUninitializedObject(typeof(ItemFactory)));
        harmony.Patch(AccessTools.Method(typeof(ItemFactory),"GetGameTimeNow"),prefix:new HarmonyMethod(typeof(Program),nameof(TestTime)));
        MissionStorageTests(harmony);
        EquipmentTests(items);
        RobotTests(harmony);
        DeploymentTests(harmony);
        RoboticsTests(harmony);
        LocalizationTests();
        var inventory=new Inventory(6,5,4,default(Inventory.BackpackMode),true);
        inventory.BackpackStore.AddItemAndReshuffleOptional(Item("contents_test",7));
        var badge=new ArLoadoutComponent { MobClassId="robot_test", Inventory=inventory };
        badge.Augmentations["arm"]="custom_arm";
        var recovered=Item("ar_test",1); recovered.Add(badge); recovered.SetMaxStack(1);
        var stock=Item("ar_test",2);
        Check(!ItemInteractionSystem.CanMerge(recovered,stock),"recovered -> stock rejected");
        Check(!ItemInteractionSystem.CanMerge(stock,recovered),"stock -> recovered rejected");
        Check(ItemInteractionSystem.CanMerge(stock,Item("ar_test",1)),"ordinary stacks still merge");
        bool empty=false;
        Check(!ItemInteractionSystem.Merge(new SpaceTime(),recovered,stock,ref empty),"real Merge cannot swallow loadout");
        Check(recovered.StackCount==1 && stock.StackCount==2,"merge conserves both packages");
        recovered.SetMaxStack(20);
        Check(recovered.MaxStack==1,"Arsenal stack reset cannot remove protection");
        var cargo=new ItemStorage(ItemStorageSource.ShipCargo,8,3);
        cargo.AddItemAndReshuffleOptional(recovered);
        Check(PackageEditor.CanEdit(recovered),"recovered cargo package editable");
        Check(!PackageEditor.CanEdit(stock),"stock package without loadout not editable");
        // Edit the same Inventory reference the native editor receives, then round-trip the whole package.
        badge.Inventory.BackpackStore.Items[0].StackCount=11;
        var json=SaveToJSON.CreateNode(recovered);
        var loaded=new PickupItem(); loaded.LoadJSON(json["Content"]);
        var loadedBadge=ArLoadout.Find(loaded);
        Check(loadedBadge!=null && loadedBadge.MobClassId=="robot_test","badge and class survive native save/load");
        Check(loadedBadge.Inventory.BackpackStore.Items[0].StackCount==11,"edited inventory survives native save/load");
        Check(loadedBadge.Augmentations["arm"]=="custom_arm","augmentation survives native save/load");
        Check(loaded.MaxStack==1,"loaded package remains unstackable");
        Check(ReferenceEquals(loadedBadge.Inventory.BackpackStore.Items[0].Storage,loadedBadge.Inventory.BackpackStore),"loaded inventory ownership restored");
        var factory=(ItemFactory)FormatterServices.GetUninitializedObject(typeof(ItemFactory));
        AccessTools.Field(typeof(ItemFactory),"_componentsCache").SetValue(factory,new List<PickupItemComponent>());
        var copied=factory.CopyItem(recovered);
        var copyBadge=ArLoadout.Find(copied);
        Check(copyBadge!=null,"native CopyItem preserves custom badge");
        Check(!ReferenceEquals(copyBadge.Inventory,badge.Inventory),"copied inventory is independent");
        Check(!ReferenceEquals(copyBadge.Inventory.BackpackStore.Items[0],badge.Inventory.BackpackStore.Items[0]),"copied items are independent");
        copyBadge.Inventory.BackpackStore.Items[0].StackCount=3;
        Check(badge.Inventory.BackpackStore.Items[0].StackCount==11,"copy editing cannot change original");
        AccessTools.Property(typeof(PackageEditor),nameof(PackageEditor.Package)).SetValue(null,recovered);
        Check(recovered.Locked && recovered.LockCounter==0,"editor lock is runtime only");
        Check(!stock.Locked,"editor lock does not affect other items");
        AccessTools.Property(typeof(PackageEditor),nameof(PackageEditor.Package)).SetValue(null,null);
        Check(!recovered.Locked,"editor lock clears");
        // All Harmony attributes resolve to real installed-game methods.
        foreach(var type in typeof(Plugin).Assembly.GetTypes().Where(t=>t.GetCustomAttributes(typeof(HarmonyPatch),false).Length>0))
        {
            var infos=type.GetCustomAttributes(typeof(HarmonyPatch),false).Cast<HarmonyPatch>().Select(p=>p.info).ToArray();
            var info=infos[0];

            var name=info.methodName;
            if(info.methodType==MethodType.Getter) name="get_"+name;
            if(info.methodType==MethodType.Setter) name="set_"+name;
            // Out/ref variations are already by-ref types in argumentTypes.
            Check(AccessTools.Method(info.declaringType,name,info.argumentTypes)!=null,"hook resolves: "+type.Name);
        }
        Console.WriteLine("PASS: "+checks+" checks: native merge, stack limits, save/load, edited contents, clone isolation, robot roster conversion/persistence/names/fixed rank, runtime lock and hook targets.");
    }
    static void MissionStorageTests(Harmony harmony)
    {
        var inv=new Inventory(6,12,7,Inventory.BackpackMode.Normal,true);
        var items=new List<BasePickupItem>();
        for(int i=0;i<30;i++) { var item=new PickupItem("contents_test",i%3==0?2:1,1); items.Add(item); inv.BackpackStore.AddItemAndReshuffleOptional(item); }
        ArStorageLayout.ResizeBackpack(inv);
        Check(inv.BackpackStore.Width==4 && inv.BackpackStore.Height==18,"native 72-cell backpack becomes four by eighteen");
        Check(items.All(i=>i.Storage==inv.BackpackStore) && inv.BackpackStore.Items.Count==30,"native reshuffle retains all single and double-width items");
        Check(items.All(i=>i.InventoryPos.X+i.InventoryWidthSize<=4),"two-cell items fit within native row boundaries");
        var saved=SaveToJSON.CreateNode(inv).ToString();
        ArStorageLayout.ResizeBackpack(inv);
        Check(SaveToJSON.CreateNode(inv).ToString()==saved,"repeated refresh leaves native item coordinates stable");
        Check(ReferenceEquals(ArMissionStorage.SelectedStorage(inv,true),inv.VestStore),"vest uses live native storage");
        Check(inv.VestStore.Width==7,"backpack resizing leaves vest capacity unchanged");
        var view=(ArMissionStorage)FormatterServices.GetUninitializedObject(typeof(ArMissionStorage));
        AccessTools.Field(typeof(ArMissionStorage),"inventory").SetValue(view,inv);
        AccessTools.Field(typeof(ArMissionStorage),"showVest").SetValue(view,true);
        Check(ReferenceEquals(view.GetSelectedStorage(inv),inv.VestStore),"selected vest remains the native grid target");
        var other=new Inventory(4,4,2,Inventory.BackpackMode.Normal,true);
        Check(ReferenceEquals(view.GetSelectedStorage(other),other.BackpackStore),"vest selection cannot leak into another inventory");
        AccessTools.Field(typeof(ArMissionStorage),"showVest").SetValue(view,false);
        Check(ReferenceEquals(view.GetSelectedStorage(inv),inv.BackpackStore),"backpack selection restores the native target");
        var backpackField=AccessTools.Field(typeof(Inventory),nameof(Inventory.BackpackStore));
        var resolve=AccessTools.Method(typeof(ArMissionStorage),nameof(ArMissionStorage.GetSelectedStorage),new[]{typeof(Inventory),typeof(MonsterInspectWindow)});
        foreach(string name in new[]{"RefreshBackpackPage","RefreshSlots"})
        {
            var method=AccessTools.Method(typeof(MonsterInspectWindow),name,Type.EmptyTypes);
            var original=PatchProcessor.GetOriginalInstructions(method);
            Check(original.Count(i=>i.LoadsField(backpackField))==1,name+" has one native backpack binding");
            var patched=ArMissionStorage.PreserveStorage(original).ToList();
            Check(!patched.Any(i=>i.LoadsField(backpackField)) && patched.Count(i=>i.Calls(resolve))==1,name+" binds directly to selected storage without a temporary backpack grid");
        }
        var odd=new Inventory(6,3,0,Inventory.BackpackMode.Normal,true);
        ArStorageLayout.ResizeBackpack(odd);
        Check(odd.BackpackStore.Width==4 && odd.BackpackStore.Height==5,"partial native row rounds up without losing pack capacity");
        Check(!typeof(Plugin).Assembly.GetTypes().SelectMany(t=>t.GetCustomAttributes(typeof(HarmonyPatch),false).Cast<HarmonyPatch>()).Any(p=>p.info.declaringType==typeof(ItemGrid)||p.info.declaringType==typeof(ItemGridScroll)),"no custom grid rendering or scrolling patches remain");
    }
    static bool TestTime(ref SpaceTime __result) { __result=new SpaceTime(); return false; }
    sealed class Cargo : IFloorItemsGetter
    {
        public ItemStorage Store = new ItemStorage(ItemStorageSource.ShipCargo,20,20);
        public ItemStorage GetActualFloorItems()=>Store;
    }
    static void EquipmentTests(ItemsCollection items)
    {
        var packRecord=new BackpackRecord { Width=6,Height=5,MaxDurability=100 };
        var vestRecord=new VestRecord { SlotCapacity=5,MaxDurability=100 };
        SetId(packRecord,"test_pack"); SetId(vestRecord,"test_vest");
        foreach(var record in new BasePickupItemRecord[]{packRecord,vestRecord})
        {
            var composite=new CompositeItemRecord(record.Id); SetId(composite,record.Id);
            composite.Records.Add(record); items.AddRecord(record.Id,record);
        }
        var pack=new PickupItem("test_pack",2,1); pack.Add(packRecord,true); pack.Add(new BreakableItemComponent(packRecord));
        var vest=new PickupItem("test_vest",2,1); vest.Add(vestRecord,true); vest.Add(new BreakableItemComponent(vestRecord));
        var inv=new Inventory(4,4,0,Inventory.BackpackMode.Endless,false);
        inv.BackpackSlot.AddItemAndReshuffleOptional(pack);
        inv.VestSlot.AddItemAndReshuffleOptional(vest);
        Check(inv.BackpackStore.Width==4 && inv.VestStore.Width==0,"reproduces original ignored equipment");
        bool rejected=false; try{ArEquipmentStorage.Enable(inv,null);}catch(InvalidOperationException){rejected=true;}
        Check(rejected && !inv.CanHaveVest,"missing overflow destination rejected before changes");
        var cargo=new Cargo(); ArEquipmentStorage.Enable(inv,cargo);
        Check(inv.BackpackStore.Width==6 && inv.BackpackStore.Height==5,"existing backpack capacity refreshed");
        Check(inv.VestStore.Width==5 && inv.CanHaveVest,"existing vest capacity refreshed");
        inv.RefreshPerkVestBonus(2);
        Check(inv.VestStore.Width==7,"vest bonuses use native sizing");
        for(int i=0;i<20;i++)ArEquipmentStorage.Enable(inv,cargo);
        Check(inv.VestStore.Width==7 && inv.BackpackStore.Height==5,"reopening cannot accumulate capacity");
        inv.BackpackSlot.Remove(pack); inv.VestSlot.Remove(vest);
        Check(inv.BackpackStore.Width==4 && inv.BackpackStore.Height==4,"removing backpack restores AR base storage");
        Check(inv.VestStore.Width==2,"removing vest preserves bonus slots");
        inv.BackpackSlot.AddItemAndReshuffleOptional(pack); inv.VestSlot.AddItemAndReshuffleOptional(vest);
        Check(inv.BackpackStore.Height==5 && inv.VestStore.Width==7,"subsequent native equip callbacks expand storage");
        var extended=new ExtendedHeightComp { ExtendedHeight=12 }; pack.Add(extended);
        ArEquipmentStorage.Enable(inv,cargo);
        Check(inv.BackpackStore.Height==12,"upgraded backpack rows respected");
        packRecord.AddServoArm = true;
        ArEquipmentStorage.Enable(inv,cargo);
        Check(inv.HasServoArm && inv.ServoArmSlot.Width==2,"servo backpack enables the third weapon storage");
        packRecord.AddServoArm = false;
        ArEquipmentStorage.Enable(inv,cargo);
        Check(!inv.HasServoArm,"ordinary backpack disables third weapon storage");
        var contents=new List<BasePickupItem>();
        for(int n=0;n<40;n++){var content=new PickupItem("contents_test",1,1); contents.Add(content); inv.BackpackStore.AddItemAndReshuffleOptional(content);}
        inv.BackpackSlot.Remove(pack);
        Check(cargo.Store.Items.Count>0 && contents.All(i=>i.Storage==inv.BackpackStore || i.Storage==cargo.Store),"removing full backpack preserves every item and sends overflow to cargo");
        inv.BackpackSlot.AddItemAndReshuffleOptional(pack);
        var badge=Item("ar_test",1); badge.Add(new ArLoadoutComponent {MobClassId="robot_test",Inventory=inv});
        var saved=SaveToJSON.CreateNode(badge); var loaded=new PickupItem(); loaded.LoadJSON(saved["Content"]);
        var restored=ArLoadout.Find(loaded).Inventory;
        Check(restored.CanHaveVest && restored.BackpackStore.Height==12 && restored.VestStore.Width==7,"equipment capacity survives badge save/load");
        restored.InitializeItemsOnFloor(cargo);

        restored.BackpackSlot.Remove(restored.BackpackSlot.First); restored.VestSlot.Remove(restored.VestSlot.First);
        Check(restored.BackpackStore.Height==4 && restored.VestStore.Width==2,"native sizing remains active after reload");
    }
    static PickupItem Item(string id,short count)
    {
        var i=new PickupItem(id,1,1); i.Add(new StackableItemComponent(20){Count=count});
        var r=new TrashRecord {MaxStack=20}; SetId(r,id); i.Add(r,true); return i;
    }
    static void SetId(ConfigTableRecord r,string id)=>AccessTools.Property(typeof(ConfigTableRecord),"Id").SetValue(r,id);
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static IEnumerable<CodeInstruction> Equality(IEnumerable<CodeInstruction> instructions){yield return new CodeInstruction(OpCodes.Ldarg_0);yield return new CodeInstruction(OpCodes.Ldarg_1);yield return new CodeInstruction(OpCodes.Ceq);yield return new CodeInstruction(OpCodes.Ret);}
    static IEnumerable<CodeInstruction> Inequality(IEnumerable<CodeInstruction> instructions){yield return new CodeInstruction(OpCodes.Ldarg_0);yield return new CodeInstruction(OpCodes.Ldarg_1);yield return new CodeInstruction(OpCodes.Ceq);yield return new CodeInstruction(OpCodes.Ldc_I4_0);yield return new CodeInstruction(OpCodes.Ceq);yield return new CodeInstruction(OpCodes.Ret);}
}

