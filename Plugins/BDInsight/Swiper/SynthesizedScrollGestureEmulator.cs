using Microsoft.Playwright;
using SEM;

namespace BDInsight.Swiper
{
    /// <summary>
    /// 使用 Chrome DevTools Protocol 的 Input.synthesizeScrollGesture 实现滚动。
    /// 它由浏览器合成一个滚动手势，不逐点派发 touchStart/touchMove/touchEnd，
    /// 适合和 SwipeEmulator 的真实触屏事件实现做效果对比。
    /// </summary>
    public static class SynthesizedScrollGestureEmulator
    {
        public sealed class SynthesizedScrollTrace
        {
            public float X { get; set; }
            public float Y { get; set; }
            public double XDistance { get; set; }
            public double YDistance { get; set; }
            public int Speed { get; set; }
            public int RepeatCount { get; set; }
            public int RepeatDelayMs { get; set; }
            public PageScrollDirection Direction { get; set; }
            public int TotalDelayMs { get; set; }
            public bool ScrollChanged { get; set; }
        }

        private sealed class SynthesizedScrollProfile
        {
            public int DistancePx { get; set; }
            public int Speed { get; set; }
            public int RepeatCount { get; set; }
            public int RepeatDelayMs { get; set; }
            public int PauseMs { get; set; }
            public bool PreventFling { get; set; } = true;
        }

        public static async Task<List<SynthesizedScrollTrace>> PageScrollAsync(
            IPage page,
            ICDPSession client,
            int scrollCount,
            PageScrollDirection direction,
            Func<IPage, Task<bool>>? predexp = null,
            int timeDelay = 0,
            ScrollOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var traces = new List<SynthesizedScrollTrace>();

            if (page == null || page.IsClosed || client == null || scrollCount <= 0)
                return traces;

            options ??= new ScrollOptions();

            try
            {
                int noMoveCount = 0;
                await Task.Delay(CommonHelper.NextInt(140, 360), cancellationToken);

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

                    var actualDirection = PickDirection(direction);

                    if (actualDirection == PageScrollDirection.Down && options.EnableTopProtection)
                    {
                        bool nearTop = await HumanScrollHelper.IsNearTopAsync(page, options.NearTopThresholdPx);
                        if (nearTop)
                            break;
                    }

                    var profile = ResolveProfile(viewport.Height, actualDirection, i, noMoveCount, options);

                    var trace = await SynthesizeScrollOnceAsync(
                        page: page,
                        client: client,
                        direction: actualDirection,
                        distancePx: profile.DistancePx,
                        speed: profile.Speed,
                        repeatCount: profile.RepeatCount,
                        repeatDelayMs: profile.RepeatDelayMs,
                        preventFling: profile.PreventFling,
                        verifyScrollChanged: options.VerifyScrollChanged,
                        cancellationToken: cancellationToken);

                    if (trace != null)
                        traces.Add(trace);

                    int pause = timeDelay > 0 ? timeDelay : profile.PauseMs;
                    if (pause > 0)
                        await Task.Delay(pause, cancellationToken);

                    if (trace == null || (options.VerifyScrollChanged && !trace.ScrollChanged))
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
            }
            catch
            {
            }

            return traces;
        }

