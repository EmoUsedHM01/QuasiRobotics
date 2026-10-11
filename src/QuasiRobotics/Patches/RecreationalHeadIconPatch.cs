using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace QuasiRobotics.Patches;

// Replace the shared body-part art so the body slot and roster badges agree.
// Normal and fixated (fixed) states use the mod's art; wounded and amputated
// indicators keep their native artwork.
public static class RecreationalHeadIcons
{
    private static readonly Dictionary<string, Sprite> icons = new Dictionary<string, Sprite>();
    public static bool IsHead(WoundSlotDescriptor descriptor) => Data.WoundSlots != null
        && Data.WoundSlots.GetRecord("RecreationCyborgHead")?.ContentDescriptor == descriptor;
    public static Sprite Get(string asset, Sprite native)
    {
        if (icons.TryGetValue(asset, out var icon) && icon != null) return icon;
        using (var stream = typeof(Plugin).Assembly.GetManifestResourceStream("QuasiRobotics." + asset + ".png"))
        using (var bytes = new MemoryStream())
        {
            stream.CopyTo(bytes);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(texture, bytes.ToArray());
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = asset;
            // Preserve the original native UI footprint even if source resolution differs.
            float ppu = native.pixelsPerUnit * texture.width / native.rect.width;
            icon = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), ppu);
            icon.name = asset;
            Object.DontDestroyOnLoad(texture);
            Object.DontDestroyOnLoad(icon);
        }
        return icons[asset] = icon;
    }
}
[HarmonyPatch(typeof(WoundSlotDescriptor), nameof(WoundSlotDescriptor.NormalIcon), MethodType.Getter)]
public static class RecreationalHeadIconPatch
{
    [HarmonyPostfix] public static void Postfix(WoundSlotDescriptor __instance, ref Sprite __result)
    {
        if (__result != null && RecreationalHeadIcons.IsHead(__instance)) __result = RecreationalHeadIcons.Get("RecreationalHeadNormal", __result);
    }
}
[HarmonyPatch(typeof(WoundSlotDescriptor), nameof(WoundSlotDescriptor.FixatedIcon), MethodType.Getter)]
public static class RecreationalHeadFixedIconPatch
{
    [HarmonyPostfix] public static void Postfix(WoundSlotDescriptor __instance, ref Sprite __result)
    {
        if (__result != null && RecreationalHeadIcons.IsHead(__instance)) __result = RecreationalHeadIcons.Get("RecreationalHeadFixed", __result);
    }
}
