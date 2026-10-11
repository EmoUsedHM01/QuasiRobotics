using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MGSC;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuasiRobotics;

// The Robotics ship room. Native ship rooms are just the ModuleIds of the
// magnum_perks table, so adding a perk record with a new ModuleId gives the
// room its ship point, window and tech tree. The ship model needs an anchor
// and a camera for it, which are added at runtime.
public static class RoboticsModule
{
    public const string ModuleId = "robotics";
    public const string PrintyardId = "printyard";
    // Welding Machine, Robot Parts, Reanimatron.
    public static readonly string[] PrintyardPrice = { "welding_machine_device", "robot_parts_container", "robot_resurrect_device" };
    // Lets destroyed robots return to the ship. Construction Drone, 2 Toolbox, Ledger.
    public const string SalvageDronesId = "salvage_drones";
    public static readonly string[] SalvageDronesPrice = { "construction_equipment", "tool_container", "tool_container", "ledger_book" };
    public const string UpgradeIconSprite = "Active_Enemy";
    // Mission active: calls robots left aboard down in explosive pods (see DropPods).
    public const string DropPodsId = "drop_pods";
    // 2 Geoscanner, Driller, 2 Welding Machine, Toolbox.
    public static readonly string[] DropPodsPrice = Cost(("geoscanner_device", 2), ("mining_equipment", 1), ("welding_machine_device", 2), ("tool_container", 1));
    // Mini-node prices (see RobotUpgrades for the bonuses). A repeated item id is its quantity.
    public static readonly Dictionary<string, string[]> UpgradePrices = new Dictionary<string, string[]>
    {
        [RobotUpgrades.FusionWelds] = Cost(("welding_machine_device", 2), ("tool_container", 1)),
        [RobotUpgrades.RedundancySystems] = Cost(("engineering_parts_container", 1), ("electrical_parts_container", 1), ("memory_software", 1)),
        [RobotUpgrades.AblativePlating] = Cost(("welding_machine_device", 1), ("armor_plates", 30), ("ceramite_plates", 1)),
        [RobotUpgrades.ReinforcedAlloys] = Cost(("armor_plates", 20), ("rusty_plates", 20), ("venus_armor_plates", 5), ("moon_armor_plates", 5)),
        [RobotUpgrades.TargetingSystems] = Cost(("military_unit", 1), ("electrical_parts_container", 1), ("robot_parts_container", 1), ("quasi_sensor_device", 1)),
        [RobotUpgrades.EnhancedOptics] = Cost(("ai_module", 1), ("lens", 20), ("tech_repair_kit", 5), ("darknet_unit", 2)),
        [RobotUpgrades.Overclocker] = Cost(("ai_module", 2), ("robot_parts_container", 2), ("military_unit", 3), ("gunbang_magazine", 3)),
        [RobotUpgrades.ExpandedBays(1)] = Cost(("construction_equipment", 1), ("worker_clothing_container", 2), ("tool_container", 3), ("management_unit", 2)),
        [RobotUpgrades.ExpandedBays(2)] = Cost(("construction_equipment", 2), ("worker_clothing_container", 4), ("tool_container", 5), ("management_unit", 5)),
        [RobotUpgrades.ExpandedBays(3)] = Cost(("construction_equipment", 5), ("worker_clothing_container", 10), ("tool_container", 12), ("management_unit", 12)),
        [RobotUpgrades.ExpandedBays(4)] = Cost(("construction_equipment", 10), ("worker_clothing_container", 20), ("tool_container", 25), ("management_unit", 25)),
        [RobotUpgrades.ExpandedBays(5)] = Cost(("construction_equipment", 8), ("worker_clothing_container", 8), ("tool_container", 8), ("management_unit", 8), ("human_poop", 1)),
        [RobotUpgrades.RestorativeNanobots] = Cost(("medical_equipment", 1), ("tool_container", 1), ("robot_parts_container", 1), ("electrical_parts_container", 1)),
        [RobotUpgrades.BolsteredSwarm] = Cost(("ai_module", 1), ("robot_resurrect_device", 3)),
        [RobotUpgrades.RapidResponseUnit] = Cost(("scientific_equipment", 1), ("research_disk", 2), ("robot_resurrect_device", 3), ("management_unit", 2)),
        [RobotUpgrades.FoamCanisters] = Cost(("fire_extinguisher", 10), ("hydraulic_pump", 1)),
        [RobotUpgrades.ElectricFence] = Cost(("electrical_parts_container", 5), ("defibrillator_device", 2)),
        [RobotUpgrades.FlatPackFurniture] = Cost(("construction_equipment", 2), ("worker_clothing_container", 1), ("tool_container", 1)),
    };
    public static string[] Cost(params (string id, int count)[] items) => items.SelectMany(i => Enumerable.Repeat(i.id, i.count)).ToArray();
    public static IEnumerable<string> DeviceIds => new[] { PrintyardId, SalvageDronesId, DropPodsId }.Concat(RobotUpgrades.All.Select(u => u.Id));
    public static string[] PriceOf(string id) => id == PrintyardId ? PrintyardPrice : id == SalvageDronesId ? SalvageDronesPrice
        : id == DropPodsId ? DropPodsPrice : UpgradePrices.TryGetValue(id, out var price) ? price : new string[0];
    // Placeholder icons until Printyard has its own art.
    public const string IconSourcePerkId = "hyperwave_scanner_department";

