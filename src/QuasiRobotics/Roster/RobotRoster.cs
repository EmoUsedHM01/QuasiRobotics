using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MGSC;
using QuasiRobotics.Packages;

namespace QuasiRobotics;

// Campaign data, separate from both Mercenaries.Values and cargo items.
public sealed class RobotRoster
{
    [Save] public List<RobotRecord> Robots = new List<RobotRecord>();
    // Saved for old campaigns; the live value comes from Printyard and
    // Expanded Bays (see Capacity), when a campaign's progression is known.
    [Save] public int SlotCapacity = 2;
    public int Capacity => RobotUpgrades.SlotCapacity(SlotCapacity);
    [Save] public List<string> SelectedIds = new List<string>();
    [Save] public List<string> DeployedIds = new List<string>();
    [Save] public List<string> PendingSpawnIds = new List<string>();
    // Drop Pods charges in use: when each one is ready again.
    [Save] public List<DateTime> DropPodCooldowns = new List<DateTime>();
    public int EmptySlots => Math.Max(0, Capacity - Robots.Count);
    private static readonly ConditionalWeakTable<Mercenaries, RobotRoster> Rosters = new ConditionalWeakTable<Mercenaries, RobotRoster>();
    public static RobotRoster For(Mercenaries owner) => Rosters.GetOrCreateValue(owner);
    public static void Restore(Mercenaries owner, RobotRoster roster)
    {
        Rosters.Remove(owner);
        Rosters.Add(owner, roster);
    }
    public static bool CanRegister(BasePickupItem item)
    {
        if (item == null || item.Storage?.Source != ItemStorageSource.ShipCargo || item.Locked || item.StackCount < 1) return false;
        var device = item.Record<PlaceableDeviceRecord>();
        if (device == null || device.Usage.ToString() != ArPackageMap.AllySpawnUsage || !IsDroid(device.TargetObjectId)) return false;
        var saved = ArLoadout.Find(item);
        return saved == null || (item.StackCount == 1 && saved.Inventory != null && saved.MobClassId == device.TargetObjectId);
    }
    // A packaged droid (stock or recovered). Turrets and drones are not droids.
    public static bool IsDroidPackage(BasePickupItem item)
    {
        var device = item?.Record<PlaceableDeviceRecord>();
        return device != null && device.Usage.ToString() == ArPackageMap.AllySpawnUsage && IsDroid(device.TargetObjectId);
    }
    // The three packaged droid models. AllySpawn also includes turrets, drones and creatures.
    public static bool IsDroid(string mobId) => mobId == "cyborg_worker"
        || mobId == "cyborg_battle" || mobId == "cyborg_recreation";
    public static string VariantName(string mobId) => mobId == "cyborg_battle" ? ModText.Get("quasirobotics.variant.combat")
        : mobId == "cyborg_worker" ? ModText.Get("quasirobotics.variant.industrial")
        : mobId == "cyborg_recreation" ? ModText.Get("quasirobotics.variant.recreational") : ModText.Get("quasirobotics.variant.robot");
    public RobotRecord Register(BasePickupItem package, Func<string, CreatureData> generate, Func<Perk> createRank)
    {
        if (EmptySlots == 0) throw new InvalidOperationException("No empty robot slots are available.");
        if (!CanRegister(package)) throw new InvalidOperationException("This package cannot be added to the robot roster.");
        var saved = ArLoadout.Find(package);
        var data = generate(package.Record<PlaceableDeviceRecord>().TargetObjectId);
        if (data?.Inventory == null) throw new InvalidOperationException("The game did not generate a robot inventory.");
        if (saved != null) ArLoadout.Apply(saved, data);
        var identity = new RobotIdentity {
            Id = Guid.NewGuid().ToString("N"), DefaultLocalizationId = data.LocalizationId, Rank = createRank()
        };
        if (identity.Rank?.PerkId != "rank_0") throw new InvalidOperationException("Robot rank must be rank_0.");
        var record = new RobotRecord { Data = data };
        RobotIdentity.Bind(data, identity);
        // Build/validate before consuming. The loadout moves, rather than being duplicated.
        var storage = package.Storage;
        var count = package.StackCount;
        Robots.Add(record);
        try
        {
            if (count > 1) package.StackCount--;
            else { storage.Remove(package); package.Storage = null; }
        }
        catch
        {
            Robots.Remove(record);
            package.StackCount = count;
            if (!storage.Items.Contains(package)) storage.AddItemAndReshuffleOptional(package);
            throw;
        }
        if (saved != null) { saved.Inventory = null; saved.MobClassId = null; }
        return record;
    }
    // Gear comes off first; the robot then goes back to its package, which is
    // dismantled. Returns everything bound for Ship's Cargo. Nothing changes
    // unless the package and its dismantle results were produced.
    public List<BasePickupItem> Dismantle(RobotRecord record, Func<string, BasePickupItem> createPackage,
        Func<BasePickupItem, List<BasePickupItem>> disassemble)
    {
        if (record == null || !Robots.Contains(record)) throw new InvalidOperationException("This robot is not in the roster.");
        var id = record.Identity?.Id;
        if (id != null && DeployedIds.Contains(id)) throw new InvalidOperationException("A deployed robot cannot be dismantled.");
        var package = createPackage(record.Data.MobClassId)
            ?? throw new InvalidOperationException("No package exists for " + record.Data.MobClassId + ".");
        var parts = disassemble(package) ?? new List<BasePickupItem>();
        var cargo = new List<BasePickupItem>();
        foreach (var storage in record.Data.Inventory.AllContainers)
            foreach (var item in storage.Items.ToArray())
            {
                // Implicit items are body parts (fists, arm weapons), not loot.
                if (item == null || item.IsImplicit) continue;
                storage.Remove(item);
                item.Storage = null;
                cargo.Add(item);
            }
        cargo.AddRange(parts);
        Robots.Remove(record);
        if (id != null) { SelectedIds.Remove(id); PendingSpawnIds.Remove(id); }
        return cargo;
    }
    // The only augmentation each droid model can roll (config_items #augmentations).
    public static string WeaponArm(string mobId) => mobId == "cyborg_worker" ? "cyborg_drill"
        : mobId == "cyborg_battle" ? "cyborg_battle_machinegun"
        : mobId == "cyborg_recreation" ? "cyborg_recreation_blade" : null;
    public static CreatureData Generate(string mobId, bool recovered)
    {
        var mode = SingletonMonoBehaviour<SpaceGameMode>.Instance;
        var factory = mode.Get<PerkFactory>();
        var data = CreatureSystem.GenerateMonster(factory, mode.Get<Difficulty>(), mobId, "");
        data.BaseOverallDmgMult = mode.Get<Difficulty>().Preset.EnemyDamageMult;
        data.CreatureAlliance = CreatureAlliance.PlayerAlliance;
        if (!recovered)
        {
            // A stock robot joins bare except for its variant's weapon arm.
            // Natively that arm is a 40% roll (mobclasses AugmentationClasses);
            // here it is always fitted. No other augments, implants, gear or ammo.
            var arm = WeaponArm(mobId);
            if (arm != null) AugmentationSystem.TryApplyGeneratedAugmentation(data, arm);
            AugmentationSystem.ConfigureImplicitEffects(data);
            CreatureSystem.SetBareHandSlot(data);
            CreatureSystem.SetArmStumpSlot(data);
        }
        return data;
    }
}
public sealed class RobotRecord
{
    [Save] public CreatureData Data;
    [Save] public DateTime RepairStart;
    [Save] public DateTime RepairEnd;
    [Save] public bool Destroyed;
    public bool IsRepairing(DateTime now) => RepairEnd > now;
    public string Subtitle(DateTime now) => RobotRoster.VariantName(Data.MobClassId)
        + (IsRepairing(now) ? " — " + ModText.Format("quasirobotics.roster.repairing", FormatHelper.ToLocalizedDaysAndHours(RepairEnd - now)) : "");
    public RobotIdentity Identity => RobotIdentity.Find(Data);
    public string DisplayName => string.IsNullOrEmpty(Identity.Name)
        ? Localization.Get("monster." + Identity.DefaultLocalizationId + ".name") : Identity.Name;
    public Mercenary CreateProxy() => new Mercenary {
        CreatureData = Data, ProfileId = MGSC.Data.MercenaryProfiles.Records.First().Id,
        AgentName = DisplayName, EnabledProgression = false, CurseData = new CurseData()
    };
    public void Rename(string value)
    {
        Identity.Name = RobotIdentity.CleanName(value);
        RobotIdentity.Bind(Data, Identity);
    }
}
// Embedded in CreatureData saves too: future missions/floor transfers retain identity.
public sealed class RobotIdentity
{
    [Save] public string Id;
    [Save] public string Name = "";
    [Save] public string DefaultLocalizationId;
    [Save] public Perk Rank;
    private static readonly ConditionalWeakTable<CreatureData, RobotIdentity> Identities = new ConditionalWeakTable<CreatureData, RobotIdentity>();
    private static readonly ConditionalWeakTable<Perk, RobotIdentity> Ranks = new ConditionalWeakTable<Perk, RobotIdentity>();
    private static readonly Dictionary<string, RobotIdentity> Names = new Dictionary<string, RobotIdentity>();
    public static RobotIdentity Find(CreatureData data) => data != null && Identities.TryGetValue(data, out var value) ? value : null;
    public static bool IsFixedRank(Perk perk) => perk != null && Ranks.TryGetValue(perk, out _);
    public static string CleanName(string value)
    {
        var text = new string((value ?? "").Where(c => !char.IsControl(c) && c != '<' && c != '>').ToArray()).Trim();
        return text.Length <= 48 ? text : text.Substring(0, 48);
    }
    public static void Bind(CreatureData data, RobotIdentity identity)
    {
        Identities.Remove(data);
        Identities.Add(data, identity);
        if (identity.Rank != null) { identity.Rank.ResetExp(); identity.Rank.NextPerkId = string.Empty; identity.Rank.ExpPerAction = 0; Ranks.Remove(identity.Rank); Ranks.Add(identity.Rank, identity); }
        data.LocalizationId = "ar_roster_" + identity.Id;
        Names["monster." + data.LocalizationId + ".name"] = identity;
        Names["monster." + data.LocalizationId + ".desc"] = identity;
    }
    // Name: the robot's own, else its base variant's. Description: the base variant's.
    public static bool TryName(string key, out string name)
    {
        name = null;
        if (key == null || !Names.TryGetValue(key, out var identity)) return false;
        name = key.EndsWith(".desc") ? Localization.Get("monster." + identity.DefaultLocalizationId + ".desc")
            : string.IsNullOrEmpty(identity.Name) ? Localization.Get("monster." + identity.DefaultLocalizationId + ".name") : identity.Name;
        return true;
    }
}
