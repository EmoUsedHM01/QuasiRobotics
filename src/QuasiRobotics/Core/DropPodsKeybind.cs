using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MGSC;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace QuasiRobotics;

// "Call Drop Pods" is a real game key (Hotkeys layout, X by default), so the
// game stores it, shows it on the Mission Control button and presses the button.
// It can be rebound from the game's Keybindings page, and from Mod Config Menu,
// where the native key row replaces a placeholder line.
public static class DropPodsKeybind
{
    public const string KeyId = "quasirobotics_drop_pods";
    public const string Layout = "Hotkeys";
    public const KeyCode DefaultKey = KeyCode.X;
    // The Mod Config Menu line the key row takes the place of.
    public const string McmPlaceholder = "Call Drop Pods key";

    public static GameKeyRecord CreateRecord()
    {
        var record = new GameKeyRecord
        {
            Layout = Layout,
            OtherKeyIdToPress = string.Empty,
            Bind1 = new List<KeyCode> { DefaultKey },
            Bind2 = new List<KeyCode> { KeyCode.None },
            ControllerBind1 = new List<ControllerAction> { ControllerAction.None },
            ControllerBind2 = new List<ControllerAction> { ControllerAction.None },
            AxisName = string.Empty,
            ExclusiveInputMode = new List<InputController.InputMode>(),
            ForbiddenKeysToBind = new List<KeyCode>(),
        };
        AccessTools.Property(typeof(ConfigTableRecord), nameof(ConfigTableRecord.Id)).SetValue(record, KeyId);
        return record;
    }

    // Config tables: picked up if the input controller starts after this.
    public static void Register(ConfigRecordCollection<GameKeyRecord> keys)
    {
        if (keys != null && keys.GetRecord(KeyId) == null) keys.AddRecord(KeyId, CreateRecord());
    }

    // An input controller that already started gets the key added directly.
    public static void EnsureKey(InputController input)
    {
        if (input == null || input.GetKey(KeyId) != null) return;
        var record = Data.Keybinding?.GetRecord(KeyId) ?? CreateRecord();
        var keys = (List<GameKey>)AccessTools.Field(typeof(InputController), "_keys").GetValue(input);
        var ids = (Dictionary<string, GameKey>)AccessTools.Field(typeof(InputController), "_idsToKeys").GetValue(input);
        var key = new GameKey(record, keys.Count == 0 ? 0 : keys.Max(k => k.RuntimeId) + 1);
        key.Load();
        keys.Add(key);
        ids[KeyId] = key;
        var maps = (IDictionary)AccessTools.Field(typeof(InputController), "_keymaps").GetValue(input);
        if (maps.Contains(Layout)) Traverse.Create(maps[Layout]).Property<List<GameKey>>("Keys").Value.Add(key);
        else Plugin.Logger.LogWarning("No " + Layout + " key layout; the Drop Pods key will not work.");
        input.RaiseKeyChange(KeyId);
    }

    // Mod Config Menu, after it builds the QuasiRobotics page: the native key
    // row (label, two bind slots) in place of the placeholder line.
    public static void PatchMcm(Harmony harmony)
    {
        var build = AccessTools.Method(AccessTools.TypeByName("ModConfigMenu.ModConfigMenu"), "BuildModConfig");
        if (build == null) { Plugin.Logger.LogWarning("Mod Config Menu page builder not found; rebind Drop Pods in Keybindings."); return; }
        harmony.Patch(build, postfix: new HarmonyMethod(typeof(DropPodsKeybind), nameof(BuildPostfix)));
    }
    public static void BuildPostfix(object modData, Transform __result)
    {
        try
        {
            if (__result == null || Traverse.Create(modData).Field<string>("ModName").Value != "QuasiRobotics") return;
            var placeholder = __result.GetComponentsInChildren<TextMeshProUGUI>(true)
                .FirstOrDefault(t => string.Equals(t.text?.Trim(), McmPlaceholder, StringComparison.OrdinalIgnoreCase));
            var key = SingletonMonoBehaviour<InputController>.Instance?.GetKey(KeyId);
            var page = Object.FindObjectOfType<KeybindingPage>(true);
            var pool = page == null ? null : RobotRosterView.Field<Pool>(page, "_gameKeyPanelsPool");
            if (placeholder == null || key == null || pool?.prefab == null)
            {
                Plugin.Logger.LogWarning("Drop Pods key row could not be added to Mod Config Menu; rebind it in Keybindings.");
                return;
            }
            // The placeholder's row is a direct child of the page's scroll content.
            var content = __result.GetComponent<UnityEngine.UI.ScrollRect>()?.content;
            var row = placeholder.transform;
            while (row.parent != null && row.parent != content) row = row.parent;
            if (row.parent == null) { Plugin.Logger.LogWarning("Drop Pods key row: placeholder is outside the page."); return; }
            // A copy of the nearest MCM toggle row above, so the label, font and
            // spacing match; the native bind slots take the toggle's place.
            UnityEngine.UI.Toggle toggle = null;
            Transform template = null;
            for (int i = row.GetSiblingIndex() - 1; i >= 0 && toggle == null; i--)
            {
                template = content.GetChild(i);
                toggle = template.GetComponentInChildren<UnityEngine.UI.Toggle>(true);
            }
            if (toggle == null) { Plugin.Logger.LogWarning("Drop Pods key row: no MCM row to copy."); return; }
            var copy = Object.Instantiate(template.gameObject, content, false);
            copy.name = "QuasiRobotics_DropPodsKey";
            copy.transform.SetSiblingIndex(row.GetSiblingIndex());
            Object.Destroy(row.gameObject);
            copy.GetComponentInChildren<LocalizableLabel>(true)?.ChangeLabel(McmLabel);
            CopyTooltip(template.gameObject, copy, McmTooltip);
            UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)content);