        public static async Task<SynthesizedScrollTrace?> SynthesizeScrollOnceAsync(
            IPage page,
            ICDPSession client,
            PageScrollDirection direction,
            int? distancePx = null,
            int? speed = null,
            int repeatCount = 0,
            int repeatDelayMs = 250,
            bool preventFling = true,
            bool verifyScrollChanged = true,
            CancellationToken cancellationToken = default)
        {
            if (page == null || page.IsClosed || client == null || page.ViewportSize == null)
                return null;

            try
            {
                var viewport = page.ViewportSize;
                int vw = viewport.Width;
                int vh = viewport.Height;

                var start = PickStartPoint(vw, vh);
                int actualDistance = distancePx ?? CommonHelper.NextInt((int)(vh * 0.24), (int)(vh * 0.52));
                actualDistance = ClampDistance(actualDistance, vh);

                PageScrollDirection actualDirection = PickDirection(direction);
                double yDistance = actualDirection == PageScrollDirection.Up
                    ? actualDistance
                    : -actualDistance;

                double xDistance = CommonHelper.Chance(0.32)
                    ? CommonHelper.NextDouble(-10, 10)
                    : 0;

                int actualSpeed = speed ?? GuessSpeed(actualDistance, vh);
                int actualRepeatDelay = Math.Clamp(repeatDelayMs, 60, 1200);
                int actualRepeatCount = Math.Clamp(repeatCount, 0, 3);

                double beforeY = verifyScrollChanged ? await HumanScrollHelper.GetPageScrollYSafeAsync(page) : 0;

                var parameters = new Dictionary<string, object>
                {
                    ["x"] = MathF.Round(start.x, 2),
                    ["y"] = MathF.Round(start.y, 2),
                    ["xDistance"] = Math.Round(xDistance, 2),
                    ["yDistance"] = Math.Round(yDistance, 2),
                    ["speed"] = actualSpeed,
                    ["gestureSourceType"] = "touch",
                    ["preventFling"] = preventFling,
                    ["repeatCount"] = actualRepeatCount,
                    ["repeatDelayMs"] = actualRepeatDelay,
                    ["interactionMarkerName"] = $"synth_scroll_{actualDirection.ToString().ToLowerInvariant()}"
                };

                await client.SendAsync("Input.synthesizeScrollGesture", parameters);

                int settleDelay = CommonHelper.NextInt(80, 180) + (actualRepeatCount * actualRepeatDelay);
                await Task.Delay(settleDelay, cancellationToken);

                bool changed = true;
                if (verifyScrollChanged)
                {
                    double afterY = await HumanScrollHelper.GetPageScrollYSafeAsync(page);
                    changed = Math.Abs(afterY - beforeY) >= 3;
                }

                return new SynthesizedScrollTrace
                {
                    X = start.x,
                    Y = start.y,
                    XDistance = xDistance,
                    YDistance = yDistance,
                    Speed = actualSpeed,
                    RepeatCount = actualRepeatCount,
                    RepeatDelayMs = actualRepeatDelay,
                    Direction = actualDirection,
                    TotalDelayMs = settleDelay,
                    ScrollChanged = changed
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private static SynthesizedScrollProfile ResolveProfile(
            int viewportHeight,
            PageScrollDirection direction,
            int index,
            int noMoveCount,
            ScrollOptions options)
        {
            int vh = Math.Max(viewportHeight, 320);

            int distance;
            if (options.DistancePx.HasValue || options.HeightRatio.HasValue)
            {
                distance = options.DistancePx
                    ?? (int)(vh * Math.Clamp(options.HeightRatio ?? 0.48, 0.04, 0.72));
            }
            else
            {
                HumanScrollMode mode = options.Mode == HumanScrollMode.Auto
                    ? PickAutoMode(direction, index, noMoveCount, options.EnableAutoMix)
                    : options.Mode;

                distance = mode switch
                {
                    HumanScrollMode.Long => CommonHelper.NextInt((int)(vh * 0.42), (int)(vh * 0.62)),
                    HumanScrollMode.Short => CommonHelper.NextInt((int)(vh * 0.20), (int)(vh * 0.34)),
                    HumanScrollMode.Probe => CommonHelper.NextInt((int)(vh * 0.12), (int)(vh * 0.22)),
                    HumanScrollMode.FineTune => CommonHelper.NextInt((int)(vh * 0.06), (int)(vh * 0.14)),
                    _ => CommonHelper.NextInt((int)(vh * 0.22), (int)(vh * 0.48))
                };

                if (direction == PageScrollDirection.Down)
                    distance = (int)(distance * CommonHelper.NextDouble(0.55, 0.78));
            }

            distance += noMoveCount * CommonHelper.NextInt(16, 34);
            distance = ClampDistance(distance, vh);

            int speed = GuessSpeed(distance, vh);
            int pause = options.PauseRangeMs is { } pr
                ? NextIntSafe(pr.Min, pr.Max)
                : GuessPause(distance, direction);

            return new SynthesizedScrollProfile
            {
                DistancePx = distance,
                Speed = speed,
                RepeatCount = CommonHelper.Chance(0.12) ? 1 : 0,
                RepeatDelayMs = CommonHelper.NextInt(180, 360),
                PauseMs = pause,
                PreventFling = !CommonHelper.Chance(0.08)
            };
        }

        private static HumanScrollMode PickAutoMode(
            PageScrollDirection direction,
            int index,
            int noMoveCount,
            bool enableAutoMix)
        {
            if (!enableAutoMix)
                return HumanScrollMode.Short;

            if (noMoveCount > 0)
                return HumanScrollMode.Probe;

            if (direction == PageScrollDirection.Down)
                return CommonHelper.Chance(0.70) ? HumanScrollMode.FineTune : HumanScrollMode.Short;

            if (index == 0 && CommonHelper.Chance(0.46))
                return HumanScrollMode.Long;

            double r = CommonHelper.NextDouble();
            if (r < 0.46) return HumanScrollMode.Short;
            if (r < 0.78) return HumanScrollMode.Long;
            if (r < 0.93) return HumanScrollMode.Probe;
            return HumanScrollMode.FineTune;
        }

        private static (float x, float y) PickStartPoint(int viewportWidth, int viewportHeight)
        {
            float x = (float)CommonHelper.NextDouble(viewportWidth * 0.34, viewportWidth * 0.66);
            float y = (float)CommonHelper.NextDouble(viewportHeight * 0.36, viewportHeight * 0.70);

            return (x, y);
        }

        private static int GuessSpeed(int distancePx, int viewportHeight)
        {
            if (distancePx >= viewportHeight * 0.48)
                return CommonHelper.NextInt(520, 820);

            if (distancePx >= viewportHeight * 0.22)
                return CommonHelper.NextInt(430, 700);

            return CommonHelper.NextInt(320, 560);
        }

        private static int GuessPause(int distancePx, PageScrollDirection direction)
        {
            if (direction == PageScrollDirection.Down)
                return CommonHelper.NextInt(360, 760);

            if (distancePx >= 420)
                return CommonHelper.NextInt(680, 1280);

            if (distancePx >= 220)
                return CommonHelper.NextInt(480, 980);

            return CommonHelper.NextInt(280, 660);
        }

        private static int ClampDistance(int distancePx, int viewportHeight)
        {
            int min = Math.Max(18, (int)(viewportHeight * 0.035));
            int max = Math.Max(min + 1, (int)(viewportHeight * 0.68));

            return Math.Clamp(distancePx, min, max);
        }

        private static PageScrollDirection PickDirection(PageScrollDirection direction)
        {
            if (direction != PageScrollDirection.Random)
                return direction;

            return CommonHelper.Chance(0.88)
                ? PageScrollDirection.Up
                : PageScrollDirection.Down;
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
}
