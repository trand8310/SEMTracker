namespace BDInsight
{
    using System;
    using System.Diagnostics;
    using System.Linq;

    public sealed class DeviceProfileResult
    {
        public int PhysicalWidth { get; set; }
        public int PhysicalHeight { get; set; }

        public int CssWidth { get; set; }
        public int CssHeight { get; set; }

        public float DeviceScaleFactor { get; set; }

        public double DprX { get; set; }
        public double DprY { get; set; }

        public double Score { get; set; }

        public bool IsSuspiciousInput { get; set; }
        public bool IsFallback { get; set; }

        public override string ToString()
        {
            return $"Physical={PhysicalWidth}x{PhysicalHeight}, CSS={CssWidth}x{CssHeight}, DPR={DeviceScaleFactor:F3}, DprX={DprX:F4}, DprY={DprY:F4}, Score={Score:F6}, Suspicious={IsSuspiciousInput}, Fallback={IsFallback}";
        }
    }

    public static class AndroidViewportMatcher
    {
        // 只保留常见、正常、适合手机模拟的档位
        // 不要让结果落到奇怪的“近方屏”形态
        private static readonly (int CssW, int CssH)[] Profiles =
        {
            (360, 720), // 18:9，正常
            (360, 740),
            (360, 760),
            (360, 780),
            (360, 800), // 20:9

            (390, 844), // 19.5:9
            (393, 851),
            (393, 873),
            (392, 872),

            (411, 914),
            (412, 891),
            (412, 915),
        };

        // 常见安卓 DPR
        private static readonly float[] CommonDprs =
        {
            2.0f,
            2.25f,
            2.5f,
            2.625f,
            2.75f,
            3.0f,
            3.5f
        };

        // 正常手机比例范围（Height / Width）
        private const double MinPhoneRatio = 1.75;
        private const double MaxPhoneRatio = 2.50;

        // 只要低于这个值，就认为高度很可疑，不能太信任输入 height
        private const double SuspiciousRatioThreshold = 1.75;

        // 正常允许的 DPR 范围
        private const double MinDpr = 1.90;
        private const double MaxDpr = 3.60;

        public static DeviceProfileResult Match(int width, int height)
        {
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width), "分辨率必须大于 0");

            int physicalWidth = Math.Min(width, height);
            int physicalHeight = Math.Max(width, height);

            double inputRatio = (double)physicalHeight / physicalWidth;
            bool suspiciousInput = inputRatio < SuspiciousRatioThreshold || inputRatio > MaxPhoneRatio;

            DeviceProfileResult? best = null;

            foreach (var p in Profiles)
            {
                double profileRatio = (double)p.CssH / p.CssW;

                // 保证 profile 本身也是正常手机比例
                if (profileRatio < MinPhoneRatio || profileRatio > MaxPhoneRatio)
                    continue;

                double dprX = (double)physicalWidth / p.CssW;
                double dprY = (double)physicalHeight / p.CssH;
                double avgDpr = (dprX + dprY) / 2.0;

                if (avgDpr < MinDpr || avgDpr > MaxDpr)
                    continue;

                double score = 0;

                if (!suspiciousInput)
                {
                    // 输入比例正常时：宽高都参与评分
                    double dprDiff = Math.Abs(dprX - dprY);
                    double ratioDiff = Math.Abs(inputRatio - profileRatio);

                    score += dprDiff * 3.0;
                    score += ratioDiff * 5.0;
                }
                else
                {
                    // 输入比例可疑时：不要太信 physicalHeight
                    // 主要按 physicalWidth 去匹配一个正常手机模板
                    // 这样可避免算出“接近方屏”的怪结果

                    double widthDpr = (double)physicalWidth / p.CssW;
                    double snappedWidthDpr = SnapToNearestCommonDpr(widthDpr);
                    double widthDprDiff = Math.Abs(widthDpr - snappedWidthDpr);

                    // 偏向更长的正常手机比例，而不是接近方屏
                    double longScreenBonus = Math.Abs(profileRatio - 2.1);

                    // 这里故意弱化 dprY，因为输入 height 本身你已经怀疑有问题
                    score += widthDprDiff * 6.0;
                    score += longScreenBonus * 2.0;

                    // 如果按这个模板算出来 dprY 太离谱，也适当惩罚
                    score += Math.Abs(dprY - snappedWidthDpr) * 0.8;
                }

                // 轻微偏向主流宽度
                score += GetWidthPenalty(p.CssW);

                // 轻微偏向常见 DPR
                score += GetDprPenalty(avgDpr);

                var result = new DeviceProfileResult
                {
                    PhysicalWidth = physicalWidth,
                    PhysicalHeight = physicalHeight,
                    CssWidth = p.CssW,
                    CssHeight = p.CssH,
                    DeviceScaleFactor = NormalizeDpr((float)avgDpr),
                    DprX = dprX,
                    DprY = dprY,
                    Score = score,
                    IsSuspiciousInput = suspiciousInput,
                    IsFallback = false
                };

                if (best == null || result.Score < best.Score)
                    best = result;
            }

            if (best != null)
            {
                Debug.WriteLine("[AndroidViewportMatcher] Matched: " + best);
                return best;
            }

            var fallback = BuildSafeFallback(physicalWidth, physicalHeight, suspiciousInput);
            Debug.WriteLine("[AndroidViewportMatcher] Fallback: " + fallback);
            return fallback;
        }

        private static DeviceProfileResult BuildSafeFallback(int physicalWidth, int physicalHeight, bool suspiciousInput)
        {
            // fallback 也不自由计算高度
            // 只从正常手机模板里选，避免生成接近方屏的结果

            DeviceProfileResult? best = null;

            foreach (var p in Profiles)
            {
                double profileRatio = (double)p.CssH / p.CssW;
                if (profileRatio < MinPhoneRatio || profileRatio > MaxPhoneRatio)
                    continue;

                double widthDpr = (double)physicalWidth / p.CssW;
                double snapped = SnapToNearestCommonDpr(widthDpr);

                if (snapped < MinDpr || snapped > MaxDpr)
                    continue;

                double dprX = (double)physicalWidth / p.CssW;
                double dprY = (double)physicalHeight / p.CssH;

                double score = 0;

                // fallback 时，以宽度为主，尽量找最像正常手机的模板
                score += Math.Abs(widthDpr - snapped) * 8.0;
                score += Math.Abs(profileRatio - 2.1) * 2.0;
                score += GetWidthPenalty(p.CssW);

                var result = new DeviceProfileResult
                {
                    PhysicalWidth = physicalWidth,
                    PhysicalHeight = physicalHeight,
                    CssWidth = p.CssW,
                    CssHeight = p.CssH,
                    DeviceScaleFactor = NormalizeDpr((float)snapped),
                    DprX = dprX,
                    DprY = dprY,
                    Score = score + 100.0,
                    IsSuspiciousInput = suspiciousInput,
                    IsFallback = true
                };

                if (best == null || result.Score < best.Score)
                    best = result;
            }

            if (best != null)
                return best;

            // 理论上不会走到这里；真走到这里也给一个稳定正常值
            return new DeviceProfileResult
            {
                PhysicalWidth = physicalWidth,
                PhysicalHeight = physicalHeight,
                CssWidth = 360,
                CssHeight = 800,
                DeviceScaleFactor = NormalizeDpr((float)physicalWidth / 360f),
                DprX = (double)physicalWidth / 360.0,
                DprY = (double)physicalHeight / 800.0,
                Score = 9999,
                IsSuspiciousInput = suspiciousInput,
                IsFallback = true
            };
        }

        private static double GetWidthPenalty(int cssWidth)
        {
            return cssWidth switch
            {
                360 => 0.0000,
                393 => 0.0010,
                392 => 0.0012,
                390 => 0.0015,
                412 => 0.0020,
                411 => 0.0022,
                _ => 0.0050
            };
        }

        private static double GetDprPenalty(double dpr)
        {
            double nearestDiff = CommonDprs.Min(x => Math.Abs(x - dpr));
            return nearestDiff * 0.01;
        }

        private static double SnapToNearestCommonDpr(double dpr)
        {
            return CommonDprs
                .OrderBy(x => Math.Abs(x - dpr))
                .First();
        }

        private static float NormalizeDpr(float dpr)
        {
            foreach (var common in CommonDprs)
            {
                if (Math.Abs(dpr - common) <= 0.08f)
                    return common;
            }

            return (float)Math.Round(dpr, 3, MidpointRounding.AwayFromZero);
        }
    }
}