    // Owning Printyard unlocks the Robots tab and the departure Robots section.
    public static bool HasPrintyard(MagnumProgression progression) =>
        progression != null && progression.IsPerkPurchased(PrintyardId);
    public static bool HasPrintyard() =>
        HasPrintyard(SingletonMonoBehaviour<SpaceGameMode>.Instance?.Get<MagnumProgression>());
    // Without Salvage Drones, robots destroyed on a mission are not recovered.
    public static bool HasSalvageDrones(MagnumProgression progression) =>
        progression != null && progression.IsPerkPurchased(SalvageDronesId);
    public static bool HasDropPods(MagnumProgression progression) =>
        progression != null && progression.IsPerkPurchased(DropPodsId);

    // Uninstall mode wins, so a cleaned save is not given Printyard back.
    public static List<string> GrantStartingDevices(MagnumProgression progression)
    {
        var settings = Plugin.Settings;
        bool on = settings != null && !settings.UninstallMode;
        var granted = new List<string>();
        if (GrantStartingDevice(progression, PrintyardId, on && settings.StartWithPrintyard)) granted.Add(PrintyardId);
        if (GrantStartingDevice(progression, SalvageDronesId, on && settings.StartWithSalvageDrones)) granted.Add(SalvageDronesId);
        if (GrantStartingDevice(progression, DropPodsId, on && settings.StartWithDropPods)) granted.Add(DropPodsId);
        return granted;
    }
    public static bool GrantStartingDevice(MagnumProgression progression, string id, bool enabled)
    {
        if (!enabled || progression == null || progression.IsPerkPurchased(id) || Data.MagnumPerks.GetRecord(id) == null) return false;
        progression.AddPerk(id, silent: true);
        return true;
    }

    public static MagnumPerkRecord CreatePrintyardRecord() => CreateDeviceRecord(PrintyardId, 0, 0);
    public static MagnumPerkRecord CreateSalvageDronesRecord()
    {
        var record = CreateDeviceRecord(SalvageDronesId, 2, 1);
        record.Parents.Add(RobotUpgrades.RedundancySystems);
        record.Parents.Add(RobotUpgrades.ExpandedBays(2));
        return record;
    }
    public static MagnumPerkRecord CreateDropPodsRecord()
    {
        var record = CreateDeviceRecord(DropPodsId, 3, 3);
        record.Parents.Add(RobotUpgrades.BolsteredSwarm);
        return record;
    }
    public static MagnumPerkRecord CreateUpgradeRecord(RobotUpgrade upgrade)
    {
        var record = CreateDeviceRecord(upgrade.Id, upgrade.X, upgrade.Y);
        record.IsDepartment = false;
        record.DepartmentId = upgrade.Department;
        record.Parents.Add(upgrade.Parent);
        return record;
    }
    public static List<MagnumPerkRecord> CreateRecords() =>
        new[] { CreatePrintyardRecord(), CreateSalvageDronesRecord(), CreateDropPodsRecord() }.Concat(RobotUpgrades.All.Select(CreateUpgradeRecord)).ToList();

    // Passive devices, like Navigation's Scanner: departments with no modifiers.
    private static MagnumPerkRecord CreateDeviceRecord(string id, int x, int y)
    {
        var record = new MagnumPerkRecord
        {
            Enabled = true,
            ModuleId = ModuleId,
            DepartmentId = string.Empty,
            IsDepartment = true,
            Parents = new List<string>(),
            PositionInGraph = new CellPosition(x, y),
            Modifiers = new List<MagnumParameterModifier>(),
            UpgradePrice = PriceOf(id).ToList(),
        };
        AccessTools.Property(typeof(ConfigTableRecord), nameof(ConfigTableRecord.Id)).SetValue(record, id);
        return record;
    }

