using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using MGSC;
using QuasiRobotics.Packages;
using UnityEngine;
using UnityEngine.UI;

namespace QuasiRobotics;

// The monster window has a four-column viewport and no native vest-content page.
// Resize the real backpack and let native ItemGrid own all slot placement and scrolling.
public sealed class ArMissionStorage : MonoBehaviour
{
    private MonsterInspectWindow window;
    private Inventory inventory;
    private ItemGrid grid;
    private bool refreshing;
    private bool showVest;
    private readonly List<EquipmentLayout> equipment = new List<EquipmentLayout>();
    public static ItemStorage SelectedStorage(Inventory inv, bool vest) => vest ? inv.VestStore : inv.BackpackStore;

    private sealed class EquipmentLayout
    {
        public ItemSlot Vest, Servo;
        public Button VestButton, BackpackButton;
        public Vector3 OriginalVestPosition;
    }
    private void ConfigureEquipment()
    {
        foreach (string prefix in new[] { "_", "_ally" })
        {
            string Name(string part) => prefix == "_" ? "_" + char.ToLowerInvariant(part[0]) + part.Substring(1) : prefix + part;
            ItemSlot Get(string part) => Traverse.Create(window).Field<ItemSlot>(Name(part)).Value;
            var vest = Get("VestSlot");
            var pack = Get("BackpackSlot");
            var helmet = Get("HelmetSlot");
            var secondary = Get("SecondarySlot");
            var boots = Get("BootsSlot");
            var servo = UnityEngine.Object.Instantiate(secondary, secondary.transform.parent);
            servo.name = "ARServoArmSlot";
            equipment.Add(new EquipmentLayout {
                Vest = vest, Servo = servo, OriginalVestPosition = vest.transform.localPosition,
                VestButton = CreateButton(vest, true), BackpackButton = CreateButton(pack, false)
            });
            vest.transform.position = pack.transform.position + (pack.transform.position - helmet.transform.position);
            var pos = secondary.transform.position;
            pos.y = boots.transform.position.y;
            servo.transform.position = pos;
        }
    }
    public void RefreshEquipment()
    {
        if (inventory == null) return;
        foreach (var layout in equipment)
        {
            RefreshButton(layout.VestButton, showVest);
            RefreshButton(layout.BackpackButton, !showVest);
            layout.Servo.gameObject.SetActive(inventory.HasServoArm);
            if (!inventory.HasServoArm) continue;
            var storage = inventory.ServoArmSlot;
            if (storage.Empty) layout.Servo.InitializeEmpty(storage);
            else layout.Servo.Initialize(storage.First, storage);
        }
    }

    public void Configure(MonsterInspectWindow view, Creature creature)
    {
        ResetLayout();
        window = view;
        var data = creature?.CreatureData;
        inventory = creature is Monster && data?.Inventory?.CanHaveVest == true
            && ArPackageCatalogue.TryGetPackageId(data.MobClassId, out _) ? data.Inventory : null;
        showVest = false;
        if (inventory == null) return;
        grid = Traverse.Create(window).Field<ItemGrid>("_itemGrid").Value;

        ConfigureEquipment();
        Refresh();
    }

    // The native header is selected again by OnEnable during item handling.
    // Only Configure (a new inspection) and our two arrows change the storage.
    public ItemStorage GetSelectedStorage(Inventory inv) =>
        SelectedStorage(inv, ReferenceEquals(inv, inventory) && showVest);

    public static ItemStorage GetSelectedStorage(Inventory inv, MonsterInspectWindow view) =>
        view.GetComponent<ArMissionStorage>()?.GetSelectedStorage(inv) ?? inv.BackpackStore;

