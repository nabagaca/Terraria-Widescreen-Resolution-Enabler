using System;
using Terraria;
using TerrariaModder.Core;
using TerrariaModder.Core.Events;
using TerrariaModder.Core.Logging;
using Terraria.Graphics.Light;
using WidescreenTools.Patches;

namespace WidescreenTools
{
    public class Mod : IMod
    {
        internal static Mod Instance { get; private set; }

        public string Id => "widescreen-tools";
        public string Name => "Widescreen Tools";
        public string Version => "0.1.0";

        private ILogger _log;
        private ModContext _context;
        private bool _enabled;
        private bool _overrideForcedMinimumZoom;
        private bool _enableCustomZoomRange;
        private bool _unlockHighResModes;
        private bool _persistResolution;
        private float _zoomRangeMultiplier;
        private int _desiredResolutionWidth;
        private int _desiredResolutionHeight;
        private int _worldViewWidth;
        private int _worldViewHeight;
        private bool _pendingApply = true;
        private bool _startupResolutionHandled;
        private int _lastObservedWidth;
        private int _lastObservedHeight;
        private bool _zoomDebugLogged;
        private float _lastLoggedRawZoomTarget = float.NaN;
        private float _lastLoggedMappedZoomTarget = float.NaN;

        public void Initialize(ModContext context)
        {
            Instance = this;
            _log = context.Logger;
            _context = context;

            WidescreenZoomOverride.Initialize(_log);
            WidescreenResolutionOverride.Initialize(_log);
            LoadConfigValues();
            ApplyResolutionOverrides();
            FrameEvents.OnPostUpdate += OnPostUpdate;

            _log.Info($"{Name} v{Version} initialized");
        }

        public void OnWorldLoad()
        {
        }

        public void OnWorldUnload()
        {
        }

        public void Unload()
        {
            Instance = null;
            FrameEvents.OnPostUpdate -= OnPostUpdate;
            WidescreenZoomOverride.RestoreOriginal();
        }

        public void OnConfigChanged()
        {
            LoadConfigValues();
            _zoomDebugLogged = false;
            _pendingApply = true;
        }

        private void OnPostUpdate()
        {
            if (_pendingApply)
            {
                _pendingApply = false;
                ApplyConfiguredOverrides();
            }

            TrackResolutionChanges();
            LogZoomStateChanges();
        }

        private void LoadConfigValues()
        {
            if (_context?.Config == null)
            {
                return;
            }

            _enabled = _context.Config.Get("enabled", true);
            _overrideForcedMinimumZoom = _context.Config.Get("overrideForcedMinimumZoom", true);
            _enableCustomZoomRange = _context.Config.Get("enableCustomZoomRange", false);
            _unlockHighResModes = _context.Config.Get("unlockHighResModes", true);
            _persistResolution = _context.Config.Get("persistResolution", true);
            _zoomRangeMultiplier = _context.Config.Get("zoomRangeMultiplier", 1f);
            _desiredResolutionWidth = _context.Config.Get("desiredResolutionWidth", 0);
            _desiredResolutionHeight = _context.Config.Get("desiredResolutionHeight", 0);
            _worldViewWidth = _context.Config.Get("worldViewWidth", 5120);
            _worldViewHeight = _context.Config.Get("worldViewHeight", 1440);
            WidescreenZoomOverride.ConfigureZoomRange(_enabled && _enableCustomZoomRange, _zoomRangeMultiplier);

            if (_worldViewWidth < WidescreenZoomOverride.VanillaWidth)
            {
                _worldViewWidth = WidescreenZoomOverride.VanillaWidth;
            }

            if (_worldViewHeight < WidescreenZoomOverride.VanillaHeight)
            {
                _worldViewHeight = WidescreenZoomOverride.VanillaHeight;
            }
        }

