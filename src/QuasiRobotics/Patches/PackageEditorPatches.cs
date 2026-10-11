using System;
using System.Linq;
using HarmonyLib;
using MGSC;

namespace QuasiRobotics.Patches;

[HarmonyPatch(typeof(ScreenWithShipCargo), "DragControllerShowContextMenuCallback")]
public static class PackageMenuPatch
{
    [HarmonyPostfix]
    public static void Postfix(ScreenWithShipCargo __instance, ItemSlot obj)
    {
        if (!(__instance is ArsenalScreen) || !PackageEditor.CanEdit(obj.Item)) return;
        var menu = UI.Get<CommonContextMenu>();
        menu.SetupCommand("Edit AR inventory", PackageEditor.Command);
        if (!UI.IsShowing<CommonContextMenu>())
            UI.Chain<CommonContextMenu>().Show().SetBackgroundOrder(-1).SetBackOnBackgroundClick(true);
        else
        {
            AccessTools.Method(typeof(CommonContextMenu), "InitSize").Invoke(menu, new object[] { 0 });
            AccessTools.Method(typeof(CommonContextMenu), "RecalculatePosition").Invoke(menu, null);
            // The extra command was added after OnEnable registered the original buttons.
            var binds = (System.Collections.Generic.Dictionary<CommonButton, int>)
                AccessTools.Field(typeof(CommonContextMenu), "_commandBinds").GetValue(menu);
            foreach (var pair in binds.Where(p => p.Value == PackageEditor.Command))
                UI.Navigation.RegisterElement(pair.Key);
        }
    }
}

[HarmonyPatch(typeof(ScreenWithShipCargo), "ContextMenuOnCmdSelected")]
public static class PackageMenuCommandPatch
{
    [HarmonyPrefix]
    public static bool Prefix(ScreenWithShipCargo __instance, int bindValue, ItemSlot ____contextMenuItemSlot)
    {
        if (bindValue != PackageEditor.Command) return true;
        try
        {
            if (__instance is ArsenalScreen view && ____contextMenuItemSlot != null)
                PackageEditor.Open(view, ____contextMenuItemSlot.Item);
        }
        catch (Exception ex) { Plugin.Logger.LogError("Could not open the AR inventory."); Plugin.Logger.LogException(ex); }
        return false;
    }
}

[HarmonyPatch(typeof(ArsenalScreen), nameof(ArsenalScreen.Configure), typeof(Mercenary), typeof(bool))]
public static class PackageEditorConfigurePatch
{
    [HarmonyPrefix]
    public static void Prefix(bool showShuttle) { PackageEditor.LastShowShuttle = showShuttle; }
}

[HarmonyPatch(typeof(NoPlayerInventoryView), nameof(NoPlayerInventoryView.Initialize))]
public static class PackageEditorProfilePatch
{
    [HarmonyPrefix]
    public static void Prefix(Mercenary mercenary, ref MercenaryProfileRecord profileRecord)
    {
        if (PackageEditor.Proxy == null || !ReferenceEquals(mercenary, PackageEditor.Proxy)) return;
        profileRecord = new MercenaryProfileRecord {
            ActorId = mercenary.CreatureData.ActorId };
        AccessTools.Property(typeof(ConfigTableRecord), "Id").SetValue(profileRecord, mercenary.CreatureData.LocalizationId);
    }
    [HarmonyPostfix]
    public static void Postfix(NoPlayerInventoryView __instance, Mercenary mercenary)
    {
        if (PackageEditor.Proxy != null && ReferenceEquals(mercenary, PackageEditor.Proxy)) PackageEditor.Label(__instance);
    }
}

[HarmonyPatch(typeof(UI), nameof(UI.Back))]
public static class PackageEditorBackPatch
{
    [HarmonyPrefix]
    public static bool Prefix()
    {
        if (PackageEditor.Package == null || !UI.GetActiveViews().Any(v => v is ArsenalScreen)) return true;
        PackageEditor.Close();
        return false;
    }
}

[HarmonyPatch(typeof(ArsenalScreen), "OnDisable")]
public static class PackageEditorCleanupPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        if (!PackageEditor.Switching) PackageEditor.Finish();
    }
}

// Runtime-only lock: autosaves must not serialize a permanently locked package.
[HarmonyPatch(typeof(BasePickupItem), nameof(BasePickupItem.Locked), MethodType.Getter)]
public static class PackageEditorLockPatch
{
    [HarmonyPostfix]
    public static void Postfix(BasePickupItem __instance, ref bool __result)
    {
        if (ReferenceEquals(__instance, PackageEditor.Package)) __result = true;
    }
}
// These commands change mercenary progression/roster state, not equipment.
[HarmonyPatch(typeof(CommonContextMenu), nameof(CommonContextMenu.SetupCommand))]
public static class PackageEditorEquipmentOnlyPatch
{
    [HarmonyPrefix]
    public static bool Prefix(int bindedVal)
    {
        if (PackageEditor.Proxy == null) return true;
        return bindedVal != (int)ContextMenuCommand.ApplySkull
            && bindedVal != (int)ContextMenuCommand.RemoveSkull
            && bindedVal != (int)ContextMenuCommand.Use
            && bindedVal != (int)ContextMenuCommand.Eat;
    }
}
