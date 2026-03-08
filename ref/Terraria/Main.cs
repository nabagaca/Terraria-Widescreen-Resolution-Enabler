using System;
using Microsoft.Xna.Framework;
using Terraria.GameContent.Drawing;
using Terraria.Graphics;

namespace Terraria
{
    public class Main
    {
        public static int screenWidth;
        public static int screenHeight;
        public static int PendingResolutionWidth;
        public static int PendingResolutionHeight;
        public static object instance;
        public static bool dedServ;
        public static bool targetSet;
        public static int maxTilesX = 8400;
        public static int maxTilesY = 2400;
        public static int offScreenRange = 192;
        public static int maxScreenW = 3840;
        public static int maxScreenH = 2160;
        public static int minScreenW = 800;
        public static int minScreenH = 600;
        public static int[] displayWidth = new int[256];
        public static int[] displayHeight = new int[256];
        public static int numDisplayModes;
        public static float ForcedMinimumZoom = 1f;
        public static float GameZoomTarget = 1f;
        public static SpriteViewMatrix GameViewMatrix = new SpriteViewMatrix();
        public static Point MaxWorldViewSize = new Point(3839, 1200);
        private static int _renderTargetMaxSize = 4096;

        public static void SetResolution(int width, int height)
        {
            screenWidth = width;
            screenHeight = height;
            PendingResolutionWidth = width;
            PendingResolutionHeight = height;
        }

        public static void CacheSupportedDisplaySizes()
        {
        }

        private static void RegisterDisplayResolution(int width, int height)
        {
            if (numDisplayModes >= displayWidth.Length || numDisplayModes >= displayHeight.Length)
            {
                return;
            }

            displayWidth[numDisplayModes] = width;
            displayHeight[numDisplayModes] = height;
            numDisplayModes++;
        }

        public static void InitTargets()
        {
        }

        public static void RenderToTargets()
        {
        }

        public static Rectangle GetAreaToLight()
        {
            return new Rectangle(0, 0, 0, 0);
        }
    }

    public static class Lighting
    {
        public static bool UsingNewLighting = true;
    }
}

namespace Terraria.Graphics
{
    using Microsoft.Xna.Framework;

    public class SpriteViewMatrix
    {
        private Vector2 _zoom;

        public Vector2 Zoom
        {
            get => _zoom;
            set => _zoom = value;
        }
    }

    public class WorldSceneLayerTarget
    {
        public void UpdateContent(Action render)
        {
            render?.Invoke();
        }
    }
}

namespace Terraria.GameContent.Drawing
{
    using Microsoft.Xna.Framework;

    public class TileDrawing
    {
        public void GetScreenDrawArea(bool useOffscreenRange, ref Vector2 drawOffSet, ref int firstTileX, ref int lastTileX, ref int firstTileY, ref int lastTileY)
        {
        }
    }
}