        private void ApplyConfiguredOverrides()
        {
            ApplyResolutionOverrides();
            ApplySavedResolution();
            EnsureLightingModeForExtendedZoom();

            if (!_enabled || !_overrideForcedMinimumZoom)
            {
                WidescreenZoomOverride.RestoreOriginal();
                _log.Info("[WidescreenTools] Forced minimum zoom override disabled");
                return;
            }

            int worldViewWidth = _worldViewWidth;
            int worldViewHeight = _worldViewHeight;
            worldViewWidth = WidescreenZoomOverride.ExpandWorldViewWidthForZoom(worldViewWidth, Main.screenWidth);
            worldViewHeight = WidescreenZoomOverride.ExpandWorldViewHeightForZoom(worldViewHeight, Main.screenHeight);

            if (WidescreenZoomOverride.Apply(worldViewWidth, worldViewHeight))
            {
                _log.Info($"[WidescreenTools] Forced minimum zoom comparer set to {worldViewWidth}x{worldViewHeight}");
            }

            if (!_zoomDebugLogged)
            {
                _zoomDebugLogged = true;
                _log.Info($"[WidescreenTools] Zoom range config: enabled={_enableCustomZoomRange}, multiplier={_zoomRangeMultiplier:0.###}, mappedRange={WidescreenZoomOverride.GetZoomTargetMin():0.###}-{WidescreenZoomOverride.GetZoomTargetMax():0.###}");
                _log.Info($"[WidescreenTools] Lighting mode newEngine={Lighting.UsingNewLighting}");
                _log.Info($"[WidescreenTools] Zoom setter hook adjusted={SpriteViewMatrixZoomSetterPatch.HasAdjustedGameViewZoom}, count={SpriteViewMatrixZoomSetterPatch.AdjustCount}");
                _log.Info($"[WidescreenTools] InitTargets min->max replacements={InitTargetsPatch.ReplacedMinCalls}");
                _log.Info($"[WidescreenTools] Tile draw range expanded={TileDrawAreaPatch.HasExpandedTileRange}, count={TileDrawAreaPatch.ExpansionCount}, lastFactor={TileDrawAreaPatch.LastRevealFactor:0.###}, lastTiles={TileDrawAreaPatch.LastExpandedWidth}x{TileDrawAreaPatch.LastExpandedHeight}");
                _log.Info($"[WidescreenTools] AreaToLight expanded={AreaToLightPatch.HasExpandedAreaToLight}, count={AreaToLightPatch.ExpansionCount}, lastFactor={AreaToLightPatch.LastRevealFactor:0.###}, lastTiles={AreaToLightPatch.LastWidth}x{AreaToLightPatch.LastHeight}");
            }
        }

        private void ApplyResolutionOverrides()
        {
            if (!_enabled || !_unlockHighResModes)
            {
                return;
            }

            WidescreenResolutionOverride.Apply();
        }

        private void ApplySavedResolution()
        {
            if (!_enabled || !_unlockHighResModes || !_persistResolution)
            {
                _startupResolutionHandled = true;
                return;
            }

            if (_desiredResolutionWidth <= 0 || _desiredResolutionHeight <= 0)
            {
                _startupResolutionHandled = true;
                return;
            }

            if (Main.screenWidth == _desiredResolutionWidth && Main.screenHeight == _desiredResolutionHeight)
            {
                SetPendingResolution(_desiredResolutionWidth, _desiredResolutionHeight);
                _startupResolutionHandled = true;
                _lastObservedWidth = Main.screenWidth;
                _lastObservedHeight = Main.screenHeight;
                return;
            }

            if (TrySetResolution(_desiredResolutionWidth, _desiredResolutionHeight))
            {
                SetPendingResolution(_desiredResolutionWidth, _desiredResolutionHeight);
                _startupResolutionHandled = true;
                _lastObservedWidth = Main.screenWidth;
                _lastObservedHeight = Main.screenHeight;
                _log.Info($"[WidescreenTools] Restored saved resolution {_desiredResolutionWidth}x{_desiredResolutionHeight}");
                return;
            }

            _startupResolutionHandled = true;
        }

