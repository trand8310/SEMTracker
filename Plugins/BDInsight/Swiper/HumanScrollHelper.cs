using Microsoft.Playwright;
using SEM;

namespace BDInsight.Swiper
{
    public enum HumanScrollMode
    {
        Auto = 0,
        Short = 1,
        Long = 2,
        Probe = 3,
        FineTune = 4
    }

    public static class HumanScrollHelper
    {
        private sealed class HumanScrollProfile
        {
            public HumanScrollMode Mode { get; set; }
            public int DistancePx { get; set; }
            public int? PointCount { get; set; }
            public int PauseMs { get; set; }
            public bool MicroSwipe { get; set; }
            public SwipeArea Area { get; set; } = SwipeArea.Normal;
        }

        /// <summary>
        /// 真人级页面滚动。
        /// 支持短滑 / 长滑 / 探测滑 / 微调滑 / 自动混合策略。
        /// 底层统一使用 SwipeEmulator，支持内部滚动容器。
        /// </summary>
        public static async Task TouchPageScrollAsync(
            IPage page,
            ICDPSession client,
            int scrollCount,
            PageScrollDirection direction,
            Func<IPage, Task<bool>>? predexp = null,
            int timeDelay = 0,
            ScrollOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (page == null || page.IsClosed || client == null || scrollCount <= 0)
                return;

            options ??= new ScrollOptions();

            try
            {
                int noMoveCount = 0;

                for (int i = 0; i < scrollCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (page.IsClosed)
                        break;

                    if (await ShouldStopByPredicateAsync(page, predexp))
                        break;

                    var viewport = page.ViewportSize;
                    if (viewport == null || viewport.Width <= 0 || viewport.Height <= 0)
                        break;

                    int vh = viewport.Height;

                    if (direction == PageScrollDirection.Down && options.EnableTopProtection)
                    {
                        bool nearTop = await IsNearTopAsync(page, options.NearTopThresholdPx);
                        if (nearTop)
                            break;
                    }

                    var profile = ResolveHumanScrollProfile(
                        viewportHeight: vh,
                        direction: direction,
                        index: i,
                        scrollCount: scrollCount,
                        noMoveCount: noMoveCount,
                        options: options);

                    ScrollDirection swipeDirection = ToSwipeDirection(direction);

                    var trace = await SwipeEmulator.SwipeOnceHumanAsync(
                        page: page,
                        client: client,
                        direction: swipeDirection,
                        area: profile.Area,
                        microSwipe: profile.MicroSwipe,
                        steps: profile.PointCount,
                        totalDistancePx: profile.DistancePx,
                        verifyScrollChanged: options.VerifyScrollChanged,
                        cancellationToken: cancellationToken);

                    int pause = timeDelay > 0 ? timeDelay : profile.PauseMs;
                    if (pause > 0)
                        await Task.Delay(pause, cancellationToken);

                    if (trace == null)
                    {
                        noMoveCount++;
                    }
                    else
                    {
                        noMoveCount = 0;
                    }

                    if (await ShouldStopByPredicateAsync(page, predexp))
                        break;

                    if (noMoveCount >= options.MaxConsecutiveNoMove)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // 保持和你原来的逻辑一致：外部取消时不继续抛出
            }
            catch
            {
                // 滚动辅助类不向外抛异常
            }
        }

        /// <summary>
        /// 单次真人级滚动。
        /// </summary>
        public static Task TouchPageScrollOnceAsync(
            IPage page,
            ICDPSession client,
            PageScrollDirection direction,
            Func<IPage, Task<bool>>? predexp = null,
            int timeDelay = 0,
            ScrollOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            return TouchPageScrollAsync(
                page: page,
                client: client,
                scrollCount: 1,
                direction: direction,
                predexp: predexp,
                timeDelay: timeDelay,
                options: options,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 长滑为主，适合快速浏览。
        /// </summary>
        public static Task TouchPageLongScrollAsync(
            IPage page,
            ICDPSession client,
            int scrollCount,
            PageScrollDirection direction,
            Func<IPage, Task<bool>>? predexp = null,
            int timeDelay = 0,
            CancellationToken cancellationToken = default)
        {
            return TouchPageScrollAsync(
                page: page,
                client: client,
                scrollCount: scrollCount,
                direction: direction,
                predexp: predexp,
                timeDelay: timeDelay,
                options: new ScrollOptions
                {
                    Mode = HumanScrollMode.Long
                },
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 短滑为主，适合普通浏览。
        /// </summary>
        public static Task TouchPageShortScrollAsync(
            IPage page,
            ICDPSession client,
            int scrollCount,
            PageScrollDirection direction,
            Func<IPage, Task<bool>>? predexp = null,
            int timeDelay = 0,
            CancellationToken cancellationToken = default)
        {
            return TouchPageScrollAsync(
                page: page,
                client: client,
                scrollCount: scrollCount,
                direction: direction,
                predexp: predexp,
                timeDelay: timeDelay,
                options: new ScrollOptions
                {
                    Mode = HumanScrollMode.Short
                },
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 微调滑动，适合轻微修正位置。
        /// </summary>
        public static Task TouchPageFineTuneScrollAsync(
            IPage page,
            ICDPSession client,
            int scrollCount,
            PageScrollDirection direction,
            Func<IPage, Task<bool>>? predexp = null,
            int timeDelay = 0,
            CancellationToken cancellationToken = default)
        {
            return TouchPageScrollAsync(
                page: page,
                client: client,
                scrollCount: scrollCount,
                direction: direction,
                predexp: predexp,
                timeDelay: timeDelay,
                options: new ScrollOptions
                {
                    Mode = HumanScrollMode.FineTune
                },
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 探测滑，适合试探性找元素或判断页面是否还有内容。
        /// </summary>
        public static Task TouchPageProbeScrollAsync(
            IPage page,
            ICDPSession client,
            int scrollCount,
            PageScrollDirection direction,
            Func<IPage, Task<bool>>? predexp = null,
            int timeDelay = 0,
            CancellationToken cancellationToken = default)
        {
            return TouchPageScrollAsync(
                page: page,
                client: client,
                scrollCount: scrollCount,
                direction: direction,
                predexp: predexp,
                timeDelay: timeDelay,
                options: new ScrollOptions
                {
                    Mode = HumanScrollMode.Probe
                },
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 按屏幕高度比例滚动。
        /// </summary>
        public static Task TouchPageScrollByRatioAsync(
            IPage page,
            ICDPSession client,
            int scrollCount,
            PageScrollDirection direction,
            double heightRatio,
            Func<IPage, Task<bool>>? predexp = null,
            int timeDelay = 0,
            CancellationToken cancellationToken = default)
        {
            return TouchPageScrollAsync(
                page: page,
                client: client,
                scrollCount: scrollCount,
                direction: direction,
                predexp: predexp,
                timeDelay: timeDelay,
                options: new ScrollOptions
                {
                    HeightRatio = heightRatio
                },
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 按固定像素滚动。
        /// </summary>
        public static Task TouchPageScrollByDistanceAsync(
            IPage page,
            ICDPSession client,
            int scrollCount,
            PageScrollDirection direction,
            int distancePx,
            Func<IPage, Task<bool>>? predexp = null,
            int timeDelay = 0,
            CancellationToken cancellationToken = default)
        {
            return TouchPageScrollAsync(
                page: page,
                client: client,
                scrollCount: scrollCount,
                direction: direction,
                predexp: predexp,
                timeDelay: timeDelay,
                options: new ScrollOptions
                {
                    DistancePx = distancePx
                },
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 滑动到元素舒适区。
        /// 这个方法直接使用 SwipeEmulator 的元素定位滑动能力。
        /// </summary>
        public static Task<List<SwipeTrace>> TouchScrollToElementAsync(
            IPage page,
            ICDPSession client,
            ILocator element,
            int maxSwipes = 10,
            CancellationToken cancellationToken = default)
        {
            return SwipeEmulator.SwipeToElementAsync(
                page: page,
                client: client,
                element: element,
                maxSwipes: maxSwipes,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// 滑动到元素舒适操作区。
        /// </summary>
        public static Task<List<SwipeTrace>> TouchElementIntoComfortZoneAsync(
            IPage page,
            ICDPSession client,
            ILocator element,
            int maxSwipes = 8,
            float comfortTopRatio = 0.22f,
            float comfortBottomRatio = 0.72f,
            CancellationToken cancellationToken = default)
        {
            return SwipeEmulator.SwipeElementIntoComfortZoneAsync(
                page: page,
                client: client,
                element: element,
                maxSwipes: maxSwipes,
                comfortTopRatio: comfortTopRatio,
                comfortBottomRatio: comfortBottomRatio,
                cancellationToken: cancellationToken);
        }

        private static HumanScrollProfile ResolveHumanScrollProfile(
            int viewportHeight,
            PageScrollDirection direction,
            int index,
            int scrollCount,
            int noMoveCount,
            ScrollOptions options)
        {
            int vh = Math.Max(viewportHeight, 320);

            if (options.DistancePx.HasValue || options.HeightRatio.HasValue)
            {
                int distancePx = options.DistancePx
                    ?? (int)(vh * Math.Clamp(options.HeightRatio ?? 0.60, 0.04, 0.85));

                distancePx = ClampDistance(distancePx, vh);

                int? pointCount = null;
                if (options.PointCountRange is { } pcr)
                {
                    pointCount = NextIntSafe(pcr.Min, pcr.Max);
                }
                else if (options.EnableCustomPointCount)
                {
                    pointCount = GuessPointCount(vh, distancePx, direction);
                }

                int pauseMs = options.PauseRangeMs is { } pr
                    ? NextIntSafe(pr.Min, pr.Max)
                    : GuessPauseMs(direction, distancePx);

                bool micro = distancePx <= vh * 0.18;

                return new HumanScrollProfile
                {
                    Mode = options.Mode,
                    DistancePx = distancePx,
                    PointCount = pointCount,
                    PauseMs = pauseMs,
                    MicroSwipe = micro,
                    Area = micro ? SwipeArea.Micro : SwipeArea.Normal
                };
            }

            HumanScrollMode mode = options.Mode;

            if (mode == HumanScrollMode.Auto)
            {
                mode = DecideAutoMode(
                    direction: direction,
                    index: index,
                    scrollCount: scrollCount,
                    noMoveCount: noMoveCount,
                    enableAutoMix: options.EnableAutoMix);
            }

            return BuildProfileByMode(
                viewportHeight: vh,
                direction: direction,
                mode: mode,
                noMoveCount: noMoveCount,
                options: options);
        }

        private static HumanScrollMode DecideAutoMode(
            PageScrollDirection direction,
            int index,
            int scrollCount,
            int noMoveCount,
            bool enableAutoMix)
        {
            if (!enableAutoMix)
            {
                return direction == PageScrollDirection.Up
                    ? HumanScrollMode.Long
                    : HumanScrollMode.Short;
            }

            if (noMoveCount >= 1)
                return HumanScrollMode.Probe;

            if (direction == PageScrollDirection.Down)
            {
                int r = CommonHelper.NextInt(0, 100);

                if (r < 65) return HumanScrollMode.FineTune;
                if (r < 90) return HumanScrollMode.Short;
                return HumanScrollMode.Probe;
            }

            if (index == 0)
            {
                int r = CommonHelper.NextInt(0, 100);

                if (r < 55) return HumanScrollMode.Long;
                if (r < 85) return HumanScrollMode.Short;
                return HumanScrollMode.Probe;
            }

            int r2 = CommonHelper.NextInt(0, 100);

            if (r2 < 55) return HumanScrollMode.Long;
            if (r2 < 78) return HumanScrollMode.Short;
            if (r2 < 92) return HumanScrollMode.Probe;
            return HumanScrollMode.FineTune;
        }

        private static HumanScrollProfile BuildProfileByMode(
            int viewportHeight,
            PageScrollDirection direction,
            HumanScrollMode mode,
            int noMoveCount,
            ScrollOptions options)
        {
            int vh = Math.Max(viewportHeight, 320);

            int distancePx;
            int? pointCount;
            int pauseMs;
            bool micro;
            SwipeArea area;

            switch (mode)
            {
                case HumanScrollMode.Short:
                    distancePx = direction == PageScrollDirection.Up
                        ? NextIntSafe((int)(vh * 0.22), (int)(vh * 0.36))
                        : NextIntSafe((int)(vh * 0.10), (int)(vh * 0.18));

                    micro = false;
                    area = SwipeArea.Normal;
                    break;

                case HumanScrollMode.Long:
                    distancePx = direction == PageScrollDirection.Up
                        ? NextIntSafe((int)(vh * 0.50), (int)(vh * 0.66))
                        : NextIntSafe((int)(vh * 0.14), (int)(vh * 0.24));

                    micro = false;
                    area = SwipeArea.Normal;
                    break;

                case HumanScrollMode.Probe:
                    distancePx = direction == PageScrollDirection.Up
                        ? NextIntSafe((int)(vh * 0.14), (int)(vh * 0.24))
                        : NextIntSafe((int)(vh * 0.08), (int)(vh * 0.14));

                    distancePx += noMoveCount * 18;
                    micro = distancePx <= vh * 0.18;
                    area = micro ? SwipeArea.Micro : SwipeArea.Normal;
                    break;

                case HumanScrollMode.FineTune:
                    distancePx = direction == PageScrollDirection.Up
                        ? NextIntSafe((int)(vh * 0.08), (int)(vh * 0.16))
                        : NextIntSafe((int)(vh * 0.06), (int)(vh * 0.12));

                    micro = true;
                    area = SwipeArea.Micro;
                    break;

                case HumanScrollMode.Auto:
                default:
                    distancePx = direction == PageScrollDirection.Up
                        ? NextIntSafe((int)(vh * 0.34), (int)(vh * 0.58))
                        : NextIntSafe((int)(vh * 0.10), (int)(vh * 0.20));

                    micro = distancePx <= vh * 0.18;
                    area = micro ? SwipeArea.Micro : SwipeArea.Normal;
                    break;
            }

            distancePx = ClampDistance(distancePx, vh);

            if (options.PointCountRange is { } pcr)
            {
                pointCount = NextIntSafe(pcr.Min, pcr.Max);
            }
            else if (options.EnableCustomPointCount)
            {
                pointCount = GuessPointCount(vh, distancePx, direction);
            }
            else
            {
                pointCount = null;
            }

            if (options.PauseRangeMs is { } pr)
            {
                pauseMs = NextIntSafe(pr.Min, pr.Max);
            }
            else
            {
                pauseMs = GuessPauseMs(direction, distancePx);
            }

            return new HumanScrollProfile
            {
                Mode = mode,
                DistancePx = distancePx,
                PointCount = pointCount,
                PauseMs = pauseMs,
                MicroSwipe = micro,
                Area = area
            };
        }

        private static int GuessPointCount(
            int viewportHeight,
            int distancePx,
            PageScrollDirection direction)
        {
            if (direction == PageScrollDirection.Down)
                return NextIntSafe(8, 12);

            if (distancePx >= viewportHeight * 0.50)
                return NextIntSafe(18, 28);

            if (distancePx >= viewportHeight * 0.30)
                return NextIntSafe(14, 22);

            if (distancePx >= viewportHeight * 0.15)
                return NextIntSafe(10, 16);

            return NextIntSafe(8, 12);
        }

        private static int GuessPauseMs(
            PageScrollDirection direction,
            int distancePx)
        {
            if (direction == PageScrollDirection.Down)
                return CommonHelper.NextInt(280, 580);

            if (distancePx >= 420)
                return CommonHelper.NextInt(580, 1100);

            if (distancePx >= 260)
                return CommonHelper.NextInt(420, 850);

            return CommonHelper.NextInt(220, 520);
        }

        private static int ClampDistance(int distancePx, int viewportHeight)
        {
            int min = Math.Max(20, (int)(viewportHeight * 0.04));
            int max = Math.Max(min + 1, (int)(viewportHeight * 0.72));

            return Math.Clamp(distancePx, min, max);
        }

        private static ScrollDirection ToSwipeDirection(PageScrollDirection direction)
        {
            return direction switch
            {
                PageScrollDirection.Down => ScrollDirection.Down,
                PageScrollDirection.Up => ScrollDirection.Up,
                _ => ScrollDirection.Up
            };
        }

        private static async Task<bool> ShouldStopByPredicateAsync(
            IPage page,
            Func<IPage, Task<bool>>? predexp)
        {
            if (predexp == null)
                return false;

            if (page == null || page.IsClosed)
                return true;

            try
            {
                return await predexp(page);
            }
            catch
            {
                return false;
            }
        }

        public static async Task<double> GetPageScrollYSafeAsync(IPage page)
        {
            if (page == null || page.IsClosed)
                return 0;

            try
            {
                return await page.EvaluateAsync<double>(
                    @"() => {
                        try {
                            const se = document.scrollingElement || document.documentElement || document.body;
                            return Number(window.scrollY || se.scrollTop || 0);
                        } catch {
                            return 0;
                        }
                    }");
            }
            catch
            {
                return 0;
            }
        }

        public static async Task<bool> IsNearTopAsync(
            IPage page,
            int thresholdPx = 10)
        {
            if (page == null || page.IsClosed)
                return false;

            try
            {
                double y = await GetPageScrollYSafeAsync(page);
                return y <= thresholdPx;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> IsNearBottomAsync(
            IPage page,
            int thresholdPx = 10)
        {
            if (page == null || page.IsClosed)
                return false;

            try
            {
                return await page.EvaluateAsync<bool>(
                    @"(threshold) => {
                        try {
                            const se = document.scrollingElement || document.documentElement || document.body;
                            const y = Number(window.scrollY || se.scrollTop || 0);
                            const ch = Number(window.innerHeight || se.clientHeight || 0);
                            const sh = Number(se.scrollHeight || 0);
                            return y + ch >= sh - Number(threshold || 10);
                        } catch {
                            return false;
                        }
                    }",
                    thresholdPx);
            }
            catch
            {
                return false;
            }
        }

        private static int NextIntSafe(int min, int max)
        {
            if (min == max)
                return min;

            if (min > max)
            {
                int t = min;
                min = max;
                max = t;
            }

            return CommonHelper.NextInt(min, max);
        }
    }



    public sealed class IntRange
    {
        public int Min { get; set; }
        public int Max { get; set; }

        public IntRange()
        {
        }

        public IntRange(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }

    public sealed class FloatRange
    {
        public double Min { get; set; }
        public double Max { get; set; }

        public FloatRange()
        {
        }

        public FloatRange(double min, double max)
        {
            Min = min;
            Max = max;
        }
    }
}