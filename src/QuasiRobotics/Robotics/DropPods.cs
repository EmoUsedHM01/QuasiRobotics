using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MGSC;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuasiRobotics;

// Drop Pods: a mission active with 2 charges, each back after 48 hours (24 with
// Flat Pack Furniture). Called from Mission Control: every robot left aboard
// gets a random landing spot, the spot is blown open with an FGP Type-7 blast,
// then the robot is placed there once the blast is over.
public static class DropPods
{
    public const int Charges = 2;
    public const int ReloadHours = 48, FlatPackReloadHours = 24;
    // FGP Type-7: its explosion and damage.
    public const string ExplosionItemId = "frag_mine";
    public static int Reload(bool flatPack) => flatPack ? FlatPackReloadHours : ReloadHours;

    public static int Ready(RobotRoster roster, DateTime now)
    {
        if (roster.DropPodCooldowns == null) roster.DropPodCooldowns = new List<DateTime>();
        roster.DropPodCooldowns.RemoveAll(t => t <= now);
        return Math.Max(0, Charges - roster.DropPodCooldowns.Count);
    }
    public static bool Use(RobotRoster roster, DateTime now, bool flatPack)
    {
        if (Ready(roster, now) == 0) return false;
        roster.DropPodCooldowns.Add(now.AddHours(Reload(flatPack)));
        return true;
    }
    public static TimeSpan UntilNext(RobotRoster roster, DateTime now) =>
        Ready(roster, now) > 0 || roster.DropPodCooldowns.Count == 0 ? TimeSpan.Zero : roster.DropPodCooldowns.Min() - now;
    // Robots aboard and fit to fight: not on this mission, not destroyed, not in repair.
    public static List<RobotRecord> Callable(RobotRoster roster, DateTime now, ICollection<string> excluded = null) =>
        roster.Robots.Where(r => r.Identity != null && !roster.DeployedIds.Contains(r.Identity.Id)
            && (excluded == null || !excluded.Contains(r.Identity.Id))
            && !r.Destroyed && !r.IsRepairing(now) && r.Data?.Health != null && r.Data.Health.Alive).ToList();

    // Ship tree status, like the capsule's: charges ready, or time to the next one.
    public static string Status(RobotRoster roster, DateTime now)
    {
        int ready = Ready(roster, now);
        return ready > 0 ? Localization.Get("ui.magnum.ready").SafeFormat(ready.ToString(), Charges.ToString())
            : FormatHelper.ToLocalizedDaysAndHours(UntilNext(roster, now));
    }

    // Robots whose pods are still in the air.
    private static readonly HashSet<string> InFlight = new HashSet<string>();

    public static State DungeonState()
    {
        var mode = SingletonMonoBehaviour<DungeonGameMode>.Instance;
        return mode == null ? null : RobotRosterView.Field<State>(mode, "_state");
    }
    public static bool Allowed(State state)
    {
        var raid = state?.Get<RaidMetadata>();
        if (raid == null || raid.StationVisit || !RoboticsModule.HasDropPods(state.Get<MagnumProgression>())) return false;
        var scenarios = state.Get<DungeonGameMode>()?.Scenarios;
        return scenarios == null || !scenarios.Values.OfType<BaseDungeonScenario>().Any(s => s.ForbidMonsterTransfer);
    }

