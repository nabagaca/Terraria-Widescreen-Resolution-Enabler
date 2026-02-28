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

        public static void SetResolution(int width, int height)
        {
            screenWidth = width;
            screenHeight = height;
            PendingResolutionWidth = width;
            PendingResolutionHeight = height;
        }
    }
}