    public static IEnumerable<CodeInstruction> PreserveStorage(IEnumerable<CodeInstruction> instructions)
    {
        var backpack = AccessTools.Field(typeof(Inventory), nameof(Inventory.BackpackStore));
        var replacement = AccessTools.Method(typeof(ArMissionStorage), nameof(GetSelectedStorage),
            new[] { typeof(Inventory), typeof(MonsterInspectWindow) });
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(backpack))
            {
                // Keep labels/exception boundaries on the first replacement instruction.
                instruction.opcode = OpCodes.Ldarg_0;
                instruction.operand = null;
                yield return instruction;
                yield return new CodeInstruction(OpCodes.Call, replacement);
            }
            else yield return instruction;
        }
    }

    public void Refresh()
    {
        if (inventory == null || grid == null || refreshing) return;
        refreshing = true;
        try
        {
        RefreshEquipment();
        bool allowed = window.ShouldShowAllyInventory();
        // Other tabs own their visibility. Do not expose storage when access is denied.
        if (!allowed || Traverse.Create(window).Field("_currentPage").GetValue().ToString() != "Backpack") return;
        var storage = SelectedStorage(inventory, showVest);
        ArStorageLayout.ResizeBackpack(inventory);
        grid.Initialize(storage);
        grid.InitFakeGrid(0, 0);
        Traverse.Create(window).Field<CommonScrollBar>("_scrollbar").Value.UpdateVisualDeferred();
        }
        finally { refreshing = false; }
    }

    private void SelectStorage(bool vest)
    {
        if (inventory == null || !window.ShouldShowAllyInventory()) return;
        Traverse.Create(window).Field<InspectWindowHeaderButton>("_itemsButton").Value.Select(true);
        showVest = vest;
        Refresh();
        if (grid.ItemGridScroll != null) grid.ItemGridScroll.VerticalNormalizedPosition = 1f;
        Traverse.Create(window).Field<CommonScrollBar>("_scrollbar").Value.Value = 1f;
    }

    private void ResetLayout()
    {
        foreach (var layout in equipment)
        {
            if (layout.Vest != null) layout.Vest.transform.localPosition = layout.OriginalVestPosition;
            if (layout.Servo != null) { layout.Servo.gameObject.SetActive(false); UnityEngine.Object.Destroy(layout.Servo.gameObject); }
            foreach (var button in new[] { layout.VestButton, layout.BackpackButton })
                if (button != null) { button.gameObject.SetActive(false); UnityEngine.Object.Destroy(button.gameObject); }
        }
        equipment.Clear();
        grid = null;
        inventory = null;
    }
    // Native Refresh temporarily hides this window without reconfiguring it.
    private void OnDestroy() { ResetLayout(); }

    private void RefreshButton(Button button, bool selected)
    {
        button.interactable = window.ShouldShowAllyInventory();
        var normal = RobotButtonSprites.Get(selected ? "arrowNormal" : "arrowPressed");
        ((Image)button.targetGraphic).sprite = normal;
        button.spriteState = new SpriteState {
            highlightedSprite = RobotButtonSprites.Get("arrowMouseOver"),
            pressedSprite = RobotButtonSprites.Get("arrowPressed"),
            selectedSprite = normal, disabledSprite = RobotButtonSprites.Get("arrowPressed")
        };
    }

    private Button CreateButton(ItemSlot slot, bool vest)
    {
        var go = new GameObject(vest ? "ARShowVest" : "ARShowBackpack", typeof(RectTransform), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(slot.transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(1, 0);
        rect.sizeDelta = new Vector2(8, 8);
        rect.anchoredPosition = new Vector2(0, 1);
        go.GetComponent<LayoutElement>().ignoreLayout = true;
        var icon = new GameObject("Arrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        icon.transform.SetParent(rect, false);
        var imageRect = (RectTransform)icon.transform;
        imageRect.anchorMin = Vector2.zero; imageRect.anchorMax = Vector2.one;
        imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
        // Mirror across the horizontal axis: the native arrow points up.
        imageRect.localScale = new Vector3(1, -1, 1);
        var image = icon.GetComponent<Image>();
        image.type = Image.Type.Simple; image.color = Color.white;
        var button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.SpriteSwap;
        button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
        button.onClick.AddListener(() => SelectStorage(vest));
        RefreshButton(button, vest == showVest);
        return button;
    }
}

[HarmonyPatch(typeof(MonsterInspectWindow), nameof(MonsterInspectWindow.Configure))]
public static class ArMissionStorageConfigurePatch
{
    [HarmonyPostfix]
    public static void Postfix(MonsterInspectWindow __instance, Creature creature)
    {
        var view = __instance.GetComponent<ArMissionStorage>() ?? __instance.gameObject.AddComponent<ArMissionStorage>();
        view.Configure(__instance, creature);
    }
}
[HarmonyPatch(typeof(MonsterInspectWindow), "RefreshBackpackPage")]
public static class ArMissionStoragePagePatch
{
    [HarmonyTranspiler] public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => ArMissionStorage.PreserveStorage(instructions);
    [HarmonyPostfix] public static void Postfix(MonsterInspectWindow __instance) => __instance.GetComponent<ArMissionStorage>()?.Refresh();
}
// RefreshSlots is overloaded (a private 8-slot helper exists); target the public no-arg one.
[HarmonyPatch(typeof(MonsterInspectWindow), "RefreshSlots", new Type[0])]
public static class ArMissionStorageRefreshPatch
{
    [HarmonyTranspiler] public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => ArMissionStorage.PreserveStorage(instructions);
    [HarmonyPostfix] public static void Postfix(MonsterInspectWindow __instance) => __instance.GetComponent<ArMissionStorage>()?.Refresh();
}
[HarmonyPatch(typeof(MonsterInspectWindow), "RefreshAllySlots")]
public static class ArMissionAllyEquipmentPatch
{
    [HarmonyPostfix] public static void Postfix(MonsterInspectWindow __instance) => __instance.GetComponent<ArMissionStorage>()?.RefreshEquipment();
}
[HarmonyPatch(typeof(MonsterInspectWindow), "RefreshEnemySlots")]
public static class ArMissionEquipmentPatch
{
    [HarmonyPostfix] public static void Postfix(MonsterInspectWindow __instance) => __instance.GetComponent<ArMissionStorage>()?.RefreshEquipment();
}
