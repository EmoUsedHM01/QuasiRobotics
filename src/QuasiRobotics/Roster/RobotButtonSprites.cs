using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace QuasiRobotics;

// Exact native UI sprites, extracted without resampling. Explicit UI rect sizes
// keep their footprint independent of the game's sprite pixels-per-unit settings.
internal static class RobotButtonSprites
{
    private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

    // The game's locked tree icons are darker copies of the unlocked ones.
    public static Sprite GetDimmed(string name, float brightness)
    {
        string key = name + "#dim";
        if (cache.TryGetValue(key, out var sprite) && sprite != null) return sprite;
        var source = Get(name).texture;
        var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        var pixels = source.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32((byte)(pixels[i].r * brightness), (byte)(pixels[i].g * brightness), (byte)(pixels[i].b * brightness), pixels[i].a);
        texture.SetPixels32(pixels);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.name = key;
        sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
        sprite.name = key;
        Object.DontDestroyOnLoad(texture);
        Object.DontDestroyOnLoad(sprite);
        return cache[key] = sprite;
    }

    public static bool Exists(string name) =>
        typeof(Plugin).Assembly.GetManifestResourceInfo("QuasiRobotics." + name + ".png") != null;

    public static Sprite Get(string name)
    {
        if (cache.TryGetValue(name, out var sprite) && sprite != null) return sprite;
        using (var stream = typeof(Plugin).Assembly.GetManifestResourceStream("QuasiRobotics." + name + ".png"))
        using (var bytes = new MemoryStream())
        {
            if (stream == null) throw new FileNotFoundException("Embedded sprite " + name + ".png is missing.");
            stream.CopyTo(bytes);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(texture, bytes.ToArray());
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = name;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f), 100);
            sprite.name = name;
            Object.DontDestroyOnLoad(texture);
            Object.DontDestroyOnLoad(sprite);
            return cache[name] = sprite;
        }
    }
}
