using TerrariaModder.Core.Config;

namespace WidescreenTools
{
    public class WidescreenToolsConfig : ModConfig
    {
        public override int Version => 1;

        [Client, Label("Enabled"), Description("Enable widescreen zoom overrides")]
        public bool Enabled { get; set; } = true;

        [Client, Label("Override Forced Zoom"), Description("Replace Terraria's vanilla world-view limit used to force zoom on large resolutions")]
        public bool OverrideForcedMinimumZoom { get; set; } = true;

        [Client, Label("Enable Custom Zoom Range"), Description("Allow zooming beyond Terraria's default 100%-200% target range")]
        public bool EnableCustomZoomRange { get; set; } = false;

        [Client, Range(1.0, 4.0), Label("Zoom Range Multiplier"), Description("Range size multiplier around vanilla zoom range; 2.0 gives roughly 50%-250%")]
        public float ZoomRangeMultiplier { get; set; } = 1f;

        [Client, Label("Unlock High Res Modes"), Description("Raise Terraria's internal resolution caps and register native monitor resolutions")]
        public bool UnlockHighResModes { get; set; } = true;

        [Client, Label("Persist Resolution"), Description("Save the active resolution in Widescreen Tools config and restore it on startup")]
        public bool PersistResolution { get; set; } = true;

        [Client, Range(0, 8192), Label("Resolution Width"), Description("Target display resolution width; also used as the world-view zoom reference. 0 = use native display resolution. Saved automatically when you change resolution in-game.")]
        public int DesiredResolutionWidth { get; set; } = 0;

        [Client, Range(0, 8192), Label("Resolution Height"), Description("Target display resolution height; also used as the world-view zoom reference. 0 = use native display resolution. Saved automatically when you change resolution in-game.")]
        public int DesiredResolutionHeight { get; set; } = 0;
    }
}