    // Called once the game's config tables are loaded.
    public static void RegisterPerk(ConfigRecordCollection<MagnumPerkRecord> perks)
    {
        if (perks == null) return;
        var deviceIcon = perks.GetRecord(IconSourcePerkId)?.ContentDescriptor;
        var art = new Dictionary<string, Object>();
        foreach (var pair in Art)
            if (TryIcon(() => CreateDescriptor(pair.Value), pair.Value) is Object descriptor) art[pair.Key] = descriptor;
        var upgradeIcon = TryIcon(() => FindIcon(perks, UpgradeIconSprite), UpgradeIconSprite) ?? deviceIcon;
        var added = new List<MagnumPerkRecord>();
        foreach (var record in CreateRecords())
        {
            if (perks.GetRecord(record.Id) != null) continue;
            record.ContentDescriptor = art.TryGetValue(record.Id, out var own) ? own : record.IsDepartment ? deviceIcon : upgradeIcon;
            perks.AddRecord(record.Id, record);
            added.Add(record);
        }
        // The game links children to parents (Childs) before mods load.
        foreach (var record in added)
            foreach (var parent in record.Parents)
            {
                var parentRecord = perks.GetRecord(parent);
                if (parentRecord != null && !parentRecord.Childs.Contains(record.Id)) parentRecord.Childs.Add(record.Id);
            }
    }

    // Tree art in Assets/ (embedded): <name>.png, plus <name>_b.png for the
    // blocked (locked) look. Without a _b file the locked look is a darker copy.
    public static readonly Dictionary<string, string> Art = new Dictionary<string, string>
    {
        [PrintyardId] = "qrPrintyard",
        [SalvageDronesId] = "qrSalvageDrones",
        [RobotUpgrades.FusionWelds] = "qrFusionWelds",
        [RobotUpgrades.RedundancySystems] = "qrRedundancySystems",
        [RobotUpgrades.AblativePlating] = "qrAblativePlating",
        [RobotUpgrades.ReinforcedAlloys] = "qrReinforcedAlloys",
        [RobotUpgrades.TargetingSystems] = "qrTargetingSystems",
        [RobotUpgrades.EnhancedOptics] = "qrEnhancedOptics",
        [RobotUpgrades.Overclocker] = "qrOverclocker",
        [RobotUpgrades.ExpandedBays(1)] = "qrExpandedBays1",
        [RobotUpgrades.ExpandedBays(2)] = "qrExpandedBays2",
        [RobotUpgrades.ExpandedBays(3)] = "qrExpandedBays3",
        [RobotUpgrades.ExpandedBays(4)] = "qrExpandedBays4",
        [RobotUpgrades.ExpandedBays(5)] = "qrExpandedBays5",
        [RobotUpgrades.RestorativeNanobots] = "qrRestorativeNanobots",
        [RobotUpgrades.BolsteredSwarm] = "qrBolsteredSwarm",
        [DropPodsId] = "qrDropPods",
        [RobotUpgrades.RapidResponseUnit] = "qrRapidResponseUnit",
        [RobotUpgrades.FoamCanisters] = "qrFoamCanisters",
        [RobotUpgrades.ElectricFence] = "qrElectricFence",
        [RobotUpgrades.FlatPackFurniture] = "qrFlatPackFurniture",
    };
    public const float LockedBrightness = .45f;

    // Icon code calls into the Unity engine. The try sits in the caller, since a
    // method that calls the engine can fail as soon as it is entered (as it
    // does outside the game, in the tests); the perks then keep fallback icons.
    private static Object TryIcon(Func<Object> make, string asset)
    {
        try { return make(); }
        catch (Exception ex)
        {
            try { Plugin.Logger.LogWarning("Robotics icon " + asset + " could not be loaded: " + ex.Message); } catch { }
            return null;
        }
    }
    private static Object CreateDescriptor(string asset)
    {
        var icon = RobotButtonSprites.Get(asset);
        var locked = RobotButtonSprites.Exists(asset + "_b") ? RobotButtonSprites.Get(asset + "_b")
            : RobotButtonSprites.GetDimmed(asset, LockedBrightness);
        var descriptor = ScriptableObject.CreateInstance<MagnumPerkDescriptor>();
        Object.DontDestroyOnLoad(descriptor);
        AccessTools.Field(typeof(MagnumPerkDescriptor), "_icon").SetValue(descriptor, icon);
        AccessTools.Field(typeof(MagnumPerkDescriptor), "_lockedIcon").SetValue(descriptor, locked);
        AccessTools.Field(typeof(MagnumPerkDescriptor), "_fastActionIcon").SetValue(descriptor, icon);
        AccessTools.Field(typeof(MagnumPerkDescriptor), "_fastActionLockedIcon").SetValue(descriptor, locked);
        return descriptor;
    }

