using HarmonyLib;
using Terraria;
using Terraria.Graphics;

namespace WidescreenTools.Patches
{
    [HarmonyPatch(typeof(Main), "RenderToTargets")]
    internal static class RenderToTargetsSafetyPatch
    {
        [HarmonyPrefix]
        private static bool RenderToTargets_Prefix()
        {
            return Main.targetSet;
        }
    }

    [HarmonyPatch(typeof(WorldSceneLayerTarget), "UpdateContent")]
    internal static class WorldSceneLayerTargetUpdateContentSafetyPatch
    {
        [HarmonyPrefix]
        private static bool UpdateContent_Prefix()
        {
            return Main.targetSet;
        }
    }
}
