using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MGSC;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuasiRobotics;

// "Add Robot" reuses the native Select Operator window: same frame, header and
// back button, filled with roster robots instead of operators.
public static class RobotPicker
{
    private const string SelectMercCaption = "ui.caption.selectmerc";
    private const string OperatorsLabel = "ui.label.operators";
    private static Action<RobotRecord> picked;
    private static readonly List<GameObject> rows = new List<GameObject>();
    private static readonly List<KeyValuePair<LocalizableLabel, string>> swapped = new List<KeyValuePair<LocalizableLabel, string>>();
    private static bool loggedLabels;

    public static bool Active => picked != null;
    private static T Field<T>(object target, string name) => RobotRosterView.Field<T>(target, name);

    public static void Open(Action<RobotRecord> onPicked)
    {
        UI.Chain<SelectMercenaryScreen>().Invoke(view => {
            view.Configure(SelectMercenaryScreen.Filter.SelectForMission, null);
            picked = onPicked;
        }).HideAll().Show().Fallback<PrepareRaidScreen>();
    }
    public static void Cancel() => picked = null;

    public static void Clear()
    {
        foreach (var row in rows) if (row != null) { row.SetActive(false); Object.Destroy(row); }
        rows.Clear();
        foreach (var pair in swapped) if (pair.Key != null) pair.Key.ChangeLabel(pair.Value);
        swapped.Clear();
    }

    public static void Fill(SelectMercenaryScreen screen)
    {
        AccessTools.Method(typeof(SelectMercenaryScreen), "ClearPanels").Invoke(screen, null);
        var root = Field<Transform>(screen, "_panelsRoot");
        var empty = Field<GameObject>(screen, "_noAvailableMercsPanel");
        SwapLabels(screen, root, empty);
        var mode = SingletonMonoBehaviour<SpaceGameMode>.Instance;
        var time = mode.Get<SpaceTime>();
        var roster = RobotRoster.For(mode.Get<Mercenaries>());
        var prefab = Field<Pool>(screen, "_panelsPool").prefab;
        foreach (var robot in roster.Robots.Where(r => !roster.SelectedIds.Contains(r.Identity.Id)).ToList())
            rows.Add(CreateRow(robot, prefab, root, time).gameObject);
        empty.SetActive(rows.Count == 0);
        Field<ScrollRect>(screen, "_scrollRect").normalizedPosition = Vector2.up;
        if (rows.Count > 0) UI.Navigation.Select(rows[0].GetComponent<MercenaryPanel>());
    }

    private static void SwapLabels(SelectMercenaryScreen screen, Transform root, GameObject empty)
    {
        foreach (var label in screen.GetComponentsInChildren<LocalizableLabel>(true))
        {
            if (label.transform.IsChildOf(root)) continue;
            if (!loggedLabels) Plugin.Logger.Log("Robot picker label: " + label.name + " = " + label.Label);
            string key = label.Label == SelectMercCaption ? ModText.SelectRobot
                : label.Label == OperatorsLabel ? ModText.Robots
                : empty != null && label.transform.IsChildOf(empty.transform) ? ModText.NoRobots : null;
            if (key == null) continue;
            swapped.Add(new KeyValuePair<LocalizableLabel, string>(label, label.Label));
            label.ChangeLabel(key);
        }
        loggedLabels = true;
    }

    private static MercenaryPanel CreateRow(RobotRecord record, GameObject prefab, Transform root, SpaceTime spaceTime)
    {
        var panel = Object.Instantiate(prefab, root, false).GetComponent<MercenaryPanel>();
        panel.name = "ARPickRobot_" + record.Identity.Id;
        panel.transform.SetAsLastSibling();
        panel.gameObject.SetActive(true);
        var proxy = record.CreateProxy();
        panel.Initialize(spaceTime, proxy, Data.MercenaryProfiles.GetRecord(proxy.ProfileId), MercenaryPanel.Mode.PanelSelectable);
        RobotRosterView.SetRobotHeadIcon(Field<Image>(panel, "_rankIcon"), record.Data);
        var name = Field<TextMeshProUGUI>(panel, "_mercName");
        name.text = record.DisplayName; name.richText = false;
        name.enableWordWrapping = false; name.overflowMode = TextOverflowModes.Ellipsis;
        var subtitle = Field<TextMeshProUGUI>(panel, "_mercClass");
        subtitle.text = record.Subtitle(spaceTime.Time);
        subtitle.enableWordWrapping = false; subtitle.overflowMode = TextOverflowModes.Ellipsis;
        Field<MercenaryTooltipHandler>(panel, "_mercenaryTooltipHandler").enabled = false;
        var rankTooltip = AccessTools.Field(typeof(MercenaryPanel), "_rankTooltipHandler")?.GetValue(panel) as Behaviour;
        if (rankTooltip != null) rankTooltip.enabled = false;
        Field<MercenaryClassIcon>(panel, "_classIcon").gameObject.SetActive(false);
        panel.OnSelected += _ => Pick(record, spaceTime);
        UI.Navigation.RegisterElement(panel);
        return panel;
    }

    private static void Pick(RobotRecord record, SpaceTime spaceTime)
    {
        if (record.IsRepairing(spaceTime.Time) || picked == null)
        {
            SingletonMonoBehaviour<SoundController>.Instance.PlayUiSound(SingletonMonoBehaviour<SoundsStorage>.Instance.EmptyAttack);
            return;
        }
        SingletonMonoBehaviour<SoundController>.Instance.PlayUiSound(SingletonMonoBehaviour<SoundsStorage>.Instance.ButtonClick);
        var callback = picked;
        picked = null;
        callback(record);
        UI.Back();
    }
}

[HarmonyPatch(typeof(SelectMercenaryScreen), nameof(SelectMercenaryScreen.Configure))]
public static class RobotPickerConfigurePatch
{
    // Every native caller configures the window first: that ends robot mode.
    [HarmonyPrefix] public static void Prefix() => RobotPicker.Cancel();
}

[HarmonyPatch(typeof(SelectMercenaryScreen), "OnEnable")]
public static class RobotPickerScreenPatch
{
    [HarmonyPrefix] public static bool Prefix(SelectMercenaryScreen __instance)
    {
        try
        {
            RobotPicker.Clear();
            if (!RobotPicker.Active) return true;
            RobotPicker.Fill(__instance);
            return false;
        }
        catch (Exception ex) { Plugin.Logger.LogException(ex); RobotPicker.Cancel(); return true; }
    }
}
