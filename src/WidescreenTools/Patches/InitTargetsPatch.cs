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

        internal static int ReplacedMinCalls { get; private set; }

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> InitTargets_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo minInt = AccessTools.Method(typeof(Math), nameof(Math.Min), new[] { typeof(int), typeof(int) });
            MethodInfo chooseAxisMethod = AccessTools.Method(typeof(InitTargetsPatch), nameof(ChooseTargetAxis));
            int replaced = 0;

            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(minInt))
                {
                    replaced++;
                    yield return new CodeInstruction(OpCodes.Call, chooseAxisMethod);
                    continue;
                }

                yield return instruction;
            }

            ReplacedMinCalls = replaced;
        }

        private static int ChooseTargetAxis(int backBufferAxis, int maxWorldViewAxis)
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