        private void TrackResolutionChanges()
        {
            if (Main.screenWidth <= 0 || Main.screenHeight <= 0)
            {
                return;
            }

            bool changed = Main.screenWidth != _lastObservedWidth || Main.screenHeight != _lastObservedHeight;
            if (!changed)
            {
                return;
            }

            _lastObservedWidth = Main.screenWidth;
            _lastObservedHeight = Main.screenHeight;
            _pendingApply = true;

            if (!_enabled || !_unlockHighResModes || !_persistResolution || _context?.Config == null)
            {
                return;
            }

            if (!_startupResolutionHandled)
            {
                return;
            }

            if (_desiredResolutionWidth == Main.screenWidth && _desiredResolutionHeight == Main.screenHeight)
            {
                return;
            }

            _desiredResolutionWidth = Main.screenWidth;
            _desiredResolutionHeight = Main.screenHeight;
            _context.Config.Set("desiredResolutionWidth", _desiredResolutionWidth);
            _context.Config.Set("desiredResolutionHeight", _desiredResolutionHeight);
            _context.Config.Save();
            _log.Info($"[WidescreenTools] Saved resolution {_desiredResolutionWidth}x{_desiredResolutionHeight}");
        }

        private void LogZoomStateChanges()
        {
            if (!_enabled || !_overrideForcedMinimumZoom || !WidescreenZoomOverride.IsCustomZoomRangeEnabled())
            {
                return;
            }

            float rawZoomTarget = WidescreenZoomOverride.GetCurrentGameZoomTarget();
            float mappedZoomTarget = WidescreenZoomOverride.MapVanillaZoomToConfigured(rawZoomTarget);

            if (Math.Abs(rawZoomTarget - _lastLoggedRawZoomTarget) < 0.0001f &&
                Math.Abs(mappedZoomTarget - _lastLoggedMappedZoomTarget) < 0.0001f)
            {
                return;
            }

            _lastLoggedRawZoomTarget = rawZoomTarget;
            _lastLoggedMappedZoomTarget = mappedZoomTarget;
            _log.Info($"[WidescreenTools] Zoom target raw={rawZoomTarget:0.###} mapped={mappedZoomTarget:0.###} (range {WidescreenZoomOverride.GetZoomTargetMin():0.###}-{WidescreenZoomOverride.GetZoomTargetMax():0.###})");
            _log.Info($"[WidescreenTools] Lighting mode newEngine={Lighting.UsingNewLighting}");
            _log.Info($"[WidescreenTools] Tile draw expansion state: expanded={TileDrawAreaPatch.HasExpandedTileRange}, count={TileDrawAreaPatch.ExpansionCount}, factor={TileDrawAreaPatch.LastRevealFactor:0.###}, tiles={TileDrawAreaPatch.LastExpandedWidth}x{TileDrawAreaPatch.LastExpandedHeight}");
            _log.Info($"[WidescreenTools] AreaToLight state: expanded={AreaToLightPatch.HasExpandedAreaToLight}, count={AreaToLightPatch.ExpansionCount}, factor={AreaToLightPatch.LastRevealFactor:0.###}, tiles={AreaToLightPatch.LastWidth}x{AreaToLightPatch.LastHeight}");
        }

        private void EnsureLightingModeForExtendedZoom()
        {
            if (!_enabled || !_enableCustomZoomRange)
            {
                return;
            }

            if (Lighting.UsingNewLighting)
            {
                return;
            }

            try
            {
                Lighting.Mode = LightMode.Color;
                _log.Warn("[WidescreenTools] Switched Lighting.Mode to Color for extended zoom support");
            }
            catch (Exception ex)
            {
                _log.Warn($"[WidescreenTools] Failed to switch Lighting.Mode to Color: {ex.Message}");
            }
        }

        private bool TrySetResolution(int width, int height)
        {
            try
            {
                var setResolution = typeof(Main).GetMethod("SetResolution", new[] { typeof(int), typeof(int) });
                if (setResolution == null)
                {
                    _log.Warn("[WidescreenTools] Failed to find Main.SetResolution(int, int)");
                    return false;
                }

                setResolution.Invoke(null, new object[] { width, height });
                return true;
            }
            catch (Exception ex)
            {
                _log.Warn($"[WidescreenTools] Failed to restore saved resolution {width}x{height}: {ex.Message}");
                return false;
            }
        }

        internal void ApplySavedResolutionFromCache()
        {
            if (_startupResolutionHandled)
            {
                return;
            }

            ApplySavedResolution();
        }

        private static void SetPendingResolution(int width, int height)
        {
            Main.PendingResolutionWidth = width;
            Main.PendingResolutionHeight = height;
        }
    }
}
