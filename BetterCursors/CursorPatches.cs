using System;
using EFT.UI;
using HarmonyLib;
using UnityEngine;

namespace BetterCursors
{
    internal static class CursorPatches
    {
        public static void Apply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(CursorSwitcher), nameof(CursorSwitcher.SetCursor), new[] { typeof(CursorSwitcher.CursorData) })
                         ?? throw new MissingMethodException("CursorSwitcher.SetCursor(CursorData)");
            harmony.Patch(target, prefix: new HarmonyMethod(typeof(CursorPatches), nameof(SetCursorPrefix)));
        }

        private static bool SetCursorPrefix(CursorSwitcher.CursorData cursor)
        {
            if (Plugin.Set.Value == Plugin.Off || cursor == null)
                return true;

            var type = TypeOf(cursor);
            if (type == null || type == ECursorType.Invisible)
                return true;

            var built = CursorSet.Get(type.Value, cursor);
            if (built == null)
                return true;

            var mode = Plugin.Mode.Value == DrawMode.Software ? CursorMode.ForceSoftware : CursorMode.Auto;
            Cursor.SetCursor(built.Texture, built.HotSpot, mode);
            return false;
        }

        public static void Reapply()
        {
            var cursors = CursorSwitcher._cursors;
            if (cursors == null || !cursors.TryGetValue(CursorSwitcher._currentCursor, out var current) || current == null)
                return;
            CursorSwitcher.SetCursor(current);
        }

        private static ECursorType? TypeOf(CursorSwitcher.CursorData cursor)
        {
            var cursors = CursorSwitcher._cursors;
            if (cursors == null)
                return null;
            foreach (var pair in cursors)
            {
                if (ReferenceEquals(pair.Value, cursor))
                    return pair.Key;
            }

            return null;
        }
    }
}
