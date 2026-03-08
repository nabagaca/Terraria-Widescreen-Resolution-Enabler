using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;

namespace WidescreenTools.Patches
{
    [HarmonyPatch(typeof(Main), "GetAreaToLight")]
    internal static class AreaToLightPatch
    {
        private static int _cachedViewWidth = -1;
        private static int _cachedViewHeight = -1;
        private static int _cachedTargetTilesWide;
        private static int _cachedTargetTilesHigh;

        [HarmonyPostfix]
        private static void GetAreaToLight_Postfix(ref Rectangle __result)
        {
            if (!WidescreenZoomOverride.HasExpandedZoomRange())
            {
                return;
            }

            // Legacy lighting allocates fixed buffers from unscaled screen size.
            // Inflating area there can cause out-of-range indexing in LegacyLighting.GetColor.
            if (!Lighting.UsingNewLighting)
            {
                return;
            }

            UpdateCachedTileTargets();
            int targetTilesWide = _cachedTargetTilesWide;
            int targetTilesHigh = _cachedTargetTilesHigh;
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
        }

        private static void UpdateCachedTileTargets()
        {
            int viewWidth = Main.MaxWorldViewSize.X;
            int viewHeight = Main.MaxWorldViewSize.Y;
            if (viewWidth == _cachedViewWidth && viewHeight == _cachedViewHeight)
            {
                return;
            }

            _cachedViewWidth = viewWidth;
            _cachedViewHeight = viewHeight;
            _cachedTargetTilesWide = (int)System.Math.Ceiling(viewWidth / 16f) + 4;
            _cachedTargetTilesHigh = (int)System.Math.Ceiling(viewHeight / 16f) + 4;
        }
    }
}
