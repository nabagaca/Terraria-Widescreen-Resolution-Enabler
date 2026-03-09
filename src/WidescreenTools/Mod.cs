using System;
using System.Reflection;
using System.Windows.Forms;
using Terraria;
using TerrariaModder.Core;
using TerrariaModder.Core.Events;
using TerrariaModder.Core.Logging;
using WidescreenTools.Patches;

namespace WidescreenTools
{
    public class Mod : IMod
    {
        internal static Mod Instance { get; private set; }

        public string Id => "widescreen-tools";
        public string Name => "Widescreen Tools";
        public string Version => "0.3.0";

        private ILogger _log;
        private ModContext _context;
        private bool _enabled;
        private bool _overrideForcedMinimumZoom;
        private bool _enableCustomZoomRange;
        private bool _unlockHighResModes;
        private bool _persistResolution;
        private float _zoomRangeMultiplier;
        private float _effectiveZoomRangeMultiplier = float.NaN;
        private int _desiredResolutionWidth;
        private int _desiredResolutionHeight;
        private int _worldViewWidth;
        private int _worldViewHeight;
        private bool _pendingApply = true;
        private bool _startupResolutionHandled;
        private int _lastObservedWidth;
        private int _lastObservedHeight;
        private bool _resolutionOverridesApplied;
        private bool _worldViewApplyDirty = true;
        private bool _pendingResolutionSave;
        private DateTime _lastResolutionChangeUtc;
        private float _lastClampRequestedMultiplier = float.NaN;
        private int _lastClampReferenceWidth = -1;
        private int _lastClampReferenceHeight = -1;
        private int _lastClampRenderTargetMax = -1;
        private float _lastClampEffectiveMultiplier = float.NaN;
        private int _lastAppliedWorldViewWidth = -1;
        private int _lastAppliedWorldViewHeight = -1;
        private MethodInfo _setResolutionMethod;

        public void Initialize(ModContext context)
        {
            Instance = this;
            _log = context.Logger;
            _context = context;

            WidescreenZoomOverride.Initialize(_log);
            WidescreenResolutionOverride.Initialize(_log);
            _setResolutionMethod = typeof(Main).GetMethod("SetResolution", new[] { typeof(int), typeof(int) });
            LoadConfigValues();
            FrameEvents.OnPostUpdate += OnPostUpdate;

            _log.Info($"{Name} v{Version} initialized");
        }

        public void OnWorldLoad()
        {
            _pendingApply = true;
            _worldViewApplyDirty = true;
        }

        public void OnWorldUnload()
        {
            _pendingApply = true;
            _worldViewApplyDirty = true;
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
            _pendingApply = true;
            _worldViewApplyDirty = true;
        }

        private void OnPostUpdate()
        {
            if (_pendingApply)
            {
                _pendingApply = false;
                ApplyConfiguredOverrides();
            }

            TrackResolutionChanges();
            FlushPendingResolutionSave();
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

            // Derive the world-view zoom reference from the desired resolution.
            // When no resolution is configured (0), fall back to the native display size.
            _worldViewWidth = _desiredResolutionWidth;
            _worldViewHeight = _desiredResolutionHeight;

            if (_worldViewWidth <= 0 || _worldViewHeight <= 0)
            {
                var primary = Screen.PrimaryScreen;
                if (primary != null)
                {
                    if (_worldViewWidth <= 0) _worldViewWidth = primary.Bounds.Width;
                    if (_worldViewHeight <= 0) _worldViewHeight = primary.Bounds.Height;
                }
            }

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
            ApplyResolutionOverrides(force: false);
            ApplySavedResolution();
            ApplyEffectiveZoomRange();

            if (!_enabled || !_overrideForcedMinimumZoom)
            {
                WidescreenZoomOverride.RestoreOriginal();
                _log.Info("[WidescreenTools] Forced minimum zoom override disabled");
                _worldViewApplyDirty = true;
                return;
            }

            int worldViewWidth = _worldViewWidth;
            int worldViewHeight = _worldViewHeight;
            worldViewWidth = WidescreenZoomOverride.ExpandWorldViewWidthForZoom(worldViewWidth, Main.screenWidth);
            worldViewHeight = WidescreenZoomOverride.ExpandWorldViewHeightForZoom(worldViewHeight, Main.screenHeight);

            if (!_worldViewApplyDirty &&
                _lastAppliedWorldViewWidth == worldViewWidth &&
                _lastAppliedWorldViewHeight == worldViewHeight)
            {
                return;
            }

            if (WidescreenZoomOverride.Apply(worldViewWidth, worldViewHeight))
            {
                _worldViewApplyDirty = false;
                if (_lastAppliedWorldViewWidth != worldViewWidth || _lastAppliedWorldViewHeight != worldViewHeight)
                {
                    _lastAppliedWorldViewWidth = worldViewWidth;
                    _lastAppliedWorldViewHeight = worldViewHeight;
                    _log.Info($"[WidescreenTools] Forced minimum zoom comparer set to {worldViewWidth}x{worldViewHeight}");
                }
            }
        }