    // Returns the line Mission Control says back.
    public static string Call(State state)
    {
        if (!Allowed(state)) return ModText.Get("quasirobotics.droppods.unreachable");
        var roster = RobotRoster.For(state.Get<Mercenaries>());
        var now = state.Get<SpaceTime>().Time;
        if (Ready(roster, now) == 0)
            return ModText.Format("quasirobotics.droppods.recharging", FormatHelper.ToLocalizedDaysAndHours(UntilNext(roster, now)));
        var robots = Callable(roster, now, InFlight);
        if (robots.Count == 0) return ModText.Get("quasirobotics.droppods.no_robots");
        var explosion = Data.Items.GetSimpleRecord<PlaceableDeviceRecord>(ExplosionItemId)?.Explosion;
        var record = string.IsNullOrEmpty(explosion) ? null : Data.Explosions.GetRecord(explosion);
        if (record == null) Plugin.Logger.LogWarning("Drop Pods: no " + ExplosionItemId + " explosion found; robots land without a blast.");
        int radius = record?.Parameters.Radius ?? 0;
        var grid = state.Get<MapGrid>();
        var creatures = state.Get<Creatures>();
        var floor = state.Get<ItemsOnFloor>();
        var obstacles = state.Get<MapObstacles>();
        var landings = new List<KeyValuePair<RobotRecord, CellPosition>>();
        var used = new List<CellPosition>();
        foreach (var robot in robots)
            for (int attempt = 0; attempt < 20; attempt++)
            {
                // Clear of the player's blast radius, and of the other pods.
                if (!MapSystem.FindBaronSpawnPos(grid, creatures, floor, obstacles, out var cell, radius + 2, MapSystem.ValidCellRule.Capsule)) break;
                if (used.Any(u => CellPosition.Distance(u, cell) <= 1)) continue;
                used.Add(cell);
                landings.Add(new KeyValuePair<RobotRecord, CellPosition>(robot, cell));
                break;
            }
        if (landings.Count == 0) return ModText.Get("quasirobotics.droppods.no_landing_zone");
        bool flatPack = state.Get<MagnumProgression>().IsPerkPurchased(RobotUpgrades.FlatPackFurniture);
        Use(roster, now, flatPack);
        foreach (var landing in landings) Launch(state, roster, landing.Key, landing.Value, record == null ? null : explosion);
        return landings.Count == 1 ? ModText.Format("quasirobotics.droppods.inbound_one", landings[0].Key.DisplayName)
            : ModText.Format("quasirobotics.droppods.inbound_many", landings.Count);
    }

    private static void Launch(State state, RobotRoster roster, RobotRecord robot, CellPosition cell, string explosion)
    {
        var id = robot.Identity.Id;
        InFlight.Add(id);
        ExplosionEntity blast = null;
        try
        {
            if (explosion != null)
                blast = SingletonMonoBehaviour<ExplosionFactory>.Instance.SpawnExplosion(cell, explosion, mutate: p => { p.IsPlayerFire = true; return p; });
        }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
        if (blast == null) { Land(state, roster, robot, cell); return; }
        blast.OnExplosionEnd += () => Land(state, roster, robot, cell);
        // Played straight away, not queued behind the player's turn.
        blast.ProcessActionPoint();
    }

    private static void Land(State state, RobotRoster roster, RobotRecord robot, CellPosition cell)
    {
        var id = robot.Identity.Id;
        InFlight.Remove(id);
        try
        {
            if (DungeonState() != state || roster.DeployedIds.Contains(id) || !roster.Robots.Contains(robot)) return;
            var spot = FreeCell(state, cell);
            if (spot == null) { Plugin.Logger.LogWarning("Drop Pods: no free cell to land " + robot.DisplayName + "."); return; }
            RobotDeployment.SpawnRobot(state, roster, robot, spot.Value);
            MapSystem.RefreshFogAndMinimap(state.Get<MapController>(), state.Get<Creatures>(), state.Get<ItemsOnFloor>(), state.Get<MapObstacles>(),
                state.Get<MapRenderer>(), state.Get<MapEntities>(), state.Get<MapGrid>(), state.Get<FireController>(), state.Get<Visibilities>());
        }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }

    // The pod's cell, or the nearest free one if something moved in.
    private static CellPosition? FreeCell(State state, CellPosition cell)
    {
        var creatures = state.Get<Creatures>();
        var entities = state.Get<MapEntities>();
        var free = state.Get<MapGrid>().BreadthFirstSearch(cell, c => true, 5)
            .Where(c => c.IsFloor && !c.IsObjBlockPass && !c.IsDoor && creatures.GetCreature(c.X, c.Y) == null && entities.Get(c.X, c.Y) == null)
            .OrderBy(c => CellPosition.Distance(c.Position, cell)).FirstOrDefault();
        return free?.Position;
    }
}

// The "Call Drop Pods" option in Mission Control: a copy of the capsule button,
// under it (or in its place while the capsule is not available).
public static class DropPodsButton
{
    public const string Name = "QuasiRobotics_DropPodsButton";
    // The mod's own game key (see DropPodsKeybind): the game shows its bind on
    // the button and presses the button when it is hit.
    public const string KeyId = DropPodsKeybind.KeyId;
    public static string Caption => ModText.Get("gamekey." + DropPodsKeybind.KeyId + ".desc");

