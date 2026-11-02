using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace BetterCursors
{
    public enum DrawMode
    {
        Software,
        Hardware
    }

    public enum HoverStyle
    {
        Normal,
        Hand
    }

    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency("com.SPT.core", BepInDependency.DependencyFlags.HardDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.fums.bettercursors";
        public const string Name = "Fums-BetterCursors";
        public const string Version = "1.0.0";

        internal const string Off = "Off";
        internal const string Game = "Game";

        internal static ManualLogSource Log;
        internal static string CursorsFolder;

        internal static ConfigEntry<string> Set;
        internal static ConfigEntry<int> Size;
        internal static ConfigEntry<DrawMode> Mode;
        internal static ConfigEntry<HoverStyle> Hover;
        internal static ConfigEntry<Color> Colour;
        internal static ConfigEntry<Color> HoverColour;
        internal static ConfigEntry<bool> Export;

        private void Awake()
        {
            Log = Logger;
            CursorsFolder = Path.Combine(Path.GetDirectoryName(Info.Location) ?? "", "cursors");

            var sets = Directory.Exists(CursorsFolder)
                ? Directory.GetDirectories(CursorsFolder).Select(Path.GetFileName).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray()
                : new string[0];
            var choices = new[] { Off, Game }.Concat(sets).ToArray();

            Set = Config.Bind("1. Cursor", "Cursor set", Off,
                new ConfigDescription("Which cursors to use. Off: leave the game's cursor alone (the default). "
                                      + "Game: the game's own cursors, drawn at the size below. "
                                      + "The rest are the folders in BepInEx/plugins/BetterCursors/cursors. "
                                      + "Add a folder of your own there and restart the game to see it here.",
                    new AcceptableValueList<string>(choices),
                    new ConfigurationManagerAttributes { Order = 100 }));
            Size = Config.Bind("1. Cursor", "Size (pixels)", 40,
                new ConfigDescription("How big the cursor is on screen, in pixels across its longest side. "
                                      + "The game's own arrow is drawn at about 41.",
                    new AcceptableValueRange<int>(16, 256),
                    new ConfigurationManagerAttributes { Order = 99 }));
            Mode = Config.Bind("1. Cursor", "Draw mode", DrawMode.Software,
                new ConfigDescription("Software: the game draws the cursor itself, at exactly the size above, "
                                      + "whatever the Windows pointer size is. "
                                      + "Hardware: Windows draws it, which follows the mouse a frame sooner, "
                                      + "but Windows may stretch it to its own pointer size and blur it.",
                    null,
                    new ConfigurationManagerAttributes { Order = 98 }));
            Hover = Config.Bind("2. Colour", "Hover style", HoverStyle.Normal,
                new ConfigDescription("What the cursor does over something you can click, such as an item or a button. "
                                      + "Normal: it keeps its shape and turns the hover colour. "
                                      + "Hand: it turns into the set's pointing hand, in the cursor's colour, like a Windows link cursor. "
                                      + "Hover colour isn't used with Hand.",
                    null,
                    new ConfigurationManagerAttributes { Order = 97 }));
            Colour = Config.Bind("2. Colour", "Colour", Color.white,
                new ConfigDescription("The cursor's colour, for every cursor but hover. The light parts of the cursor take the colour "
                                      + "and dark outlines stay dark, so white leaves it as drawn. "
                                      + "Click the colour to open its sliders, or type a hex code such as #4da3ff.",
                    null,
                    new ConfigurationManagerAttributes { Order = 96, CustomDrawer = ColourField.Draw }));
            HoverColour = Config.Bind("2. Colour", "Hover colour", (Color)new Color32(0xff, 0xaa, 0x3c, 0xff),
                new ConfigDescription("The cursor's colour when it's over something you can click, such as an item or a button. "
                                      + "Only used when Hover style is Normal. "
                                      + "Click the colour to open its sliders, or type a hex code such as #4da3ff.",
                    null,
                    new ConfigurationManagerAttributes { Order = 95, CustomDrawer = ColourField.Draw }));
            Export = Config.Bind("3. Tools", "Export game cursors", false,
                new ConfigDescription("Tick to save the game's own cursors as PNG files into cursors/Game Export, "
                                      + "ready to edit or upscale. Restart the game to pick the folder as a set. "
                                      + "Shown with the config manager's advanced settings.",
                    null,
                    new ConfigurationManagerAttributes { Order = 90, IsAdvanced = true }));

            var harmony = new Harmony(Guid);
            try
            {
                CursorPatches.Apply(harmony);
            }
            catch (Exception e)
            {
                harmony.UnpatchSelf();
                Logger.LogError($"{Name} {Version} could not patch the game, the cursor is left alone: {e}");
                return;
            }

            Set.SettingChanged += (_, __) => Changed();
            Size.SettingChanged += (_, __) => Changed();
            Mode.SettingChanged += (_, __) => CursorPatches.Reapply();
            Hover.SettingChanged += (_, __) => Changed();
            Colour.SettingChanged += (_, __) => Changed();
            HoverColour.SettingChanged += (_, __) => Changed();
            Export.SettingChanged += (_, __) =>
            {
                if (!Export.Value)
                    return;
                Export.Value = false;
                CursorSet.ExportGame();
            };
            Logger.LogInfo($"{Name} {Version} loaded, {sets.Length} cursor set(s) found.");
        }

        private static void Changed()
        {
            CursorSet.Invalidate();
            CursorPatches.Reapply();
            CursorSet.DestroyRetired();
        }
    }
}
