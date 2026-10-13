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

public sealed class RobotDepartureView : MonoBehaviour
{
    private PrepareRaidScreen screen;
    private RectTransform section;
    private readonly List<GameObject> rows = new List<GameObject>();
    private Mercenaries Owner => SingletonMonoBehaviour<SpaceGameMode>.Instance.Get<Mercenaries>();
    private SpaceTime Time => SingletonMonoBehaviour<SpaceGameMode>.Instance.Get<SpaceTime>();
    private RobotRoster Roster => RobotRoster.For(Owner);
    private CommonButton Template => RobotRosterView.Field<CommonButton>(UI.Get<MercenariesScreen>(), "_backButton");
    private static T Field<T>(object target, string name) => RobotRosterView.Field<T>(target, name);

    public void ResetSelection()
    {
        Roster.SelectedIds.Clear();
        if (section != null && section.gameObject.activeSelf && screen.isActiveAndEnabled) Render();
    }
    public void Refresh(PrepareRaidScreen view)
    {
        screen = view;
        if (!RoboticsModule.HasPrintyard())
        {
            // No Printyard, no robots on missions.
            Roster.SelectedIds.Clear();
                if (section != null)
            {
                section.gameObject.SetActive(false);
                LayoutRebuilder.ForceRebuildLayoutImmediate(Field<ScrollRect>(screen, "_scrollRect").content);
            }
            return;
        }
        if (section != null) section.gameObject.SetActive(true);
        if (section == null)
        {
            var content = Field<ScrollRect>(screen, "_scrollRect").content;
            var operatorHeader = content.Find("MercenaryBlock/OperatorLabel");
            var details = content.Find("DetailsBlock");
            var go = new GameObject("ARRobotsDeparture", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(LayoutElement));
            section = (RectTransform)go.transform;
            section.SetParent(content, false);
            section.SetSiblingIndex(details.GetSiblingIndex());
            section.sizeDelta = new Vector2(262, 12);
            var group = go.GetComponent<VerticalLayoutGroup>();
            group.childControlWidth = true; group.childControlHeight = true;
            group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            group.spacing = 2;
            var header = Object.Instantiate(operatorHeader.gameObject, section);
            header.name = "RobotsHeader";
            foreach (var localizer in header.GetComponentsInChildren<LocalizableLabel>(true))
            { localizer.enabled = false; Object.Destroy(localizer); }
            var caption = header.GetComponentInChildren<TextMeshProUGUI>(true);
            caption.richText = true;
            caption.text = ModText.Get(ModText.Robots).ColorFirstLetter(Colors.White);
            var layout = header.GetComponent<LayoutElement>() ?? header.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 12;
        }
        Render();
    }
    private CommonButton Action(string text, Action click)
    {
        var button = RobotRosterView.CreateTextButton(Template, section, text, click);
        rows.Add(button.gameObject);
        return button;
    }
    private void Render()
    {
        foreach (var row in rows) { row.SetActive(false); Object.Destroy(row); }
        rows.Clear();
        var roster = Roster;
        roster.SelectedIds.RemoveAll(id => !roster.Robots.Any(r => r.Identity.Id == id && !r.IsRepairing(Time.Time)));
        if (roster.SelectedIds.Count > Math.Max(0, roster.Capacity))
            roster.SelectedIds.RemoveRange(Math.Max(0, roster.Capacity), roster.SelectedIds.Count - Math.Max(0, roster.Capacity));
        foreach (var robot in roster.Robots.Where(r => roster.SelectedIds.Contains(r.Identity.Id))) { RobotUpgrades.SyncHealth(robot.Data); AddRow(robot); }
        if (roster.SelectedIds.Count < Math.Max(0, roster.Capacity))
            Action(ModText.Get("quasirobotics.departure.add_robot"), () => RobotPicker.Open(robot => RobotDeployment.Select(Roster, robot, Time.Time)));
        var layout = section.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = 12 + rows.Sum(r => r.GetComponent<LayoutElement>().preferredHeight + 2);
        // The departure content lays out children using their actual rect heights.
        // Match the native Details/Description blocks: preferredHeight alone leaves
        // this section at its initial 12-unit header height and overlaps Briefing.
        section.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, layout.preferredHeight);
        LayoutRebuilder.ForceRebuildLayoutImmediate(section);
        LayoutRebuilder.ForceRebuildLayoutImmediate(Field<ScrollRect>(screen, "_scrollRect").content);
        Field<CommonScrollBar>(screen, "_scrollBar").UpdateVisualDeferred();
    }
    private void AddRow(RobotRecord record)
    {
        var row = new GameObject("ARRobotDepartureRow", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(section, false);
        row.GetComponent<LayoutElement>().minHeight = row.GetComponent<LayoutElement>().preferredHeight = 24;
        ((RectTransform)row.transform).sizeDelta = new Vector2(262, 24);
        rows.Add(row);
        var pool = Field<Pool>(UI.Get<MercenariesScreen>(), "_panelsPool");
        var panel = RobotRosterView.CreateRobotRow(record, pool.prefab, row.transform, Time,
            () => OpenEquipment(record), () => OpenAugments(record));
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = new Vector2(-24, 0);
        var remove = RobotRosterView.CreateCrossButton(Template, row.transform, () => {
            Roster.SelectedIds.Remove(record.Identity.Id); Render();
        });
        remove.name = "ARRemoveFromDeparture";
    }
    private void OpenEquipment(RobotRecord record)
    {
        UI.Chain<ArsenalScreen>().Invoke(view => {
            view.Configure(record.CreateProxy(), false);
            ArEquipmentStorage.Enable(record.Data.Inventory, view); RobotGear.Track(record.Data.Inventory); view.RefreshView();
        }).HideAll().Show().Fallback<PrepareRaidScreen>();
    }
    private void OpenAugments(RobotRecord record)
    {
        UI.Chain<AugmentationScreen>().Invoke(view => {
            view.Configure(record.CreateProxy());
            ArEquipmentStorage.Enable(record.Data.Inventory, view); RobotGear.Track(record.Data.Inventory); view.RefreshView();
        }).HideAll().Show().Fallback<PrepareRaidScreen>();
    }
}
[HarmonyPatch(typeof(PrepareRaidScreen), "OnEnable")]
public static class RobotDepartureScreenPatch
{
    [HarmonyPostfix] public static void Postfix(PrepareRaidScreen __instance)
    {
        RobotPicker.Cancel();
        try { (__instance.GetComponent<RobotDepartureView>() ?? __instance.gameObject.AddComponent<RobotDepartureView>()).Refresh(__instance); }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
}
[HarmonyPatch(typeof(PrepareRaidScreen), nameof(PrepareRaidScreen.Configure), typeof(Mission), typeof(bool))]
public static class RobotDepartureConfigurePatch
{
    // Change side re-configures the same mission: keep the chosen robots.
    public static bool SwappingSide;
    [HarmonyPostfix] public static void Postfix(PrepareRaidScreen __instance)
    {
        if (SwappingSide) return;
        var mode = SingletonMonoBehaviour<SpaceGameMode>.Instance;
        RobotRoster.For(mode.Get<Mercenaries>()).SelectedIds.Clear();
        __instance.GetComponent<RobotDepartureView>()?.ResetSelection();
    }
}

[HarmonyPatch(typeof(PrepareRaidScreen), nameof(PrepareRaidScreen.Configure), typeof(Station))]
public static class RobotDepartureStationConfigurePatch
{
    [HarmonyPostfix] public static void Postfix(PrepareRaidScreen __instance) => RobotDepartureConfigurePatch.Postfix(__instance);
}

[HarmonyPatch(typeof(PrepareRaidScreen), "SwapSideToDefenseButtonOnClick")]
public static class RobotDepartureSwapToDefensePatch
{
    [HarmonyPrefix] public static void Prefix() => RobotDepartureConfigurePatch.SwappingSide = true;
    [HarmonyFinalizer] public static void Finalizer() => RobotDepartureConfigurePatch.SwappingSide = false;
}

[HarmonyPatch(typeof(PrepareRaidScreen), "SwapSideToAttackButtonOnClick")]
public static class RobotDepartureSwapToAttackPatch
{
    [HarmonyPrefix] public static void Prefix() => RobotDepartureConfigurePatch.SwappingSide = true;
    [HarmonyFinalizer] public static void Finalizer() => RobotDepartureConfigurePatch.SwappingSide = false;
}
