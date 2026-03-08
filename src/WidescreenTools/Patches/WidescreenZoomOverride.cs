using System;
using System.Reflection;
using Terraria;
using TerrariaModder.Core.Logging;

namespace WidescreenTools.Patches
{
    internal static class WidescreenZoomOverride
    {
        public const int VanillaWidth = 3839;
        public const int VanillaHeight = 1200;
        private const float VanillaZoomMin = 1f;
        private const float VanillaZoomMax = 2f;
        private const float MinimumAllowedZoom = 0.1f;
        private const float MinimumRangeSpan = 0.01f;
        private const float ZoomOutSafetyPadding = 0f;
        private const int WorldViewSafetyMargin = 64;

        private static ILogger _log;
        private static FieldInfo _maxWorldViewSizeField;
        private static FieldInfo _gameZoomTargetField;
        private static FieldInfo _renderTargetMaxSizeField;
        private static FieldInfo _targetSetField;
        private static ConstructorInfo _pointConstructor;
        private static FieldInfo _dedServField;
        private static bool _initialized;
        private static bool _capturedOriginal;
        private static object _originalValue;
        private static bool _customZoomRangeEnabled;
        private static float _zoomRangeMultiplier = 1f;
        private static float _zoomTargetMin = VanillaZoomMin;
        private static float _zoomTargetMax = VanillaZoomMax;

        public static void Initialize(ILogger log)
        {
            _log = log;

            if (_initialized)
            {
                return;
            }

            _maxWorldViewSizeField = typeof(Main).GetField("MaxWorldViewSize", BindingFlags.Public | BindingFlags.Static);
            _gameZoomTargetField = typeof(Main).GetField("GameZoomTarget", BindingFlags.Public | BindingFlags.Static);
            _renderTargetMaxSizeField = typeof(Main).GetField("_renderTargetMaxSize", BindingFlags.NonPublic | BindingFlags.Static);
            _targetSetField = typeof(Main).GetField("targetSet", BindingFlags.Public | BindingFlags.Static);
            _pointConstructor = _maxWorldViewSizeField?.FieldType.GetConstructor(new[] { typeof(int), typeof(int) });
            _dedServField = typeof(Main).GetField("dedServ", BindingFlags.Public | BindingFlags.Static);
            _initialized = true;

            if (_maxWorldViewSizeField == null || _pointConstructor == null)
            {
                _log?.Warn("[WidescreenTools] Could not resolve Terraria.Main.MaxWorldViewSize");
            }
        }

        public static bool Apply(int width, int height)
        {
            if (!EnsureField())
            {
                return false;
            }

            int targetWidth = ClampWorldViewAxis(Math.Max(width, VanillaWidth));
            int targetHeight = ClampWorldViewAxis(Math.Max(height, VanillaHeight));
            if (targetWidth != width || targetHeight != height)
            {
                _log?.Warn($"[WidescreenTools] Clamped world-view request {width}x{height} -> {targetWidth}x{targetHeight} for render-target safety");
            }

            object target = CreatePoint(targetWidth, targetHeight);
            if (target == null)
            {
                return false;
            }

            if (!TrySetValue(target, "apply"))
            {
                return false;
            }

            EnsureRenderTargetCapacity(targetWidth, targetHeight);
            RequestRenderTargetRefresh();
            ClampCurrentZoomTarget();
            return true;
        }

        public static void RestoreOriginal()
        {
            if (!EnsureField())
            {
                return;
            }

            if (_capturedOriginal)
            {
                if (TrySetValue(_originalValue, "restore"))
                {
                    RequestRenderTargetRefresh();
                }
            }
            else
            {
                object vanillaPoint = CreatePoint(VanillaWidth, VanillaHeight);
                if (vanillaPoint != null && TrySetValue(vanillaPoint, "restore"))
                {
                    RequestRenderTargetRefresh();
                }
            }
        }

        public static void ConfigureZoomRange(bool enabled, float multiplier)
        {
            _customZoomRangeEnabled = enabled;
            _zoomRangeMultiplier = SanitizeMultiplier(multiplier);
            RecalculateZoomBounds();
            ClampCurrentZoomTarget();
        }

