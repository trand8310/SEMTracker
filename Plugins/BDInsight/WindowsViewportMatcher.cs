

namespace BDInsight
{
    using System;
    using System.Linq;

    public sealed class WindowsDeviceProfileResult
    {
        public int PhysicalWidth { get; set; }
        public int PhysicalHeight { get; set; }

        public int CssWidth { get; set; }
        public int CssHeight { get; set; }

        public float DeviceScaleFactor { get; set; }

        public double ScaleX { get; set; }
        public double ScaleY { get; set; }

        public double Score { get; set; }

        public override string ToString()
        {
            return $"Physical={PhysicalWidth}x{PhysicalHeight}, CSS={CssWidth}x{CssHeight}, Scale={DeviceScaleFactor:F2}, Score={Score:F6}";
        }
    }

    public static class WindowsViewportMatcher
    {
        // 常见 Windows 桌面逻辑分辨率（CSS 视口）
        private static readonly (int CssW, int CssH)[] Profiles =
        {
            (1920, 1080),
            (1536, 864),   // 1920x1080 @125%
            (1280, 720),   // 1920x1080 @150%
            (1097, 617),   // 1920x1080 @175% 近似值
            (960, 540),    // 1920x1080 @200%

            (2560, 1440),
            (2048, 1152),  // 2560x1440 @125%
            (1707, 960),   // 2560x1440 @150% 近似值
            (1463, 823),   // 2560x1440 @175% 近似值
            (1280, 720),   // 2560x1440 @200%

            (1366, 768),
            (1600, 900),
            (1440, 900),
            (1280, 800),
            (1280, 720),
            (1024, 768),

            (3840, 2160),
            (3072, 1728),  // 3840x2160 @125%
            (2560, 1440),  // 3840x2160 @150%
            (2194, 1234),  // 3840x2160 @175% 近似值
            (1920, 1080),  // 3840x2160 @200%
        };

        // 常见 Windows 缩放档位
        private static readonly float[] CommonScales =
        {
            1.0f,
            1.25f,
            1.5f,
            1.75f,
            2.0f
        };

        public static WindowsDeviceProfileResult Match(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "分辨率必须大于 0");

            // Windows 默认按横屏处理
            int physicalWidth = Math.Max(width, height);
            int physicalHeight = Math.Min(width, height);

            WindowsDeviceProfileResult? best = null;

            foreach (var p in Profiles)
            {
                double scaleX = (double)physicalWidth / p.CssW;
                double scaleY = (double)physicalHeight / p.CssH;
                double avgScale = (scaleX + scaleY) / 2.0;

                // 过滤明显不合理的 Windows 缩放
                if (avgScale < 0.9 || avgScale > 2.1)
                    continue;

                // 分数1：横纵缩放差异越小越好
                double score = Math.Abs(scaleX - scaleY);

                // 分数2：轻微偏向常见桌面分辨率
                score += GetResolutionPenalty(p.CssW, p.CssH);

                // 分数3：轻微偏向标准 Windows 缩放档位
                score += GetScalePenalty(avgScale);

                var result = new WindowsDeviceProfileResult
                {
                    PhysicalWidth = physicalWidth,
                    PhysicalHeight = physicalHeight,
                    CssWidth = p.CssW,
                    CssHeight = p.CssH,
                    DeviceScaleFactor = NormalizeScale((float)avgScale),
                    ScaleX = scaleX,
                    ScaleY = scaleY,
                    Score = score
                };

                if (best == null || result.Score < best.Score)
                    best = result;
            }

            if (best == null)
            {
                // 兜底：优先按 100% 缩放处理
                return new WindowsDeviceProfileResult
                {
                    PhysicalWidth = physicalWidth,
                    PhysicalHeight = physicalHeight,
                    CssWidth = physicalWidth,
                    CssHeight = physicalHeight,
                    DeviceScaleFactor = 1.0f,
                    ScaleX = 1.0,
                    ScaleY = 1.0,
                    Score = 999
                };
            }

            return best;
        }

        private static double GetResolutionPenalty(int cssWidth, int cssHeight)
        {
            return (cssWidth, cssHeight) switch
            {
                (1920, 1080) => 0.0000,
                (1536, 864) => 0.0008,
                (1366, 768) => 0.0010,
                (1600, 900) => 0.0011,
                (1280, 720) => 0.0012,
                (2560, 1440) => 0.0015,
                (2048, 1152) => 0.0017,
                (1440, 900) => 0.0020,
                (1280, 800) => 0.0022,
                (1024, 768) => 0.0030,
                _ => 0.0050
            };
        }

        private static double GetScalePenalty(double scale)
        {
            double nearestDistance = CommonScales.Min(x => Math.Abs(x - scale));
            return nearestDistance * 0.02; // 轻惩罚，不抢主评分
        }

        private static float NormalizeScale(float scale)
        {
            foreach (var common in CommonScales)
            {
                if (Math.Abs(scale - common) <= 0.08f)
                    return common;
            }

            return (float)Math.Round(scale, 3, MidpointRounding.AwayFromZero);
        }
    }
}
