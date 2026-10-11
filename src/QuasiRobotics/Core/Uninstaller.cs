using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MGSC;
using QuasiRobotics.Packages;

namespace QuasiRobotics;

// Uninstall mode (Mod Config Menu). Saving on the ship turns every piece of
// QuasiRobotics data back into vanilla game data, so the save loads without
// the mod: robots return as stock packages, recovered packages lose their
// loadout, and Robotics devices are removed with their price refunded.
public static class Uninstaller
{
    // True only while a cleaned ship save is being written.
    public static bool Active { get; private set; }
    public static bool Enabled => Plugin.Settings?.UninstallMode == true;

    public static void Begin(State state)
    {
        Active = false;
        if (!Enabled || state == null) return;
        if (state.Get<DungeonGameMode>() != null)
        {
            Plugin.Logger.LogWarning("Uninstall mode is on, but this save is in a mission. Save on the ship to remove QuasiRobotics data.");
            return;
        }
        var cargo = new List<BasePickupItem>();
        var mercs = state.Get<Mercenaries>();
        var magnumCargo = state.Get<MagnumCargo>();
        int robots = 0, packages = 0;
        if (mercs != null)
        {
            var roster = RobotRoster.For(mercs);
            robots = roster.Robots.Count;
            cargo.AddRange(UnwindRoster(roster, mob => ArPackageCatalogue.TryGetPackageId(mob, out var id) ? CreateItem(id) : null));
            foreach (var merc in mercs.Values)
                packages += StripStorages(merc?.CreatureData?.Inventory?.AllContainers, cargo);
        }
        if (magnumCargo != null)
            packages += StripStorages(magnumCargo.ShipCargo.Concat(new[] { magnumCargo.RecyclingStorage, magnumCargo.FridgeStorage }), cargo);
        foreach (var id in RemoveDevices(state.Get<MagnumProgression>()))
            cargo.AddRange(RoboticsModule.PriceOf(id).Select(CreateItem).Where(i => i != null));
        if (magnumCargo != null)
        {
            var time = state.Get<SpaceTime>();
            foreach (var item in cargo)
                MagnumCargoSystem.AddCargo(magnumCargo, time, item, null, splittedItem: false, tabFilter: true);
        }
        Active = true;
        Plugin.Logger.Log("Uninstall mode: returned " + robots + " robot(s) and cleaned " + packages
            + " recovered package(s). This save can now load without QuasiRobotics.");
    }

    public static void End() => Active = false;

    private static BasePickupItem CreateItem(string id) => SingletonMonoBehaviour<ItemFactory>.Instance.CreateForInventory(id);

    // Every roster robot goes back to a stock package, with its gear beside it.
    public static List<BasePickupItem> UnwindRoster(RobotRoster roster, Func<string, BasePickupItem> createPackageForMob)
    {
        var items = new List<BasePickupItem>();
        roster.DeployedIds.Clear();
        foreach (var record in roster.Robots.ToArray())
            items.AddRange(roster.Dismantle(record, createPackageForMob, package => new List<BasePickupItem> { package }));
        roster.Robots.Clear(); roster.SelectedIds.Clear(); roster.PendingSpawnIds.Clear();
        return items;
    }

    public static int StripStorages(IEnumerable<ItemStorage> storages, List<BasePickupItem> cargo)
    {
        int count = 0;
        foreach (var storage in storages ?? Enumerable.Empty<ItemStorage>())
            foreach (var item in storage?.Items.ToArray() ?? Array.Empty<BasePickupItem>())
                if (StripLoadout(item, cargo)) count++;
        return count;
    }

    // A recovered package becomes a stock package; its stored gear goes to cargo.
    public static bool StripLoadout(BasePickupItem item, List<BasePickupItem> cargo)
    {
        if (!(item is PickupItem pickup)) return false;
        var components = (List<PickupItemComponent>)AccessTools.Field(typeof(PickupItem), "_components").GetValue(pickup);
        var loadouts = components.OfType<ArLoadoutComponent>().ToList();
        if (loadouts.Count == 0) return false;
        foreach (var loadout in loadouts)
        {
            foreach (var storage in loadout.Inventory?.AllContainers ?? new List<ItemStorage>())
                foreach (var stored in storage.Items.ToArray())
                {
                    if (stored == null || stored.IsImplicit) continue;
                    storage.Remove(stored);
                    stored.Storage = null;
                    cargo?.Add(stored);
                }
            components.Remove(loadout);
        }
        return true;
    }

    public static List<string> RemoveDevices(MagnumProgression progression) =>
        RoboticsModule.DeviceIds.Where(id => RemoveDevice(progression, id)).ToList();
    public static bool RemovePrintyard(MagnumProgression progression) => RemoveDevice(progression, RoboticsModule.PrintyardId);
    public static bool RemoveDevice(MagnumProgression progression, string deviceId)
    {
        if (progression == null) return false;
        var perks = (List<string>)AccessTools.Field(typeof(MagnumProgression), "_purchasedPerks").GetValue(progression);
        var departments = (List<MagnumDepartment>)AccessTools.Field(typeof(MagnumProgression), "_departments").GetValue(progression);
        var idField = AccessTools.Field(typeof(MagnumDepartment), "_departmentId");
        departments.RemoveAll(d => (string)idField.GetValue(d) == deviceId);
        return perks.Remove(deviceId);
    }
}
