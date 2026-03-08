using HarmonyLib;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics;

namespace WidescreenTools.Patches
{
    [HarmonyPatch(typeof(SpriteViewMatrix), "set_Zoom")]
    internal static class SpriteViewMatrixZoomSetterPatch
    {
        private static void Prefix(SpriteViewMatrix __instance, ref Vector2 value)
        {
            if (!WidescreenZoomOverride.IsCustomZoomRangeEnabled())
            {
                return;
            }

            if (!ReferenceEquals(__instance, Main.GameViewMatrix))
            {
                return;
            }

            float mappedZoomTarget = WidescreenZoomOverride.MapVanillaZoomToConfigured(Main.GameZoomTarget);
            value = new Vector2(Main.ForcedMinimumZoom * mappedZoomTarget);
        }
    }
}
