using HarmonyLib;
using MGSC;
using QuasiRobotics.Packages;

namespace QuasiRobotics.Patches;

// Both directions matter: vanilla only checks the destination's stack limit.
[HarmonyPatch(typeof(ItemInteractionSystem), nameof(ItemInteractionSystem.CanMerge))]
public static class PackageMergePatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(BasePickupItem itemFrom, BasePickupItem itemTo, ref bool __result)
    {
        if (ArLoadout.Find(itemFrom) != null || ArLoadout.Find(itemTo) != null)
            __result = false;
    }
}

// Arsenal.Configure/FixStacksCount normally restores the record's stack limit.
[HarmonyPatch(typeof(PickupItem), nameof(PickupItem.SetMaxStack))]
public static class PackageStackLimitPatch
{
    [HarmonyPrefix, HarmonyPriority(Priority.Last)]
    public static void Prefix(PickupItem __instance, ref short maxStack)
    {
        if (ArLoadout.Find(__instance) != null) maxStack = 1;
    }
}

[HarmonyPatch(typeof(PickupItem), nameof(PickupItem.OnAfterLoad))]
public static class PackageLoadedPatch
{
    [HarmonyPostfix]
    public static void Postfix(PickupItem __instance)
    {
        if (ArLoadout.Find(__instance) != null) __instance.SetMaxStack(1);
    }
}

// Native CopyComponent only recognizes vanilla component types.
[HarmonyPatch(typeof(ItemFactory), "CopyComponent")]
public static class PackageCopyPatch
{
    [HarmonyPrefix]
    public static bool Prefix(System.Collections.Generic.List<PickupItemComponent> itemComponents,
        PickupItemComponent initialComponent)
    {
        if (!(initialComponent is ArLoadoutComponent loadout)) return true;
        var copy = new ArLoadoutComponent();
        // Use the game's save serializer, preserving item state and avoiding aliases.
        copy.LoadJSON(SaveToJSON.CreateNode(loadout)["Content"]);
        itemComponents.Clear();
        itemComponents.Add(copy);
        return false;
    }
}

// Stack-size mods can override the getter without calling SetMaxStack.
[HarmonyPatch(typeof(PickupItem), nameof(PickupItem.MaxStack), MethodType.Getter)]
public static class PackageMaxStackPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(PickupItem __instance, ref short __result)
    {
        if (ArLoadout.Find(__instance) != null) __result = 1;
    }
}

[HarmonyPatch(typeof(PickupItem), nameof(PickupItem.IsFullStack), MethodType.Getter)]
public static class PackageFullStackPatch
{
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    public static void Postfix(PickupItem __instance, ref bool __result)
    {
        if (ArLoadout.Find(__instance) != null) __result = true;
    }
}
