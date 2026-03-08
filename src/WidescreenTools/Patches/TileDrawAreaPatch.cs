using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Drawing;

namespace WidescreenTools.Patches
{
    [HarmonyPatch(typeof(TileDrawing), "GetScreenDrawArea")]
    internal static class TileDrawAreaPatch
    {
        [HarmonyPostfix]
        private static void GetScreenDrawArea_Postfix(bool useOffscreenRange, ref Vector2 drawOffSet, ref int firstTileX, ref int lastTileX, ref int firstTileY, ref int lastTileY)
        {
            // Disabled: expanding this path triggers expensive section frame/refresh work and hurts performance.
        }
    }
}
