using System;
using HarmonyLib;
using MGSC;
using SimpleJSON;
using TMPro;
using System.Linq;
using UnityEngine.UI;

namespace QuasiRobotics.Patches;

[HarmonyPatch(typeof(SaveToJSON), nameof(SaveToJSON.CreateNode))]
public static class RobotSavePatch
{
    public const string RosterKey = "QuasiRobotics_Robots_v1";
    public const string IdentityKey = "QuasiRobotics_Robot_v1";
    // Keys written by AR Recovery; read on load so old campaigns keep their robots.
    public const string LegacyRosterKey = "QM_ArRecovery_Robots_v1";
    public const string LegacyIdentityKey = "QM_ArRecovery_Robot_v1";
    [HarmonyPostfix] public static void Postfix(object obj, JSONNode __result)
    {
        if (Uninstaller.Active) return;
        if (obj is Mercenaries mercs) __result["Content"][RosterKey] = SaveToJSON.CreateNode(RobotRoster.For(mercs));
        else if (obj is CreatureData data && RobotIdentity.Find(data) is RobotIdentity identity)
            __result[IdentityKey] = SaveToJSON.CreateNode(identity);
    }
}
[HarmonyPatch(typeof(LoadFromJSON), nameof(LoadFromJSON.LoadFieldsAndProperties))]
public static class RobotLoadPatch
{
    [HarmonyPostfix] public static void Postfix(object obj, JSONNode node)
    {
        if (obj is Mercenaries mercs)
        {
            var roster = new RobotRoster();
            if (node.HasKey(RobotSavePatch.RosterKey)) roster.LoadJSON(node[RobotSavePatch.RosterKey]);
            else if (node.HasKey(RobotSavePatch.LegacyRosterKey)) roster.LoadJSON(node[RobotSavePatch.LegacyRosterKey]);
            RobotRoster.Restore(mercs, roster);
        }
        else if (obj is CreatureData data && (node.HasKey(RobotSavePatch.IdentityKey) || node.HasKey(RobotSavePatch.LegacyIdentityKey)))
        {
            var identity = new RobotIdentity();
            identity.LoadJSON(node[node.HasKey(RobotSavePatch.IdentityKey) ? RobotSavePatch.IdentityKey : RobotSavePatch.LegacyIdentityKey]);
            RobotIdentity.Bind(data, identity);
        }
    }
}
[HarmonyPatch(typeof(Localization), nameof(Localization.Get), typeof(string), typeof(bool))]
public static class RobotNamePatch
{
    [HarmonyPrefix] public static bool Prefix(string key, ref string __result)
    {
        if (ModText.TryGet(key, out var text)) { __result = text; return false; }
        if (!RobotIdentity.TryName(key, out var name)) return true;
        __result = name;
        return false;
    }
}
[HarmonyPatch(typeof(CreatureData), nameof(CreatureData.RankPerk), MethodType.Getter)]
public static class RobotRankPatch
{
    [HarmonyPrefix] public static bool Prefix(CreatureData __instance, ref Perk __result)
    {
        var identity = RobotIdentity.Find(__instance);
        if (identity == null) return true;
        __result = identity.Rank;
        return false;
    }
}
[HarmonyPatch(typeof(Perk), nameof(Perk.AddExp))]
public static class RobotRankExpPatch
{
    [HarmonyPrefix] public static bool Prefix(Perk __instance, ref bool __result)
    {
        if (!RobotIdentity.IsFixedRank(__instance)) return true;
        __result = false;
        return false;
    }
}
[HarmonyPatch(typeof(PerkSystem), nameof(PerkSystem.LevelUpPerk))]
public static class RobotLevelUpPatch
{
    [HarmonyPrefix] public static bool Prefix(Mercenary mercenary, Perk perk) =>
        RobotIdentity.Find(mercenary?.CreatureData) == null && !RobotIdentity.IsFixedRank(perk);
}
[HarmonyPatch(typeof(MercenariesScreen), "OnEnable")]
public static class RobotRosterScreenPatch
{
    [HarmonyPostfix] public static void Postfix(MercenariesScreen __instance)
    {
        try { (__instance.GetComponent<RobotRosterView>() ?? __instance.gameObject.AddComponent<RobotRosterView>()).Refresh(__instance); }
        catch (Exception ex) { Plugin.Logger.LogError("Could not build robot roster controls."); Plugin.Logger.LogException(ex); }
    }
}
[HarmonyPatch(typeof(NoPlayerInventoryView), nameof(NoPlayerInventoryView.Initialize))]
public static class RobotInventoryProfilePatch
{
    [HarmonyPrefix] public static void Prefix(Mercenary mercenary, ref MercenaryProfileRecord profileRecord)
    {
        if (RobotIdentity.Find(mercenary?.CreatureData) == null) return;
        profileRecord = new MercenaryProfileRecord { ActorId = mercenary.CreatureData.ActorId };
        AccessTools.Property(typeof(ConfigTableRecord), "Id").SetValue(profileRecord, mercenary.CreatureData.LocalizationId);
    }
    [HarmonyPostfix] public static void Postfix(NoPlayerInventoryView __instance, Mercenary mercenary)
    {
        if (RobotIdentity.Find(mercenary?.CreatureData) == null) return;
        RobotRosterView.Field<TextMeshProUGUI>(__instance, "_agentName").text = Localization.Get("monster." + mercenary.CreatureData.LocalizationId + ".name");
        RobotRosterView.Field<TextMeshProUGUI>(__instance, "_agentSpec").text = RobotRoster.VariantName(mercenary.CreatureData.MobClassId);
    }
}
[HarmonyPatch(typeof(AugmentationScreen), "RefreshStats")]
public static class RobotAugmentProfilePatch
{
    [HarmonyPostfix] public static void Postfix(AugmentationScreen __instance)
    {
        var merc = RobotRosterView.Field<Mercenary>(__instance, "_merc");
        if (RobotIdentity.Find(merc?.CreatureData) == null) return;
        RobotRosterView.Field<TextMeshProUGUI>(__instance, "_mercName").text = Localization.Get("monster." + merc.CreatureData.LocalizationId + ".name");
        RobotRosterView.Field<TextMeshProUGUI>(__instance, "_mercClass").text = RobotRoster.VariantName(merc.CreatureData.MobClassId);
        RobotRosterView.Field<TextMeshProUGUI>(__instance, "_exp").text = "";
        RobotRosterView.SetRobotHeadIcon(RobotRosterView.Field<Image>(__instance, "_levelIcon"), merc.CreatureData);
    }
}
[HarmonyPatch(typeof(RankTooltipHandler), nameof(RankTooltipHandler.Initialize))]
public static class RobotRankTooltipPatch
{
    [HarmonyPrefix] public static void Prefix(ref Mercenary mercenary)
    {
        if (RobotIdentity.Find(mercenary?.CreatureData) != null) mercenary = null;
    }
}
[HarmonyPatch(typeof(AugmentationSystem), nameof(AugmentationSystem.RestoreDefaultWoundSlots))]
public static class RobotRestoreBodyPatch
{
    [HarmonyPrefix] public static bool Prefix(Mercenary mercenary)
    {
        var data = mercenary?.CreatureData;
        if (RobotIdentity.Find(data) == null) return true;
        // Robot editor proxies carry an operator profile for UI compatibility only.
        // Restore the robot body's defaults, preserving every installed augmentation.
        foreach (string slot in Data.BodyTypes.GetRecord(data.BodyTypeId).WoundSlots)
        {
            var record = Data.WoundSlots.GetRecord(slot);
            if (data.WoundSlotMap.Keys.Any(existing => Data.WoundSlots.GetRecord(existing).SlotType == record.SlotType)) continue;
            data.WoundSlotMap[slot] = new ImplantSocketData { TotalSockets = record.ImplantSocketsDefault };
        }
        return false;
    }
}
// Restrict the native editors by their actual current creature, not a persistent global mode.
[HarmonyPatch(typeof(ScreenWithShipCargo), "ContextMenuOnCmdSelected")]
public static class RobotEditorCommandPatch
{
    public static bool IsProgressionCommand(int value) => value == (int)ContextMenuCommand.ApplySkull
        || value == (int)ContextMenuCommand.RemoveSkull || value == (int)ContextMenuCommand.Use || value == (int)ContextMenuCommand.Eat;
    [HarmonyPrefix] public static bool Prefix(ScreenWithShipCargo __instance, int bindValue) =>
        RobotIdentity.Find(RobotRosterView.Field<Mercenary>(__instance, "_merc")?.CreatureData) == null || !IsProgressionCommand(bindValue);
}
[HarmonyPatch(typeof(CommonContextMenu), nameof(CommonContextMenu.SetupCommand))]
public static class RobotEditorMenuPatch
{
    [HarmonyPrefix] public static bool Prefix(int bindedVal)
    {
        if (!RobotEditorCommandPatch.IsProgressionCommand(bindedVal)) return true;
        foreach (var view in UI.GetActiveViews())
            if (view is ScreenWithShipCargo cargo && RobotIdentity.Find(RobotRosterView.Field<Mercenary>(cargo, "_merc")?.CreatureData) != null) return false;
        return true;
    }
}
