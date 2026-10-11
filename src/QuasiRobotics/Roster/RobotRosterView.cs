using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MGSC;
using QuasiRobotics.Packages;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace QuasiRobotics;

public sealed class RobotRosterView : MonoBehaviour
{
    private MercenariesScreen screen;
    private bool robots, picking;
    private Transform rowsRoot;
    private CommonButton operatorsTab, robotsTab;
    private GameObject tabsRoot, nativeSubtitle;
    private readonly List<GameObject> rows = new List<GameObject>();
    private string error;
    public static T Field<T>(object target, string name) => (T)AccessTools.Field(target.GetType(), name).GetValue(target);
    private RobotRoster Roster => RobotRoster.For(Field<Mercenaries>(screen, "_mercenaries"));
    private List<MercenaryPanel> NativePanels => Field<List<MercenaryPanel>>(screen, "_panels");

    public void Refresh(MercenariesScreen view)
    {
        screen = view;
        rowsRoot = Field<Transform>(screen, "_panelsRoot");
        bool unlocked = RoboticsModule.HasPrintyard();
        if (!unlocked) { robots = false; picking = false; error = null; }
        if (unlocked && operatorsTab == null) CreateTabs();
        // Without Printyard the screen is the native operators list.
        if (tabsRoot != null) tabsRoot.SetActive(unlocked);
        if (nativeSubtitle != null) nativeSubtitle.SetActive(!unlocked);
        Render();
    }
    private static void CopyRect(RectTransform from, RectTransform to)
    {
        to.anchorMin = from.anchorMin; to.anchorMax = from.anchorMax;
        to.pivot = from.pivot; to.sizeDelta = from.sizeDelta;
        to.anchoredPosition = from.anchoredPosition; to.localScale = from.localScale;
        to.localRotation = from.localRotation;
    }
    private void CreateTabs()
    {
        // Match the subtitle itself, never the Manage Operators window caption.
        var label = screen.GetComponentsInChildren<LocalizableLabel>(true).FirstOrDefault(l =>
            l.Text != null && (l.Text.text.Trim().Equals("Operators", StringComparison.OrdinalIgnoreCase)
                || l.Label == "ui.label.mercenaries" || l.Label == "ui.label.operators"));
        var subtitle = label?.Text ?? screen.GetComponentsInChildren<TextMeshProUGUI>(true)
            .FirstOrDefault(t => t.text.Trim().Equals("Operators", StringComparison.OrdinalIgnoreCase));
        if (subtitle == null) throw new InvalidOperationException("The Operators subtitle was not found.");
        var root = new GameObject("ARRosterTabs", typeof(RectTransform));
        tabsRoot = root;
        nativeSubtitle = subtitle.gameObject;
        root.transform.SetParent(subtitle.transform.parent, false);
        // The subtitle text is only 130 units wide; HeaderBlock is the full 262-unit bar.
        var tabRect = (RectTransform)root.transform;
        tabRect.anchorMin = Vector2.zero; tabRect.anchorMax = Vector2.one;
        tabRect.offsetMin = Vector2.zero; tabRect.offsetMax = Vector2.zero;
        operatorsTab = Tab(root.transform, subtitle, Localization.Get("ui.label.operators"), 0, () => Switch(false));
        robotsTab = Tab(root.transform, subtitle, ModText.Get(ModText.Robots), .5f, () => Switch(true));
        var divider = new GameObject("Divider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(root.transform, false);
        var line = (RectTransform)divider.transform;
        line.anchorMin = new Vector2(.5f, 0); line.anchorMax = new Vector2(.5f, 1);
        line.sizeDelta = new Vector2(1, 0); line.anchoredPosition = Vector2.zero;
        divider.GetComponent<Image>().color = Colors.DarkGreen;
        divider.GetComponent<Image>().raycastTarget = false;
        subtitle.gameObject.SetActive(false);
    }
    private CommonButton Tab(Transform parent, TextMeshProUGUI style, string title, float left, Action click)
    {
        var go = new GameObject(title, typeof(RectTransform));
        go.SetActive(false);
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(left, 0); rect.anchorMax = new Vector2(left + .5f, 1);
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        var background = go.AddComponent<Image>();
        background.color = new Color(.08f, .20f, .14f, .5f);
        var text = Object.Instantiate(style, go.transform);
        foreach (var localizer in text.GetComponents<LocalizableLabel>()) { localizer.enabled = false; Object.Destroy(localizer); }
        text.gameObject.SetActive(true);
        text.text = title;
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(4, 0); text.rectTransform.offsetMax = new Vector2(-4, 0);
        text.raycastTarget = false;
        var button = go.AddComponent<CommonButton>();
        button.background = background; button.captionText = text;
        button.normalCaptionColor = Colors.AltGreen; button.hoverCaptionColor = Colors.Yellow;
        button.pressedCaptionColor = Colors.Yellow;
        button.OnClick += (b, n) => click();
        go.SetActive(true);
        return button;
    }
    private void Switch(bool value) { robots = value; picking = false; error = null; Render(); }
    private void ClearRows()
    {
        foreach (var row in rows) { row.SetActive(false); Object.Destroy(row); }
        rows.Clear();
    }
    private void OnDisable() { ClearRows(); }
    private void Render()
    {
        ClearRows();
        foreach (var panel in NativePanels)
        {
            panel.gameObject.SetActive(!robots);
            if (!robots)
            {
                var merc = Field<Mercenary>(panel, "_mercenary");
                panel.Initialize(Field<SpaceTime>(screen, "_spaceTime"), merc, Data.MercenaryProfiles.GetRecord(merc.ProfileId), MercenaryPanel.Mode.StateIconsSelectable);
                UI.Navigation.RegisterElement(panel);
            }
        }
        Field<GameObject>(screen, "_noAvailableMercsPanel").SetActive(!robots && NativePanels.Count == 0);
        bool tabs = operatorsTab != null && tabsRoot.activeSelf;
        if (tabs)
        {
            operatorsTab.normalCaptionColor = operatorsTab.captionText.color = robots ? Colors.AltGreen : Colors.Yellow;
            robotsTab.normalCaptionColor = robotsTab.captionText.color = robots ? Colors.Yellow : Colors.AltGreen;
            UI.Navigation.RegisterElement(operatorsTab);
            UI.Navigation.RegisterElement(robotsTab);
        }
        if (robots)
        {
            if (Roster.EmptySlots == 0) picking = false;
            if (picking) TextButton(ModText.Get("quasirobotics.roster.back"), () => { picking = false; error = null; Render(); });
            if (!string.IsNullOrEmpty(error)) TextButton(error, null);
            if (picking)
            {
                var candidates = Field<MagnumCargo>(screen, "_magnumCargo").ShipCargo
                    .SelectMany(s => s.Items).Where(RobotRoster.CanRegister).ToList();
                foreach (var package in candidates)
                {
                    var device = package.Record<PlaceableDeviceRecord>();
                    string name = Localization.Get("monster." + device.TargetObjectId + ".name");
                    TextButton(package.StackCount > 1 ? ModText.Format("quasirobotics.roster.add_package_stack", name, package.StackCount) : ModText.Format("quasirobotics.roster.add_package", name), () => Register(package));
                }
                if (candidates.Count == 0) TextButton(ModText.Get("quasirobotics.roster.no_packages"), null);
            }
            else
            {
                foreach (var record in Roster.Robots) { RobotUpgrades.SyncHealth(record.Data); RobotRow(record); }
                for (int slot = 0; slot < Roster.EmptySlots; slot++)
                    TextButton(ModText.Get("quasirobotics.roster.empty_slot"), () => { picking = true; error = null; Render(); });
            }
        }
        Field<ScrollRect>(screen, "_scrollRect").normalizedPosition = Vector2.up;
        if (tabs) UI.Navigation.Select(robots ? robotsTab : operatorsTab);
    }
    private void Register(BasePickupItem package)
    {
        try
        {
            bool recovered = ArLoadout.Find(package) != null;
            Roster.Register(package, id => RobotRoster.Generate(id, recovered),
                () => SingletonMonoBehaviour<SpaceGameMode>.Instance.Get<PerkFactory>().CreatePerk(Data.Perks.GetRecord("rank_0")));
            picking = false; error = null;
        }
        catch (Exception ex)
        {
            error = ModText.Get("quasirobotics.roster.add_failed");
            Plugin.Logger.LogException(ex);
        }
        Render();
    }
    private CommonButton TextButton(string text, Action click)
    {
        var button = CreateTextButton(Field<CommonButton>(screen, "_backButton"), rowsRoot, text, click);
        rows.Add(button.gameObject);
        return button;
    }
    public static CommonButton CreateTextButton(CommonButton template, Transform rowsRoot, string text, Action click)
    {
        var button = Object.Instantiate(template, rowsRoot);
        button.name = "ARRosterAction";
        foreach (var key in button.GetComponentsInChildren<GameKeyPanel>(true)) key.gameObject.SetActive(false);
        AccessTools.Field(typeof(CommonButton), "OnClick").SetValue(button, (Action<CommonButton, int>)((b, n) => click?.Invoke()));
        var caption = button.captionText ?? button.CaptionLabel?.Text;
        // SetRawCaption does not clear LocalizableLabel._label: its Start and
        // language-change callbacks would restore the cloned Back caption.
        button.SetRawCaption(text);
        foreach (var localizer in button.GetComponentsInChildren<LocalizableLabel>(true))
        {
            localizer.enabled = false;
            Object.Destroy(localizer);
        }
        AccessTools.Field(typeof(CommonButton), "_captionLabel").SetValue(button, null);
        button.captionText = caption;
        if (caption != null)
        {
            caption.text = text;
            caption.enableWordWrapping = false;
            caption.overflowMode = TextOverflowModes.Ellipsis;
            caption.rectTransform.anchorMin = Vector2.zero; caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.offsetMin = new Vector2(4, 0); caption.rectTransform.offsetMax = new Vector2(-4, 0);
        }
        var rect = (RectTransform)button.transform;
        rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
        rect.anchorMax = rect.anchorMin;
        rect.sizeDelta = new Vector2(((RectTransform)rowsRoot).rect.width, 22);
        rect.anchoredPosition = Vector2.zero;
        var layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = rect.sizeDelta.y;
        layout.flexibleWidth = 1;
        button.SetInteractable(click != null);
        button.gameObject.SetActive(true);
        if (click != null) UI.Navigation.RegisterElement(button);
        return button;
    }
    private void RobotRow(RobotRecord record)
    {
        var pool = Field<Pool>(screen, "_panelsPool");
        float height = ((RectTransform)pool.prefab.transform).rect.height;
        if (height <= 0) height = 24;
        // Wrapper row: the native panel on the left, the Dismantle cross on the right.
        var row = new GameObject("ARRobotRosterRow", typeof(RectTransform), typeof(LayoutElement));
        row.transform.SetParent(rowsRoot, false);
        var layout = row.GetComponent<LayoutElement>();
        layout.minHeight = layout.preferredHeight = height;
        layout.flexibleWidth = 1;
        ((RectTransform)row.transform).sizeDelta = new Vector2(((RectTransform)rowsRoot).rect.width, height);
        rows.Add(row);
        var panel = CreateRobotRow(record, pool.prefab, row.transform, Field<SpaceTime>(screen, "_spaceTime"),
            () => OpenEquipment(record), () => OpenAugments(record));
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = new Vector2(-24, 0);
        var dismantle = CreateCrossButton(Field<CommonButton>(screen, "_backButton"), row.transform, () => ConfirmDismantle(record));
        dismantle.name = "ARDismantleRobot";
    }
    // Native BigCrossButton artwork; shared by the roster and departure rows.
    public static CommonButton CreateCrossButton(CommonButton template, Transform parent, Action click)
    {
        var button = CreateTextButton(template, parent, "", click);
        var rect = (RectTransform)button.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(1, .5f);
        rect.pivot = new Vector2(1, .5f);
        rect.sizeDelta = new Vector2(22, 22); rect.anchoredPosition = Vector2.zero;
        var layout = button.GetComponent<LayoutElement>();
        if (layout != null) layout.ignoreLayout = true;
        button.captionText.gameObject.SetActive(false);
        button.normalBgSprite = RobotButtonSprites.Get("BigCrossButton_Normal");
        button.hoverBgSprite = RobotButtonSprites.Get("BigCrossButton_Hover");
        button.pressedBgSprite = button.hoverBgSprite;
        button.disabledBgSprite = button.normalBgSprite;
        button.background.type = Image.Type.Simple;
        button.background.color = Color.white;
        button.RefreshNormalBackground(button.normalBgSprite);
        return button;
    }
    private void ConfirmDismantle(RobotRecord record)
    {
        UI.Chain<ConfirmDialogWindow>().Invoke(view => view.Configure(option => {
            if (option == ConfirmDialogWindow.Option.Yes) Dismantle(record);
        }, ModText.DismantleConfirm, focusOnApply: false, record.DisplayName, ModText.DismantleButton, "ui.tooltip.Cancel")).Show();
    }
    private void Dismantle(RobotRecord record)
    {
        try
        {
            var magnumCargo = Field<MagnumCargo>(screen, "_magnumCargo");
            var spaceTime = Field<SpaceTime>(screen, "_spaceTime");
            var items = Roster.Dismantle(record, mob => ArPackageCatalogue.TryGetPackageId(mob, out var packageId)
                ? SingletonMonoBehaviour<ItemFactory>.Instance.CreateForInventory(packageId) : null, DisassemblePackage);
            foreach (var item in items)
                MagnumCargoSystem.AddCargo(magnumCargo, spaceTime, item, null, splittedItem: false, tabFilter: true);
            error = null;
        }
        catch (Exception ex)
        {
            error = ModText.Get("quasirobotics.roster.dismantle_failed");
            Plugin.Logger.LogException(ex);
        }
        Render();
    }
    // Standard cargo Disassemble: native part list and native drop chance.
    private static List<BasePickupItem> DisassemblePackage(BasePickupItem package)
    {
        if (ItemInteractionSystem.CanDisassemble(package)
            && ItemInteractionSystem.Disassemble(package, null, out var parts)) return parts;
        return new List<BasePickupItem> { package };
    }
    public static MercenaryPanel CreateRobotRow(RobotRecord record, GameObject prefab, Transform parent, SpaceTime spaceTime, Action equipment, Action augments)
    {
        var panel = Object.Instantiate(prefab, parent).GetComponent<MercenaryPanel>();
        panel.gameObject.SetActive(true);
        panel.name = "ARRobot_" + record.Identity.Id;
        var proxy = record.CreateProxy();
        panel.Initialize(spaceTime, proxy,
            Data.MercenaryProfiles.GetRecord(proxy.ProfileId), MercenaryPanel.Mode.StateIconsSelectable);
        SetRobotHeadIcon(Field<Image>(panel, "_rankIcon"), record.Data);
        var name = Field<TextMeshProUGUI>(panel, "_mercName");
        name.text = record.DisplayName; name.richText = false;
        name.enableWordWrapping = false; name.overflowMode = TextOverflowModes.Ellipsis;
        name.rectTransform.anchorMin = new Vector2(0, .5f); name.rectTransform.anchorMax = new Vector2(1, .5f);
        name.rectTransform.offsetMin = new Vector2(43, -5); name.rectTransform.offsetMax = new Vector2(-80, 15);
        var subtitle = Field<TextMeshProUGUI>(panel, "_mercClass");
        subtitle.text = record.Subtitle(spaceTime.Time);
        subtitle.enableWordWrapping = false;
        subtitle.overflowMode = TextOverflowModes.Ellipsis;
        subtitle.rectTransform.anchorMin = subtitle.rectTransform.anchorMax = new Vector2(0, .5f);
        subtitle.rectTransform.anchorMax = new Vector2(1, .5f);
        subtitle.rectTransform.offsetMin = new Vector2(30, -15);
        subtitle.rectTransform.offsetMax = new Vector2(-55, 5);
        Field<MercenaryTooltipHandler>(panel, "_mercenaryTooltipHandler").enabled = false;
        var classIcon = Field<MercenaryClassIcon>(panel, "_classIcon");
        classIcon.gameObject.SetActive(false);
        panel.OnIconSelected += (merc, icon) => {
            if (icon == MercenaryPanel.IconSelected.Backpack) equipment();
            else if (icon == MercenaryPanel.IconSelected.Implants) augments();
        };
        var cargo = UI.Get<ManageCargoTabScreen>();
        var rename = Object.Instantiate(Field<CommonButton>(cargo, "_editTabNameButton"), name.transform.parent);
        rename.name = "ARRename";
        var renameRect = (RectTransform)rename.transform;
        renameRect.anchorMin = renameRect.anchorMax = new Vector2(0, .5f);
        renameRect.pivot = new Vector2(0, .5f);
        renameRect.sizeDelta = new Vector2(11, 11);
        renameRect.anchoredPosition = new Vector2(30, 5);
        var renameLayout = rename.GetComponent<LayoutElement>() ?? rename.gameObject.AddComponent<LayoutElement>();
        renameLayout.ignoreLayout = true;
        // Native cargo pencil artwork and native text input, with robot-only callbacks.
        var input = Object.Instantiate(Field<InputFieldWrapper>(cargo, "_inputFieldWrapper"), name.transform.parent);
        input.name = "ARRobotNameInput";
        CopyRect(name.rectTransform, (RectTransform)input.transform);
        input.gameObject.SetActive(false);
        var tmpInput = input.GetComponent<TMP_InputField>();
        if (tmpInput != null)
        {
            tmpInput.characterLimit = 48;
            tmpInput.lineType = TMP_InputField.LineType.SingleLine;
            var area = tmpInput.textViewport;
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(2, 0); area.offsetMax = new Vector2(-2, 0);
        }
        AccessTools.Field(typeof(InputFieldWrapper), "OnEndEdit").SetValue(input, (Action<string>)(value => {
            record.Rename(value); name.text = record.DisplayName;
            input.gameObject.SetActive(false); name.gameObject.SetActive(true); rename.gameObject.SetActive(true);
        }));
        AccessTools.Field(typeof(CommonButton), "OnClick").SetValue(rename, (Action<CommonButton, int>)((b, n) => {
            name.gameObject.SetActive(false); rename.gameObject.SetActive(false);
            input.gameObject.SetActive(true); input.SetText(record.DisplayName);
            input.ActivateInputField(); UI.Navigation.RegisterElement(input); UI.Navigation.Select(input);
        }));
        rename.gameObject.SetActive(true);
        UI.Navigation.RegisterElement(panel);
        UI.Navigation.RegisterElement(rename);
        return panel;
    }
    private void OpenEquipment(RobotRecord record)
    {
        var proxy = record.CreateProxy();
        UI.Hide<MercenariesScreen>();
        UI.Chain<ArsenalScreen>().Invoke(view => {
            view.Configure(proxy, false);
            ArEquipmentStorage.Enable(record.Data.Inventory, view);
            view.RefreshView();
        }).Show().Fallback<MercenariesScreen>();
    }
    public static void SetRobotHeadIcon(Image image, CreatureData data)
    {
        string slot = data.MobClassId == "cyborg_battle" ? "BattleCyborgHead"
            : data.MobClassId == "cyborg_recreation" ? "RecreationCyborgHead" : "CyborgHead";
        var descriptor = Data.WoundSlots.GetRecord(slot).ContentDescriptor as WoundSlotDescriptor;
        if (descriptor == null) return;
        image.sprite = descriptor.NormalIcon;
        // Wound-slot artwork uses native UI units; rank sprites use a different
        // pixels-per-unit scale. Match WoundSlot.Initialize, not the rank helper.
        image.SetNativeSize();
    }
    private void OpenAugments(RobotRecord record)
    {
        var proxy = record.CreateProxy();
        UI.Hide<MercenariesScreen>();
        UI.Chain<AugmentationScreen>().Invoke(view => {
            view.Configure(proxy);
            ArEquipmentStorage.Enable(record.Data.Inventory, view);
            view.RefreshView();
        }).Show().Fallback<MercenariesScreen>();
    }
}