            var slotToggle = copy.GetComponentInChildren<UnityEngine.UI.Toggle>(true);
            var spot = (RectTransform)slotToggle.transform;
            float right = spot.localPosition.x + spot.rect.xMax, middle = spot.localPosition.y + spot.rect.center.y, height = spot.rect.height;
            slotToggle.gameObject.SetActive(false);

            // The native row stays hidden: it only drives its two slots.
            var panel = Object.Instantiate(pool.prefab, copy.transform, false).GetComponent<GameKeySetupPanel>();
            var slots = new[] { RobotRosterView.Field<Component>(panel, "_bind2"), RobotRosterView.Field<Component>(panel, "_bind1") };
            foreach (var slot in slots)
            {
                var rt = (RectTransform)slot.transform;
                float width = rt.rect.width;
                rt.SetParent(spot.parent, false);
                (rt.gameObject.GetComponent<UnityEngine.UI.LayoutElement>() ?? rt.gameObject.AddComponent<UnityEngine.UI.LayoutElement>()).ignoreLayout = true;
                rt.anchorMin = rt.anchorMax = new Vector2(.5f, .5f);
                rt.pivot = new Vector2(1f, .5f);
                rt.sizeDelta = new Vector2(width, height);
                rt.localPosition = new Vector3(right, middle, 0f);
                right -= width + SlotGap;
            }
            panel.gameObject.SetActive(false);
            panel.Initialize(key);
            panel.OnSlotClicked += OnSlotClicked;
            panel.OnDeleteBind += OnDeleteBind;
            mcmRow = copy;
        }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }
    public const string McmLabel = "quasirobotics.mcm.drop_pods_key";
    public static string McmTooltip => ModText.Get("quasirobotics.mcm.drop_pods_key.tooltip");
    private const float SlotGap = 8f;
    private static GameObject mcmRow;

    // MCM keeps its hover tooltip in private fields, which a copy does not carry.
    private static void CopyTooltip(GameObject from, GameObject to, string text)
    {
        var type = AccessTools.TypeByName("ModConfigMenu.GenericHoverTooltip");
        if (type == null) return;
        var source = from.GetComponentInChildren(type, true);
        var target = to.GetComponentInChildren(type, true);
        if (source == null || target == null) return;
        var tooltip = AccessTools.Field(type, "_tooltip").GetValue(source);
        AccessTools.Method(type, "Initialize").Invoke(target, new[] { text, tooltip });
    }

    private static GameKeySetupPanel pending;
    private static int pendingSlot;
    private static BindGameKeyWindow subscribed;

    private static void OnSlotClicked(GameKeySetupPanel panel, int slot)
    {
        pending = panel;
        pendingSlot = slot;
        var window = UI.Get<BindGameKeyWindow>();
        if (window != null && window != subscribed)
        {
            window.OnKeyBinded += OnKeyBinded;
            window.OnСontrollerKeyBinded += OnControllerKeyBinded;
            subscribed = window;
        }
        UI.Chain<BindGameKeyWindow>().Invoke(v => v.Configure(panel.GameKey)).Show();
    }
    private static bool Active => pending != null && mcmRow != null && mcmRow.activeInHierarchy;

    private static void OnKeyBinded(List<KeyCode> keys)
    {
        if (!Active) return;
        var key = pending.GameKey;
        Set(pendingSlot == 0 ? key.Bind1 : key.Bind2, pendingSlot == 0 ? key.Bind2 : key.Bind1, keys, KeyCode.None);
        Apply();
    }
    private static void OnControllerKeyBinded(List<ControllerAction> actions)
    {
        if (!Active) return;
        var key = pending.GameKey;
        Set(pendingSlot == 0 ? key.ControllerBind1 : key.ControllerBind2, pendingSlot == 0 ? key.ControllerBind2 : key.ControllerBind1, actions, ControllerAction.None);
        Apply();
    }
    private static void OnDeleteBind(GameKeySetupPanel panel, int slot)
    {
        pending = panel;
        var key = panel.GameKey;
        if (SingletonMonoBehaviour<InputController>.Instance.Mode == InputController.InputMode.Controller)
            Set(slot == 0 ? key.ControllerBind1 : key.ControllerBind2, null, new List<ControllerAction> { ControllerAction.None }, ControllerAction.None);
        else Set(slot == 0 ? key.Bind1 : key.Bind2, null, new List<KeyCode> { KeyCode.None }, KeyCode.None);
        Apply();
    }
    // The same bind in the other slot is cleared, as the game does.
    public static void Set<T>(List<T> slot, List<T> other, List<T> value, T none)
    {
        slot.Clear();
        slot.AddRange(value);
        if (other != null && other.SequenceEqual(value)) { other.Clear(); other.Add(none); }
    }
    private static void Apply()
    {
        var key = pending.GameKey;
        key.Save();
        PlayerPrefs.Save();
        SingletonMonoBehaviour<InputController>.Instance.RaiseKeyChange(key.Record.Id);
        pending.Initialize(key);
    }
}

// The Keybindings page listens to the same bind window and assumes it opened
// it. While that page is closed (Mod Config Menu is binding), it stays out.
[HarmonyPatch(typeof(KeybindingPage), "BindGameKeyWindowOnKeyBinded")]
public static class KeybindingPageKeyGuardPatch
{
    [HarmonyPrefix] public static bool Prefix(KeybindingPage __instance) => __instance.gameObject.activeInHierarchy;
}
[HarmonyPatch(typeof(KeybindingPage), "BindGameKeyWindowOnСontrollerKeyBinded")]
public static class KeybindingPageControllerGuardPatch
{
    [HarmonyPrefix] public static bool Prefix(KeybindingPage __instance) => __instance.gameObject.activeInHierarchy;
}