        public static float ClampMultiplierForCurrentResolution(float requestedMultiplier, int screenWidth, int screenHeight, out float maxAllowedMultiplier, out float minZoomLimit)
        {
            maxAllowedMultiplier = 4f;
            minZoomLimit = MinimumAllowedZoom;
            float sanitized = SanitizeMultiplier(requestedMultiplier);

            int maxAxis = GetMaximumSafeWorldViewAxis();
            if (screenWidth <= 0 || maxAxis <= 0)
            {
                return sanitized;
            }

            float widthMinZoom = screenWidth / (float)maxAxis;
            float heightMinZoom = 0f;
            if (screenHeight > 0)
            {
                heightMinZoom = screenHeight / (float)maxAxis;
            }

            minZoomLimit = Math.Max(widthMinZoom, heightMinZoom) + ZoomOutSafetyPadding;
            if (minZoomLimit < MinimumAllowedZoom)
            {
                minZoomLimit = MinimumAllowedZoom;
            }

            if (minZoomLimit > VanillaZoomMax)
            {
                minZoomLimit = VanillaZoomMax;
            }

            maxAllowedMultiplier = 2f * (1.5f - minZoomLimit);
            if (maxAllowedMultiplier < 0.1f)
            {
                maxAllowedMultiplier = 0.1f;
            }

            if (maxAllowedMultiplier > 4f)
            {
                maxAllowedMultiplier = 4f;
            }

            if (sanitized > maxAllowedMultiplier)
            {
                return maxAllowedMultiplier;
            }

            return sanitized;
        }

        public static float GetZoomTargetMin()
        {
            return _zoomTargetMin;
        }

        public static float GetZoomTargetMax()
        {
            return _zoomTargetMax;
        }

        public static bool IsCustomZoomRangeEnabled()
        {
            return _customZoomRangeEnabled;
        }

        public static float ClampZoomTarget(float target)
        {
            if (target < VanillaZoomMin)
            {
                return VanillaZoomMin;
            }

            if (target > VanillaZoomMax)
            {
                return VanillaZoomMax;
            }

            return target;
        }

        public static float MapVanillaZoomToConfigured(float vanillaZoomTarget)
        {
            float clampedVanilla = ClampZoomTarget(vanillaZoomTarget);
            if (!_customZoomRangeEnabled)
            {
                return clampedVanilla;
            }

            float normalized = clampedVanilla - VanillaZoomMin;
            return _zoomTargetMin + (_zoomTargetMax - _zoomTargetMin) * normalized;
        }

        public static float GetCurrentGameZoomTarget()
        {
            if (_gameZoomTargetField == null)
            {
                return VanillaZoomMin;
            }

            try
            {
                if (_gameZoomTargetField.GetValue(null) is float target)
                {
                    return target;
                }
            }
            catch
            {
            }

            return VanillaZoomMin;
        }

        public static void ClampCurrentZoomTarget()
        {
            if (_gameZoomTargetField == null)
            {
                return;
            }

            try
            {
                object value = _gameZoomTargetField.GetValue(null);
                if (!(value is float target))
                {
                    return;
                }

                float clamped = ClampZoomTarget(target);
                if (Math.Abs(clamped - target) > 0.0001f)
                {
                    _gameZoomTargetField.SetValue(null, clamped);
                }
            }
            catch (Exception ex)
            {
                _log?.Warn($"[WidescreenTools] Failed to clamp GameZoomTarget: {ex.Message}");
            }
        }

        public static int ExpandWorldViewWidthForZoom(int width, int currentScreenWidth)
        {
            if (_zoomTargetMin >= 1f || currentScreenWidth <= 0)
            {
                return width;
            }

            int required = (int)Math.Ceiling(currentScreenWidth / (double)_zoomTargetMin);
            return Math.Max(width, required);
        }

        public static int ExpandWorldViewHeightForZoom(int height, int currentScreenHeight)
        {
            if (_zoomTargetMin >= 1f || currentScreenHeight <= 0)
            {
                return height;
            }

            int required = (int)Math.Ceiling(currentScreenHeight / (double)_zoomTargetMin);
            return Math.Max(height, required);
        }

        private static bool EnsureField()
        {
            if (!_initialized)
            {
                Initialize(_log);
            }

            return _maxWorldViewSizeField != null;
        }

