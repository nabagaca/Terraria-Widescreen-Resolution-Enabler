using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace WidescreenTools.Patches
{
    [HarmonyPatch(typeof(Main), "GetAreaToLight")]
    internal static class AreaToLightPatch
    {
        internal static bool HasExpandedAreaToLight { get; private set; }
        internal static int ExpansionCount { get; private set; }
        internal static float LastRevealFactor { get; private set; }
        internal static int LastWidth { get; private set; }
        internal static int LastHeight { get; private set; }

        [HarmonyPostfix]
        private static void GetAreaToLight_Postfix(ref Rectangle __result)
        {
            if (!WidescreenZoomOverride.IsCustomZoomRangeEnabled())
            {
                return;
            }

            // Legacy lighting allocates fixed buffers from unscaled screen size.
            // Inflating area there can cause out-of-range indexing in LegacyLighting.GetColor.
            if (!Lighting.UsingNewLighting)
            {
                return;
            }

            int targetTilesWide = (int)System.Math.Ceiling(Main.MaxWorldViewSize.X / 16f) + 4;
            int targetTilesHigh = (int)System.Math.Ceiling(Main.MaxWorldViewSize.Y / 16f) + 4;
            if (__result.Width >= targetTilesWide && __result.Height >= targetTilesHigh)
            {
                return;
            }

            int centerX = __result.Left + __result.Width / 2;
            int centerY = __result.Top + __result.Height / 2;
            int halfW = targetTilesWide / 2;
            int halfH = targetTilesHigh / 2;
            int left = centerX - halfW;
            int right = left + targetTilesWide;
            int top = centerY - halfH;
            int bottom = top + targetTilesHigh;

            if (left < 4)
            {
                right += 4 - left;
                left = 4;
            }

            if (top < 4)
            {
                bottom += 4 - top;
                top = 4;
            }

            int maxRight = Main.maxTilesX - 4;
            int maxBottom = Main.maxTilesY - 4;
            if (right > maxRight)
            {
                int delta = right - maxRight;
                left -= delta;
                right = maxRight;
            }

            if (bottom > maxBottom)
            {
                int delta = bottom - maxBottom;
                top -= delta;
                bottom = maxBottom;
            }

            if (left < 4)
            {
                left = 4;
            }

            if (top < 4)
            {
                top = 4;
            }

            if (right <= left || bottom <= top)
            {
                return;
            }

            __result = new Rectangle(left, top, right - left, bottom - top);
            HasExpandedAreaToLight = true;
            ExpansionCount++;
            LastRevealFactor = Main.screenWidth > 0 ? Main.MaxWorldViewSize.X / (float)Main.screenWidth : 1f;
            LastWidth = __result.Width;
            LastHeight = __result.Height;
        }
    }
}