        private void ApplyEffectiveZoomRange()
        {
            if (!_enabled || !_enableCustomZoomRange)
            {
                if (float.IsNaN(_effectiveZoomRangeMultiplier) || Math.Abs(_effectiveZoomRangeMultiplier - 1f) > 0.0001f)
                {
                    _effectiveZoomRangeMultiplier = 1f;
                    WidescreenZoomOverride.ConfigureZoomRange(false, 1f);
                }

                return;
            }

            int clampReferenceWidth = Math.Max(Main.screenWidth, _desiredResolutionWidth);
            int clampReferenceHeight = Math.Max(Main.screenHeight, _desiredResolutionHeight);
            float requestedMultiplier = _zoomRangeMultiplier;
            int renderTargetMax = WidescreenZoomOverride.GetCurrentRenderTargetMaxSize();
            bool clampInputsChanged =
                Math.Abs(_lastClampRequestedMultiplier - requestedMultiplier) > 0.0001f ||
                _lastClampReferenceWidth != clampReferenceWidth ||
                _lastClampReferenceHeight != clampReferenceHeight ||
                _lastClampRenderTargetMax != renderTargetMax;

            float effectiveMultiplier;
            if (clampInputsChanged)
            {
                effectiveMultiplier = WidescreenZoomOverride.ClampMultiplierForCurrentResolution(
                    requestedMultiplier,
                    clampReferenceWidth,
                    clampReferenceHeight,
                    out _,
                    out _);

                _lastClampRequestedMultiplier = requestedMultiplier;
                _lastClampReferenceWidth = clampReferenceWidth;
                _lastClampReferenceHeight = clampReferenceHeight;
                _lastClampRenderTargetMax = renderTargetMax;
                _lastClampEffectiveMultiplier = effectiveMultiplier;
            }
            else
            {
                effectiveMultiplier = _lastClampEffectiveMultiplier;
            }

            if (effectiveMultiplier < requestedMultiplier - 0.0001f &&
                (float.IsNaN(_effectiveZoomRangeMultiplier) || Math.Abs(_effectiveZoomRangeMultiplier - effectiveMultiplier) > 0.0001f))
            {
                _log.Info($"[WidescreenTools] Clamped zoomRangeMultiplier {requestedMultiplier:0.###} -> {effectiveMultiplier:0.###} for clamp reference {clampReferenceWidth}x{clampReferenceHeight}");
            }

            _effectiveZoomRangeMultiplier = effectiveMultiplier;
            WidescreenZoomOverride.ConfigureZoomRange(_enabled && _enableCustomZoomRange, effectiveMultiplier);
        }

        private void ApplyResolutionOverrides(bool force)
        {
            if (!_enabled || !_unlockHighResModes)
            {
                _resolutionOverridesApplied = false;
                return;
            }

            if (!force && _resolutionOverridesApplied)
            {
                return;
            }

            if (WidescreenResolutionOverride.Apply())
            {
                _resolutionOverridesApplied = true;
            }
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
            _worldViewApplyDirty = true;
            _pendingResolutionSave = true;
            _lastResolutionChangeUtc = DateTime.UtcNow;

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
        }

        private void FlushPendingResolutionSave()
        {
            if (!_pendingResolutionSave)
            {
                return;
            }

            if ((DateTime.UtcNow - _lastResolutionChangeUtc).TotalMilliseconds < 500)
            {
                return;
            }

            _pendingResolutionSave = false;

            if (!_enabled || !_unlockHighResModes || !_persistResolution || _context?.Config == null)
            {
                return;
            }

            if (!_startupResolutionHandled || _desiredResolutionWidth <= 0 || _desiredResolutionHeight <= 0)
            {
                return;
            }

            _context.Config.Set("desiredResolutionWidth", _desiredResolutionWidth);
            _context.Config.Set("desiredResolutionHeight", _desiredResolutionHeight);
            _context.Config.Save();
            _log.Info($"[WidescreenTools] Saved resolution {_desiredResolutionWidth}x{_desiredResolutionHeight}");
        }

        private bool TrySetResolution(int width, int height)
        {
            try
            {
                if (_setResolutionMethod == null)
                {
                    _log.Warn("[WidescreenTools] Failed to find Main.SetResolution(int, int)");
                    return false;
                }

                _setResolutionMethod.Invoke(null, new object[] { width, height });
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

        internal void NotifyResolutionOverridesApplied()
        {
            _resolutionOverridesApplied = true;
        }

        private static void SetPendingResolution(int width, int height)
        {
            Main.PendingResolutionWidth = width;
            Main.PendingResolutionHeight = height;
        }
    }
}
