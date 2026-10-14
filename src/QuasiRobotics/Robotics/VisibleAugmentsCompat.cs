using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using MGSC;
using UnityEngine;

namespace QuasiRobotics;

// Soft compatibility with Visible Augments (Workshop 3732022272). That mod draws
// installed augments on a creature's 3D model, but only for human and possessed
// actors. Robots use cyborg actors, so their augments stay invisible. While
// Visible Augments builds the parts for a roster robot, its actor id is lent to
// that mod's supported list, then taken back, so enemy cyborgs are unchanged.
// No reference to the other DLL: everything is found by name and skipped if absent.
//
// On humans that mod also erases the skin under each new part so the old body
// does not poke through. Robots get the same: each cyborg body has an owner map
// (Assets/VA_<actor>.png, built from the mod's part UVs, which match the robot
// meshes) saying which body part every skin texel belongs to. Texels owned only
// by covered parts are made transparent on a per-robot copy of the skin.
public static class VisibleAugmentsCompat
{
    private const string AssemblyName = "VisibleAugments";
    public const int Head = 1, Arms = 2, Legs = 4, Hip = 8, Chest = 16, Guts = 32;

    private static bool _patched;
    private static HashSet<string> _supported;
    private static IList _groups;
    private static AccessTools.FieldRef<Creature3dView, MeshRenderer> _meshRenderer;
    private static readonly Dictionary<string, byte[]> OwnerMaps = new Dictionary<string, byte[]>();
    private static readonly Dictionary<int, SkinState> Skins = new Dictionary<int, SkinState>();

    private sealed class SkinState
    {
        public Creature3dView View;
        public Texture Original;
        public Texture2D Generated;
        public int Covered;
    }

    public static bool ShouldLend(bool isRobot, string actorId, ICollection<string> supported) =>
        isRobot && !string.IsNullOrEmpty(actorId) && supported != null && !supported.Contains(actorId);

    // Body parts hidden by one Visible Augments group, read from what it erases on humans.
    public static int PartsFor(IEnumerable<string> uvRegions, IEnumerable<string> masks)
    {
        int parts = 0;
        foreach (var region in uvRegions ?? Enumerable.Empty<string>())
            parts |= region == "HEAD" ? Head : region == "ARMS" ? Arms : region == "LEGS" ? Legs : region == "TORSO" ? Chest | Guts : 0;
        foreach (var mask in masks ?? Enumerable.Empty<string>())
        {
            var name = mask.ToLowerInvariant();
            parts |= name.Contains("hip") ? Hip : name.Contains("chest") ? Chest : name.Contains("guts") ? Guts : 0;
        }
        return parts;
    }

    // A texel is erased only when every part using it is covered, so a shared
    // texel never leaves a hole in a part that is still showing.
    public static bool Erases(int owners, int covered) => owners != 0 && (owners & ~covered) == 0;

    public static void TryPatch()
    {
        if (_patched) return;
        try
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == AssemblyName);
            var type = assembly?.GetType("Plugin");
            if (type == null) return;
            _patched = true;
            _supported = AccessTools.Field(type, "_supportedActorIds")?.GetValue(null) as HashSet<string>;
            var spawn = AccessTools.Method(type, "SpawnAugmentObjects", new[] { typeof(Creature), typeof(Creature3dView), typeof(long) });
            if (_supported == null || spawn == null)
            {
                Plugin.Logger.LogWarning("Visible Augments found but its layout changed; robots will not show augments.");
                return;
            }
            var harmony = new Harmony(Plugin.HarmonyId);
            harmony.Patch(spawn,
                prefix: new HarmonyMethod(typeof(VisibleAugmentsCompat), nameof(Prefix)),
                finalizer: new HarmonyMethod(typeof(VisibleAugmentsCompat), nameof(Finalizer)));
            Plugin.Logger.Log("Visible Augments found; robots show their augments.");