    // A perk icon by sprite name: a native perk's descriptor when one uses it,
    // otherwise a new descriptor around the loaded sprite.
    private static Object FindIcon(ConfigRecordCollection<MagnumPerkRecord> perks, string sprite)
    {
        var native = perks.Records.Select(r => r.ContentDescriptor)
            .FirstOrDefault(d => d is MagnumPerkDescriptor m && m.Icon != null && m.Icon.name == sprite);
        if (native != null) return native;
        var image = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(x => x.name == sprite);
        if (image == null) throw new InvalidOperationException("sprite not found");
        var descriptor = ScriptableObject.CreateInstance<MagnumPerkDescriptor>();
        foreach (var field in new[] { "_icon", "_lockedIcon", "_fastActionIcon", "_fastActionLockedIcon" })
            AccessTools.Field(typeof(MagnumPerkDescriptor), field).SetValue(descriptor, image);
        return descriptor;
    }

    // Where the room point sits, as a blend of the three leftmost room anchors
    // (L, D, M ordered by on-screen x in the ship view): L + U*(D-L) + V*(M-L).
    // Measured from the ship screen: below the MAGNUM lettering at the bow.
    public const float U = 1.1306f, V = -0.6484f;
    public static Vector3 AnchorPosition(Vector3 l, Vector3 d, Vector3 m) => l + U * (d - l) + V * (m - l);

    // Room anchors ordered from the bow, in ship space. Camera order is not
    // safe: loaded campaigns can build the ship view with the camera flipped,
    // which mirrored the room to the stern. Along the ship's long axis the
    // bow's 2nd and 3rd rooms are far apart, while the stern's sit side by side.
    public static List<int> OrderFromBow(IList<Vector3> positions)
    {
        var min = new Vector3(positions.Min(p => p.x), positions.Min(p => p.y), positions.Min(p => p.z));
        var max = new Vector3(positions.Max(p => p.x), positions.Max(p => p.y), positions.Max(p => p.z));
        var size = max - min;
        int axis = size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        var order = Enumerable.Range(0, positions.Count).OrderBy(i => positions[i][axis]).ToList();
        int n = order.Count;
        float lowGap = Mathf.Abs(positions[order[2]][axis] - positions[order[1]][axis]);
        float highGap = Mathf.Abs(positions[order[n - 3]][axis] - positions[order[n - 2]][axis]);
        if (highGap > lowGap) order.Reverse();
        return order;
    }

    public static void EnsureRoom(Spaceship ship)
    {
        if (ship == null || ship.UiAnchors.ContainsKey(ModuleId)) return;
        var parent = RobotRosterView.Field<Transform>(ship, "_uiAnchorsParent");
        var manager = RobotRosterView.Field<TravelingCameraManager>(ship, "_cameraManager");
        if (parent == null || manager == null) throw new InvalidOperationException("The ship has no room anchors.");
        var all = ship.UiAnchors.Where(a => a.Key != ModuleId && manager.ModuleIds.Contains(a.Key)).ToList();
        if (all.Count < 3) throw new InvalidOperationException("Fewer than three ship rooms to place Robotics from.");
        var rooms = OrderFromBow(all.Select(a => a.Value.localPosition).ToList()).Select(i => all[i]).ToList();
        var anchor = new GameObject(ModuleId).transform;
        anchor.SetParent(parent, false);
        anchor.localPosition = AnchorPosition(rooms[0].Value.localPosition, rooms[1].Value.localPosition, rooms[2].Value.localPosition);
        ship.UiAnchors[ModuleId] = anchor;
        Plugin.Logger.Log("Robotics room placed from " + rooms[0].Key + ", " + rooms[1].Key + ", " + rooms[2].Key + " at " + anchor.localPosition + ".");
        if (!manager.ModuleIds.Contains(ModuleId)) manager.ModuleIds.Add(ModuleId);
        // Room camera: a copy of the nearest room's camera, shifted onto the new anchor.
        var cameras = RobotRosterView.Field<IDictionary>(manager, "_moduleCameras");
        var nearest = rooms.OrderBy(a => Vector3.Distance(a.Value.position, anchor.position)).First();
        if (cameras != null && !cameras.Contains(ModuleId) && cameras[nearest.Key] is Component source)
        {
            var copy = Object.Instantiate(source.gameObject, source.transform.parent);
            copy.name = source.gameObject.name + "_" + ModuleId;
            copy.transform.position += anchor.position - nearest.Value.position;
            cameras[ModuleId] = copy.GetComponent(source.GetType());
        }
    }
}