    public static CommonButton Find(ManageRaidScreen screen)
    {
        var capsule = RobotRosterView.Field<CommonButton>(screen, "_capsuleButton");
        return capsule == null ? null : capsule.transform.parent.Find(Name)?.GetComponent<CommonButton>();
    }
    private static CommonButton Create(ManageRaidScreen screen)
    {
        var capsule = RobotRosterView.Field<CommonButton>(screen, "_capsuleButton");
        // Built under an inactive holder so it never enables with the capsule's key.
        var holder = new GameObject(Name + "_holder");
        holder.SetActive(false);
        var copy = Object.Instantiate(capsule.gameObject, holder.transform, false);
        copy.name = Name;
        var button = copy.GetComponent<CommonButton>();
        if (button is HotkeyButton) AccessTools.Field(typeof(HotkeyButton), "_keyId").SetValue(button, KeyId);
        foreach (var panel in copy.GetComponentsInChildren<GameKeyPanel>(true))
            AccessTools.Field(typeof(GameKeyPanel), "_keyId").SetValue(panel, KeyId);
        copy.transform.SetParent(capsule.transform.parent, false);
        copy.transform.SetSiblingIndex(capsule.transform.GetSiblingIndex() + 1);
        Object.Destroy(holder);
        button.OnClick += (b, clicks) => Click(screen);
        return button;
    }

    public static void Refresh(ManageRaidScreen screen)
    {
        var state = DropPods.DungeonState();
        var button = Find(screen);
        bool show = DropPods.Allowed(state);
        if (!show) { if (button != null) button.gameObject.SetActive(false); return; }
        if (button == null) button = Create(screen);
        var roster = RobotRoster.For(state.Get<Mercenaries>());
        int ready = DropPods.Ready(roster, state.Get<SpaceTime>().Time);
        button.gameObject.SetActive(true);
        // The copied caption still holds the capsule's localization key, which
        // it would re-apply on Start or a language change: clear it first.
        if (button.CaptionLabel != null) AccessTools.Field(typeof(LocalizableLabel), "_label").SetValue(button.CaptionLabel, string.Empty);
        button.SetRawCaption(ModText.Format("quasirobotics.droppods.button", Caption, ready, DropPods.Charges));
        Place(screen, button);
    }

    // With a layout group the sibling order does it; otherwise one row under the capsule.
    private static void Place(ManageRaidScreen screen, CommonButton button)
    {
        var capsule = RobotRosterView.Field<CommonButton>(screen, "_capsuleButton");
        var parent = capsule.transform.parent as RectTransform;
        if (parent != null && parent.GetComponent<LayoutGroup>() != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
            return;
        }
        var own = (RectTransform)button.transform;
        var source = (RectTransform)capsule.transform;
        if (!capsule.gameObject.activeSelf) { own.anchoredPosition = source.anchoredPosition; return; }
        var evacuation = (RectTransform)RobotRosterView.Field<CommonButton>(screen, "_evacuationButton").transform;
        float step = Mathf.Abs(evacuation.anchoredPosition.y - source.anchoredPosition.y);
        if (step < 1f) step = source.rect.height + 8f;
        own.anchoredPosition = source.anchoredPosition - new Vector2(0f, step);
    }

    public static void Click(ManageRaidScreen screen)
    {
        string message;
        try { message = DropPods.Call(DropPods.DungeonState()); }
        catch (Exception ex) { Plugin.Logger.LogException(ex); message = "Drop Pods failed to launch."; }
        RobotRosterView.Field<TextBlob>(screen, "_text")?.Show(message);
        Refresh(screen);
    }
}

[HarmonyPatch(typeof(ManageRaidScreen), "OnEnable")]
public static class DropPodsButtonPatch
{
    [HarmonyPostfix]
    public static void Postfix(ManageRaidScreen __instance)
    {
        try { DropPodsButton.Refresh(__instance); }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
}
// The capsule refreshes its own button when used; keep ours in place after it.
[HarmonyPatch(typeof(ManageRaidScreen), "CapsuleButtonOnClick")]
public static class DropPodsCapsulePatch
{
    [HarmonyPostfix]
    public static void Postfix(ManageRaidScreen __instance)
    {
        try { DropPodsButton.Refresh(__instance); }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
}