            _groups = AccessTools.Field(type, "_groups")?.GetValue(null) as IList;
            var skin = AccessTools.Method(type, "UpdateSkinTexture", new[] { typeof(Creature), typeof(Creature3dView), typeof(long) });
            if (_groups == null || skin == null)
            {
                Plugin.Logger.LogWarning("Visible Augments skin layout changed; robot bodies under augments are not hidden.");
                return;
            }
            _meshRenderer = AccessTools.FieldRefAccess<Creature3dView, MeshRenderer>("_meshRenderer");
            harmony.Patch(skin, postfix: new HarmonyMethod(typeof(VisibleAugmentsCompat), nameof(SkinPostfix)));
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError("Visible Augments compatibility failed; robots will not show augments.");
            Plugin.Logger.LogException(ex);
        }
    }

    public static void Prefix(Creature creature, out string __state)
    {
        __state = null;
        var data = creature?.CreatureData;
        if (data == null || !ShouldLend(RobotUpgrades.IsRobot(data), data.ActorId, _supported)) return;
        _supported.Add(data.ActorId);
        __state = data.ActorId;
    }

    // A finalizer so the id is taken back even if their code throws.
    public static void Finalizer(string __state)
    {
        if (__state != null) _supported?.Remove(__state);
    }

    // Visible Augments calls this every frame for a creature with augments
    // (and once more when they are gone); it does nothing for cyborg actors.
    public static void SkinPostfix(Creature creature, Creature3dView view, long flags)
    {
        try
        {
            var data = creature?.CreatureData;
            if (data == null || view == null || !RobotUpgrades.IsRobot(data)) return;
            var owners = OwnerMap(data.ActorId);
            if (owners == null) return;
            var material = _meshRenderer(view)?.sharedMaterial;
            if (material == null) return;
            int id = data.UniqueId;
            int covered = Covered(flags);
            if (Skins.TryGetValue(id, out var state) && state.View != view)
            {
                if (state.Generated != null) UnityEngine.Object.Destroy(state.Generated);
                Skins.Remove(id);
                state = null;
            }
            if (covered == 0)
            {
                if (state == null) return;
                material.SetTexture("_MainTex", state.Original);
                if (state.Generated != null) UnityEngine.Object.Destroy(state.Generated);
                Skins.Remove(id);
                return;
            }
            if (state == null)
            {
                var original = material.GetTexture("_MainTex");
                if (original == null) return;
                state = new SkinState { View = view, Original = original };
                Skins[id] = state;
            }
            if (state.Covered != covered || state.Generated == null)
            {
                if (state.Generated != null) UnityEngine.Object.Destroy(state.Generated);
                state.Generated = Erase(state.Original, owners, covered);
                state.Covered = covered;
            }
            // The game can put the stock skin back when it redraws gear.
            if (material.GetTexture("_MainTex") != state.Generated) material.SetTexture("_MainTex", state.Generated);
        }
        catch (Exception ex) { Plugin.Logger.LogException(ex); }
    }

    private static List<KeyValuePair<long, int>> _groupParts;

    private static int Covered(long flags)
    {
        // Their groups never change after load, so read them once.
        _groupParts ??= _groups.Cast<object>().Select(g => new KeyValuePair<long, int>(
            Traverse.Create(g).Field<long>("Flag").Value,
            PartsFor(Traverse.Create(g).Field<List<string>>("UvRegionsMale").Value, Traverse.Create(g).Field<List<string>>("MasksMale").Value))).ToList();
        int covered = 0;
        foreach (var group in _groupParts)
            if ((flags & group.Key) != 0) covered |= group.Value;
        return covered;
    }

    private static byte[] OwnerMap(string actorId)
    {
        if (string.IsNullOrEmpty(actorId)) return null;
        if (OwnerMaps.TryGetValue(actorId, out var map)) return map;
        map = null;
        using (var stream = typeof(Plugin).Assembly.GetManifestResourceStream("QuasiRobotics.VA_" + actorId + ".png"))
            if (stream != null)
            {
                var bytes = new MemoryStream();
                stream.CopyTo(bytes);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                ImageConversion.LoadImage(texture, bytes.ToArray());
                map = texture.GetPixels32().Select(c => c.r).ToArray();
                if (texture.width != OwnerSize || texture.height != OwnerSize) map = null;
                UnityEngine.Object.Destroy(texture);
            }
        OwnerMaps[actorId] = map;
        return map;
    }

    private const int OwnerSize = 256;

    private static Texture2D Erase(Texture source, byte[] owners, int covered)
    {
        int w = source.width, h = source.height;
        var temporary = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        var active = RenderTexture.active;
        Graphics.Blit(source, temporary);
        RenderTexture.active = temporary;
        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = source.filterMode, wrapMode = source.wrapMode };
        copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        RenderTexture.active = active;
        RenderTexture.ReleaseTemporary(temporary);
        var pixels = copy.GetPixels32();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (Erases(owners[(y * OwnerSize / h) * OwnerSize + x * OwnerSize / w], covered))
                    pixels[y * w + x] = new Color32(0, 0, 0, 0);
        copy.SetPixels32(pixels);
        copy.Apply();
        return copy;
    }
}
