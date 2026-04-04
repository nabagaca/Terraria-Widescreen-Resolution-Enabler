using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Terraria;

namespace WidescreenTools.Patches
{
    [HarmonyPatch(typeof(Main), "InitTargets")]
    internal static class InitTargetsPatch
    {
        private const int MaximumSafeRenderTargetSize = 8192;
        private static readonly FieldInfo RenderTargetMaxSizeField = AccessTools.Field(typeof(Main), "_renderTargetMaxSize");
        private static readonly FieldInfo MaxWorldViewSizeField = AccessTools.Field(typeof(Main), "MaxWorldViewSize");

        internal static int ReplacedMinCalls { get; private set; }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> InitTargets_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo minInt = AccessTools.Method(typeof(Math), nameof(Math.Min), new[] { typeof(int), typeof(int) });
            MethodInfo chooseWidthMethod = AccessTools.Method(typeof(InitTargetsPatch), nameof(ChooseTargetWidth));
            MethodInfo chooseHeightMethod = AccessTools.Method(typeof(InitTargetsPatch), nameof(ChooseTargetHeight));
            int replaced = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(minInt))
                {
                    replaced++;
                    // The first Math.Min call caps the width; the second caps the height.
                    // Vanilla uses MaxWorldViewSize.X for both, but we must use .Y for height
                    // so the render target height isn't blown out to screen-width size.
                    MethodInfo target = replaced == 1 ? chooseWidthMethod : chooseHeightMethod;
                    yield return new CodeInstruction(OpCodes.Call, target);
                    continue;
                }

                yield return instruction;
            }

            ReplacedMinCalls = replaced;
        }

        // Used for the first Math.Min replacement (BackBufferWidth vs MaxWorldViewSize.X).
        private static int ChooseTargetWidth(int backBufferWidth, int maxWorldViewX)
        {
            return ChooseAxis(backBufferWidth, maxWorldViewX);
        }

        // Used for the second Math.Min replacement (BackBufferHeight vs MaxWorldViewSize.X in
        // vanilla IL). We ignore the stacked .X value and use MaxWorldViewSize.Y instead, so the
        // render target height stays proportional to the actual screen height rather than becoming
        // as tall as the screen is wide.
        private static int ChooseTargetHeight(int backBufferHeight, int maxWorldViewXIgnored)
        {
            int maxWorldViewY = GetMaxWorldViewY();
            return ChooseAxis(backBufferHeight, maxWorldViewY);
        }

        private static int ChooseAxis(int backBufferAxis, int maxWorldViewAxis)
        {
            int desired = Math.Max(backBufferAxis, maxWorldViewAxis);
            int renderTargetMax = GetRenderTargetMaxSize();
            if (renderTargetMax > MaximumSafeRenderTargetSize)
            {
                renderTargetMax = MaximumSafeRenderTargetSize;
            }

            // Keep some room for off-screen range so InitTargets won't compute a negative offScreenRange.
            int maxAxis = renderTargetMax - 64;
            if (maxAxis < backBufferAxis)
            {
                return backBufferAxis;
            }

            if (desired > maxAxis)
            {
                desired = maxAxis;
            }

            return desired;
        }

        private static int GetMaxWorldViewY()
        {
            try
            {
                object point = MaxWorldViewSizeField?.GetValue(null);
                if (point != null)
                {
                    FieldInfo yField = point.GetType().GetField("Y");
                    if (yField?.GetValue(point) is int y && y > 0)
                    {
                        return y;
                    }
                }
            }
            catch
            {
            }

            return WidescreenZoomOverride.VanillaHeight;
        }

        private static int GetRenderTargetMaxSize()
        {
            try
            {
                if (RenderTargetMaxSizeField?.GetValue(null) is int value && value > 0)
                {
                    return value;
                }
            }
            catch
            {
            }

            return MaximumSafeRenderTargetSize;
        }
    }
}
