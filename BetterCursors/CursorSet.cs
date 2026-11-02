using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using EFT.UI;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BetterCursors
{
    internal sealed class BuiltCursor
    {
        public Texture2D Texture;
        public Vector2 HotSpot;
    }

    internal static class CursorSet
    {
        private const string HotSpotFile = "hotspots.txt";
        private const string ExportFolder = "Game Export";

        private const float GameArrowHeight = 43f;

        internal static readonly Dictionary<ECursorType, string> FileNames = new Dictionary<ECursorType, string>
        {
            { ECursorType.Idle, "idle" },
            { ECursorType.Hover, "hover" },
            { ECursorType.Hand, "hand" },
            { ECursorType.SetPin, "set_pin" },
            { ECursorType.SetLock, "set_lock" },
            { ECursorType.StretchHorizontal, "stretch_horizontal" },
            { ECursorType.StretchVertical, "stretch_vertical" },
            { ECursorType.StretchCorner, "stretch_corner" },
            { ECursorType.StretchCorner2, "stretch_corner_2" }
        };

        private static readonly Dictionary<ECursorType, BuiltCursor> Cache = new Dictionary<ECursorType, BuiltCursor>();
        private static readonly List<Texture2D> Retired = new List<Texture2D>();
        private static Dictionary<string, Vector2> _hotSpots;
        private static string _cacheKey;

        public static void Invalidate()
        {
            foreach (var built in Cache.Values)
            {
                if (built != null)
                    Retired.Add(built.Texture);
            }

            Cache.Clear();
            _hotSpots = null;
            _cacheKey = null;
        }

        public static void DestroyRetired()
        {
            foreach (var texture in Retired)
            {
                if (texture != null)
                    Object.Destroy(texture);
            }

            Retired.Clear();
        }

        public static BuiltCursor Get(ECursorType type, CursorSwitcher.CursorData game)
        {
            bool handHover = type == ECursorType.Hover && Plugin.Hover.Value == HoverStyle.Hand;
            var tint = type == ECursorType.Hover && !handHover ? Plugin.HoverColour.Value : Plugin.Colour.Value;
            var key = Plugin.Set.Value + "|" + Plugin.Size.Value + "|" + Plugin.Hover.Value + "|"
                      + ColorUtility.ToHtmlStringRGB(Plugin.Colour.Value) + ColorUtility.ToHtmlStringRGB(Plugin.HoverColour.Value);
            if (_cacheKey != key)
            {
                Invalidate();
                _cacheKey = key;
            }

            if (Cache.TryGetValue(type, out var built))
                return built;

            var shape = type == ECursorType.Hover ? handHover ? ECursorType.Hand : ECursorType.Idle : type;
            var source = game;
            if (shape != type && CursorSwitcher._cursors != null
                && CursorSwitcher._cursors.TryGetValue(shape, out var other) && other != null)
                source = other;

            try
            {
                built = Plugin.Set.Value == Plugin.Game ? FromGame(source, tint) : FromSet(shape, tint) ?? FromGame(source, tint);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"Could not build the {type} cursor from {Plugin.Set.Value}, using the game's: {e}");
                built = null;
            }

            if (built != null || source?.Texture != null)
                Cache[type] = built;
            return built;
        }

        private static BuiltCursor FromSet(ECursorType type, Color tint)
        {
            if (!FileNames.TryGetValue(type, out var name))
                return null;

            var folder = Path.Combine(Plugin.CursorsFolder, Plugin.Set.Value);
            var path = Path.Combine(folder, name + ".png");

            if (!File.Exists(path) && (type == ECursorType.SetPin || type == ECursorType.SetLock))
            {
                name = FileNames[ECursorType.Idle];
                path = Path.Combine(folder, name + ".png");
            }

            if (!File.Exists(path))
                return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path), false))
                {
                    Plugin.Log.LogWarning($"{path} is not an image the game can read.");
                    return null;
                }

                var pixels = texture.GetPixels32();
                int w = texture.width, h = texture.height;
                float scale = Plugin.Size.Value / (float)Math.Max(w, h);
                return Build(name, pixels, w, h, scale, HotSpotFraction(folder, name, type), tint);
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        private static BuiltCursor FromGame(CursorSwitcher.CursorData game, Color tint)
        {
            var source = game?.Texture;
            if (source == null)
                return null;

            var pixels = ReadPixels(source);
            int w = source.width, h = source.height;
            float scale = Plugin.Size.Value / GameArrowHeight;
            return Build(source.name, pixels, w, h, scale, new Vector2(game.HotSpot.x / w, game.HotSpot.y / h), tint);
        }

        private static BuiltCursor Build(string name, Color32[] pixels, int w, int h, float scale, Vector2 hotSpot, Color tint)
        {
            Multiply(pixels, tint);

            int dw = Math.Max(1, Mathf.RoundToInt(w * scale));
            int dh = Math.Max(1, Mathf.RoundToInt(h * scale));
            var texture = new Texture2D(dw, dh, TextureFormat.RGBA32, false)
            {
                name = "BetterCursors_" + name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(dw == w && dh == h ? pixels : Resampler.Resize(pixels, w, h, dw, dh));

            texture.Apply(false, false);
            var spot = new Vector2(Mathf.Clamp(Mathf.Round(hotSpot.x * dw), 0, dw - 1), Mathf.Clamp(Mathf.Round(hotSpot.y * dh), 0, dh - 1));
            return new BuiltCursor { Texture = texture, HotSpot = spot };
        }

        private static void Multiply(Color32[] pixels, Color colour)
        {
            Color32 c = colour;
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                pixels[i] = new Color32((byte)(p.r * c.r / 255), (byte)(p.g * c.g / 255), (byte)(p.b * c.b / 255), p.a);
            }
        }

        private static Vector2 HotSpotFraction(string folder, string name, ECursorType type)
        {
            if (_hotSpots == null)
                _hotSpots = ReadHotSpots(Path.Combine(folder, HotSpotFile));
            if (_hotSpots.TryGetValue(name, out var spot))
                return spot;

            switch (type)
            {
                case ECursorType.StretchHorizontal:
                case ECursorType.StretchVertical:
                case ECursorType.StretchCorner:
                case ECursorType.StretchCorner2:
                    return new Vector2(0.5f, 0.5f);
                case ECursorType.Hand:
                    return new Vector2(0.45f, 0.1f);
                default:
                    return Vector2.zero;
            }
        }

        private static Dictionary<string, Vector2> ReadHotSpots(string path)
        {
            var spots = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(path))
                return spots;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;

                var parts = line.Split('=');
                var xy = parts.Length == 2 ? parts[1].Split(',') : null;
                if (xy == null || xy.Length != 2
                    || !float.TryParse(xy[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
                    || !float.TryParse(xy[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
                {
                    Plugin.Log.LogWarning($"Skipping line \"{line}\" in {path}, expected name = x, y.");
                    continue;
                }

                spots[parts[0].Trim()] = new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
            }

            return spots;
        }

        private static Color32[] ReadPixels(Texture2D source)
        {
            if (source.isReadable)
                return source.GetPixels32();

            // game textures aren't readable
            int w = source.width, h = source.height;
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
            var previous = RenderTexture.active;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                copy.Apply(false, false);
                return copy.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.Destroy(copy);
            }
        }

        public static void ExportGame()
        {
            var cursors = CursorSwitcher._cursors;
            if (cursors == null)
            {
                Plugin.Log.LogWarning("The game's cursors aren't loaded yet, try again from the main menu.");
                return;
            }

            var folder = Path.Combine(Plugin.CursorsFolder, ExportFolder);
            Directory.CreateDirectory(folder);
            var lines = new List<string> { "# Where each cursor clicks, as a fraction of the image width and height from the top left." };
            int saved = 0;
            foreach (var pair in FileNames)
            {
                if (pair.Key == ECursorType.Hover || !cursors.TryGetValue(pair.Key, out var data) || data?.Texture == null)
                    continue;

                var source = data.Texture;
                var png = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
                try
                {
                    png.SetPixels32(ReadPixels(source));
                    png.Apply(false, false);
                    File.WriteAllBytes(Path.Combine(folder, pair.Value + ".png"), png.EncodeToPNG());
                }
                finally
                {
                    Object.Destroy(png);
                }

                lines.Add(string.Format(CultureInfo.InvariantCulture, "{0} = {1:0.####}, {2:0.####}",
                    pair.Value, data.HotSpot.x / source.width, data.HotSpot.y / source.height));
                saved++;
            }

            File.WriteAllLines(Path.Combine(folder, HotSpotFile), lines);
            Plugin.Log.LogInfo($"Saved {saved} game cursors to {folder}.");
        }
    }
}