        private static void RequestRenderTargetRefresh()
        {
            try
            {
                if (_dedServField?.GetValue(null) is bool dedicated && dedicated)
                {
                    return;
                }

                if (_targetSetField == null)
                {
                    return;
                }

                _targetSetField.SetValue(null, false);
            }
            catch (Exception ex)
            {
                _log?.Warn($"[WidescreenTools] Failed to request render-target rebuild: {ex.Message}");
            }
        }

        private static void EnsureRenderTargetCapacity(int worldViewWidth, int worldViewHeight)
        {
            if (_renderTargetMaxSizeField == null)
            {
                return;
            }

            try
            {
                int offscreen = Main.offScreenRange > 0 ? Main.offScreenRange : 192;
                int required = Math.Max(worldViewWidth, worldViewHeight) + offscreen * 2 + 64;

                if (!(_renderTargetMaxSizeField.GetValue(null) is int current))
                {
                    return;
                }

                if (required <= current)
                {
                    return;
                }

                _log?.Warn($"[WidescreenTools] Requested world-view needs render target size {required}, but current _renderTargetMaxSize is {current}; limiting zoom extension to hardware-safe range");
            }
            catch (Exception ex)
            {
                _log?.Warn($"[WidescreenTools] Failed to inspect _renderTargetMaxSize: {ex.Message}");
            }
        }

        private static object CreatePoint(int width, int height)
        {
            try
            {
                return _pointConstructor?.Invoke(new object[] { width, height });
            }
            catch (Exception ex)
            {
                _log?.Error($"[WidescreenTools] Failed to create Point({width}, {height}): {ex.Message}");
                return null;
            }
        }

        private static bool TrySetValue(object value, string operation)
        {
            try
            {
                if (!_capturedOriginal)
                {
                    _originalValue = _maxWorldViewSizeField.GetValue(null);
                    _capturedOriginal = true;
                }

                _maxWorldViewSizeField.SetValue(null, value);
                return true;
            }
            catch (FieldAccessException)
            {
                try
                {
                    var attributesField = typeof(FieldInfo).GetField("m_fieldAttributes", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (attributesField != null)
                    {
                        var attributes = (FieldAttributes)attributesField.GetValue(_maxWorldViewSizeField);
                        attributesField.SetValue(_maxWorldViewSizeField, attributes & ~FieldAttributes.InitOnly);
                    }

                    _maxWorldViewSizeField.SetValue(null, value);
                    return true;
                }
                catch (Exception ex)
                {
                    _log?.Error($"[WidescreenTools] Failed to {operation} MaxWorldViewSize: {ex.Message}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _log?.Error($"[WidescreenTools] Failed to {operation} MaxWorldViewSize: {ex.Message}");
                return false;
            }
        }

        private static void RecalculateZoomBounds()
        {
            if (!_customZoomRangeEnabled)
            {
                _zoomTargetMin = VanillaZoomMin;
                _zoomTargetMax = VanillaZoomMax;
                return;
            }

            float halfRange = 0.5f * _zoomRangeMultiplier;
            float min = 1.5f - halfRange;
            float max = 1.5f + halfRange;

            if (min < MinimumAllowedZoom)
            {
                min = MinimumAllowedZoom;
            }

            if (max <= min)
            {
                max = min + MinimumRangeSpan;
            }

            _zoomTargetMin = min;
            _zoomTargetMax = max;
        }

        private static float SanitizeMultiplier(float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier))
            {
                return 1f;
            }

            if (multiplier < 0.1f)
            {
                return 0.1f;
            }

            if (multiplier > 10f)
            {
                return 10f;
            }

            return multiplier;
        }

        private static int ClampWorldViewAxis(int requestedAxis)
        {
            int maxAxis = GetMaximumSafeWorldViewAxis();
            if (maxAxis < 1)
            {
                maxAxis = 1;
            }

            if (requestedAxis > maxAxis)
            {
                return maxAxis;
            }

            return requestedAxis;
        }

        private static int GetMaximumSafeWorldViewAxis()
        {
            int maxRenderTargetSize = GetRenderTargetMaxSize();
            int maxAxis = maxRenderTargetSize - WorldViewSafetyMargin;
            if (maxAxis < 1)
            {
                maxAxis = 1;
            }

            return maxAxis;
        }

        private static int GetRenderTargetMaxSize()
        {
            try
            {
                if (_renderTargetMaxSizeField?.GetValue(null) is int value && value > 0)
                {
                    return value;
                }
            }
            catch
            {
            }

            return 6186;
        }
    }
}
