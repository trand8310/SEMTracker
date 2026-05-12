using BDInsight.Swiper;
using Microsoft.Playwright;
using SEM;
using System.Numerics;

namespace BDInsight.Emulator
{
    /// <summary>
    /// 面向页面交互测试的触屏手势模拟器。
    /// 该类通过 CDP Input.dispatchTouchEvent 逐点派发 touchStart/touchMove/touchEnd，
    /// 重点提供更平滑、更接近真实手指操作节奏的滑动轨迹。
    /// </summary>
    public static class HumanTouchEmulator
    {
        public sealed class HumanTouchSwipeOptions
        {
            public int? DistancePx { get; set; }
            public int? StepCount { get; set; }
            public SwipeArea? Area { get; set; }
            public bool MicroSwipe { get; set; }
            public bool VerifyScrollChanged { get; set; } = true;
            public int SettleDelayMinMs { get; set; } = 90;
            public int SettleDelayMaxMs { get; set; } = 220;
            public bool AllowTinyCorrection { get; set; } = true;
            public bool PreferScrollableStartPoint { get; set; } = true;
        }

        public sealed class HumanTouchScrollOptions : HumanTouchSwipeOptions
        {
            public int MaxConsecutiveNoMove { get; set; } = 3;
            public int? PauseMinMs { get; set; }
            public int? PauseMaxMs { get; set; }
        }

        public sealed class HumanTouchTrace
        {
            public Vector2 Start { get; set; }
            public Vector2 End { get; set; }
            public List<Vector2> Points { get; set; } = new();
            public PageScrollDirection Direction { get; set; }
            public int DistancePx { get; set; }
            public int TotalDelayMs { get; set; }
            public bool ScrollChanged { get; set; }
            public bool IsMicroSwipe { get; set; }
        }

        private sealed class ScrollSnapshot
        {
            public double ScrollY { get; set; }
            public double ScrollHeight { get; set; }
            public double ClientHeight { get; set; }
        }

        public static async Task<List<HumanTouchTrace>> PageScrollAsync(
            IPage page,
            ICDPSession client,
            int scrollCount,
            PageScrollDirection direction,
            Func<IPage, Task<bool>>? shouldStop = null,
            HumanTouchScrollOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var traces = new List<HumanTouchTrace>();

            if (page == null || page.IsClosed || client == null || scrollCount <= 0)
                return traces;

            options ??= new HumanTouchScrollOptions();

            try
            {
                int noMoveCount = 0;
                await EnableTouchInputAsync(page, client);
                await Task.Delay(CommonHelper.NextInt(180, 460), cancellationToken);

                for (int i = 0; i < scrollCount; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (page.IsClosed)
                        break;

                    if (await ShouldStopAsync(page, shouldStop))
                        break;

                    PageScrollDirection actualDirection = PickDirection(direction);
                    var swipeOptions = BuildSwipeOptions(page, actualDirection, i, noMoveCount, options);

                    await Task.Delay(GuessBeforeGestureDelay(i, swipeOptions), cancellationToken);

                    var trace = await SwipeAsync(
                        page: page,
                        client: client,
                        direction: actualDirection,
                        options: swipeOptions,
                        cancellationToken: cancellationToken);

                    if (trace != null)
                        traces.Add(trace);

                    if (trace == null || (swipeOptions.VerifyScrollChanged && !trace.ScrollChanged))
                    {
                        noMoveCount++;
                    }
                    else
                    {
                        noMoveCount = 0;
                    }

                    if (await ShouldStopAsync(page, shouldStop))
                        break;

                    if (noMoveCount >= options.MaxConsecutiveNoMove)
                        break;

                    int pause = ResolvePause(options, trace?.DistancePx ?? swipeOptions.DistancePx ?? 0, actualDirection);
                    if (pause > 0)
                        await Task.Delay(pause, cancellationToken);
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

        public static async Task<HumanTouchTrace?> SwipeAsync(
            IPage page,
            ICDPSession client,
            PageScrollDirection direction,
            HumanTouchSwipeOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            if (page == null || page.IsClosed || client == null || page.ViewportSize == null)
                return null;

            options ??= new HumanTouchSwipeOptions();

            try
            {
                await EnableTouchInputAsync(page, client);

                var viewport = page.ViewportSize;
                int vw = viewport.Width;
                int vh = viewport.Height;
                PageScrollDirection actualDirection = PickDirection(direction);
                bool microSwipe = options.MicroSwipe;

                SwipeArea area = options.Area ?? (microSwipe ? SwipeArea.Micro : SwipeArea.Normal);
                int distance = ResolveSwipeDistance(vh, actualDirection, options.DistancePx, microSwipe);
                int steps = ResolveStepCount(vh, distance, options.StepCount, microSwipe);

                var path = await CreateTouchPathAsync(
                    page: page,
                    viewportWidth: vw,
                    viewportHeight: vh,
                    direction: actualDirection,
                    area: area,
                    distancePx: distance,
                    microSwipe: microSwipe,
                    preferScrollableStartPoint: options.PreferScrollableStartPoint);

                if (path == null)
                    return null;

                var before = options.VerifyScrollChanged
                    ? await GetScrollSnapshotAsync(page)
                    : null;

                List<Vector2> points = BuildOrganicPoints(path.Value.start, path.Value.end, steps, microSwipe, options.AllowTinyCorrection);
                var trace = await DispatchTouchPathAsync(
                    client: client,
                    points: points,
                    direction: actualDirection,
                    distancePx: distance,
                    microSwipe: microSwipe,
                    cancellationToken: cancellationToken);

                int settleDelay = CommonHelper.NextInt(
                    Math.Min(options.SettleDelayMinMs, options.SettleDelayMaxMs),
                    Math.Max(options.SettleDelayMinMs, options.SettleDelayMaxMs) + 1);

                await Task.Delay(settleDelay, cancellationToken);
                trace.TotalDelayMs += settleDelay;

                if (before != null)
                {
                    var after = await GetScrollSnapshotAsync(page);
                    trace.ScrollChanged = Math.Abs(after.ScrollY - before.ScrollY) >= (microSwipe ? 3 : 7);
                }
                else
                {
                    trace.ScrollChanged = true;
                }

                return trace;
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

        private static async Task EnableTouchInputAsync(IPage page, ICDPSession client)
        {
            try
            {
                await page.BringToFrontAsync();
            }
            catch
            {
            }

            try
            {
                await client.SendAsync("Input.setIgnoreInputEvents", new Dictionary<string, object>
                {
                    ["ignore"] = false
                });
            }
            catch
            {
            }

            try
            {
                await client.SendAsync("Emulation.setTouchEmulationEnabled", new Dictionary<string, object>
                {
                    ["enabled"] = true,
                    ["maxTouchPoints"] = 5
                });
            }
            catch
            {
            }
        }

        private static HumanTouchSwipeOptions BuildSwipeOptions(
            IPage page,
            PageScrollDirection direction,
            int index,
            int noMoveCount,
            HumanTouchScrollOptions source)
        {
            int vh = Math.Max(page.ViewportSize?.Height ?? 640, 320);
            bool micro = source.MicroSwipe;
            int? distance = source.DistancePx;

            if (!distance.HasValue)
            {
                double ratio;
                if (micro)
                {
                    ratio = CommonHelper.NextDouble(0.055, 0.14);
                }
                else if (direction == PageScrollDirection.Down)
                {
                    ratio = CommonHelper.NextDouble(0.08, 0.20);
                }
                else if (index == 0)
                {
                    ratio = CommonHelper.NextDouble(0.28, 0.54);
                }
                else
                {
                    double roll = CommonHelper.NextDouble();
                    ratio = roll < 0.55
                        ? CommonHelper.NextDouble(0.20, 0.38)
                        : roll < 0.88
                            ? CommonHelper.NextDouble(0.38, 0.58)
                            : CommonHelper.NextDouble(0.10, 0.20);
                }

                distance = (int)(vh * ratio) + noMoveCount * CommonHelper.NextInt(12, 34);
            }

            int normalizedDistance = ResolveSwipeDistance(vh, direction, distance, micro);

            return new HumanTouchSwipeOptions
            {
                DistancePx = normalizedDistance,
                StepCount = source.StepCount,
                Area = source.Area,
                MicroSwipe = micro || normalizedDistance <= vh * 0.16,
                VerifyScrollChanged = source.VerifyScrollChanged,
                SettleDelayMinMs = source.SettleDelayMinMs,
                SettleDelayMaxMs = source.SettleDelayMaxMs,
                AllowTinyCorrection = source.AllowTinyCorrection,
                PreferScrollableStartPoint = source.PreferScrollableStartPoint
            };
        }

        private static async Task<(Vector2 start, Vector2 end)?> CreateTouchPathAsync(
            IPage page,
            int viewportWidth,
            int viewportHeight,
            PageScrollDirection direction,
            SwipeArea area,
            int distancePx,
            bool microSwipe,
            bool preferScrollableStartPoint)
        {
            float safeLeft = Math.Max(viewportWidth * 0.10f, viewportWidth * area.MinXRatio);
            float safeRight = Math.Min(viewportWidth * 0.90f, viewportWidth * area.MaxXRatio);
            float safeTop = Math.Max(viewportHeight * 0.12f, viewportHeight * area.MinYRatio);
            float safeBottom = Math.Min(viewportHeight * 0.86f, viewportHeight * area.MaxYRatio);

            if (safeRight <= safeLeft || safeBottom <= safeTop)
                return null;

            for (int i = 0; i < 10; i++)
            {
                float startX = (float)CommonHelper.NextDouble(safeLeft, safeRight);
                float endX = startX + (float)CommonHelper.NextDouble(microSwipe ? -8 : -20, microSwipe ? 8 : 20);
                endX = Math.Clamp(endX, safeLeft, safeRight);

                float startY;
                float endY;

                if (direction == PageScrollDirection.Down)
                {
                    startY = (float)CommonHelper.NextDouble(viewportHeight * 0.28f, viewportHeight * 0.48f);
                    startY = Math.Clamp(startY, safeTop, safeBottom);
                    endY = startY + distancePx;

                    if (endY > safeBottom)
                    {
                        endY = safeBottom;
                        startY = Math.Clamp(endY - distancePx, safeTop, safeBottom);
                    }
                }
                else
                {
                    startY = (float)CommonHelper.NextDouble(viewportHeight * 0.56f, viewportHeight * 0.80f);
                    startY = Math.Clamp(startY, safeTop, safeBottom);
                    endY = startY - distancePx;

                    if (endY < safeTop)
                    {
                        endY = safeTop;
                        startY = Math.Clamp(endY + distancePx, safeTop, safeBottom);
                    }
                }

                var start = new Vector2(startX, startY);
                var end = new Vector2(endX, endY);

                if (!preferScrollableStartPoint || await IsUsableTouchPointAsync(page, start.X, start.Y))
                    return (start, end);
            }

            return null;
        }

        private static List<Vector2> BuildOrganicPoints(
            Vector2 start,
            Vector2 end,
            int steps,
            bool microSwipe,
            bool allowTinyCorrection)
        {
            steps = Math.Clamp(steps, microSwipe ? 14 : 28, microSwipe ? 42 : 92);
            var points = new List<Vector2>(steps + 1);

            Vector2 delta = end - start;
            float distance = delta.Length();
            if (distance < 1)
            {
                points.Add(start);
                points.Add(end);
                return points;
            }

            Vector2 normal = new(-delta.Y / distance, delta.X / distance);
            float driftA = (float)CommonHelper.NextDouble(microSwipe ? 0.8 : 2.5, microSwipe ? 2.6 : 7.0);
            float driftB = (float)CommonHelper.NextDouble(0.2, microSwipe ? 1.2 : 3.2);
            float phaseA = (float)CommonHelper.NextDouble(0, Math.PI * 2);
            float phaseB = (float)CommonHelper.NextDouble(0, Math.PI * 2);
            float correctionRatio = allowTinyCorrection && !microSwipe && CommonHelper.Chance(0.22)
                ? (float)CommonHelper.NextDouble(0.002, 0.008)
                : 0;

            for (int i = 0; i <= steps; i++)
            {
                float raw = i / (float)steps;
                float eased = EaseInOutQuint(raw);
                Vector2 point = start + delta * eased;

                float fade = MathF.Sin(raw * MathF.PI);
                float drift = (
                    MathF.Sin(raw * MathF.PI * 0.85f + phaseA) * driftA +
                    MathF.Sin(raw * MathF.PI * 2.15f + phaseB) * driftB) * fade;

                point += normal * drift;

                if (raw > 0.72f)
                {
                    float settle = SmoothStep((raw - 0.72f) / 0.28f);
                    float tremor = (float)CommonHelper.NextDouble(microSwipe ? 0.12 : 0.28, microSwipe ? 0.42 : 0.82);
                    point.X += MathF.Sin(raw * MathF.PI * 6.2f + phaseB) * tremor * settle;
                    point.Y += MathF.Sin(raw * MathF.PI * 4.8f + phaseA) * tremor * 0.55f * settle;
                }

                if (correctionRatio > 0 && raw > 0.90f)
                {
                    float correction = correctionRatio * SmoothStep((raw - 0.90f) / 0.10f);
                    point -= delta * correction;
                }

                points.Add(point);
            }

            return points;
        }

        private static async Task<HumanTouchTrace> DispatchTouchPathAsync(
            ICDPSession client,
            List<Vector2> points,
            PageScrollDirection direction,
            int distancePx,
            bool microSwipe,
            CancellationToken cancellationToken)
        {
            int totalDelay = 0;
            bool touchStarted = false;

            try
            {
                int radius = microSwipe ? CommonHelper.NextInt(2, 4) : CommonHelper.NextInt(3, 7);
                double force = microSwipe ? CommonHelper.NextDouble(0.66, 0.88) : CommonHelper.NextDouble(0.72, 0.96);

                await client.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
                {
                    ["type"] = "touchStart",
                    ["touchPoints"] = new object[]
                    {
                        CreateTouchPoint(points[0], radius, force)
                    },
                    ["modifiers"] = 0
                });

                touchStarted = true;

                int hold = microSwipe ? CommonHelper.NextInt(28, 90) : CommonHelper.NextInt(55, 180);
                await Task.Delay(hold, cancellationToken);
                totalDelay += hold;

                for (int i = 1; i < points.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    float progress = i / (float)(points.Count - 1);
                    int delay = GetMoveDelay(progress, microSwipe);
                    if (CommonHelper.Chance(microSwipe ? 0.05 : 0.08))
                        delay += CommonHelper.NextInt(8, 28);

                    int moveRadius = microSwipe ? CommonHelper.NextInt(2, 4) : CommonHelper.NextInt(3, 6);
                    double moveForce = GetForce(progress, microSwipe);

                    await client.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
                    {
                        ["type"] = "touchMove",
                        ["touchPoints"] = new object[]
                        {
                            CreateTouchPoint(points[i], moveRadius, moveForce)
                        },
                        ["modifiers"] = 0
                    });

                    await Task.Delay(delay, cancellationToken);
                    totalDelay += delay;
                }

                int releaseHold = microSwipe ? CommonHelper.NextInt(18, 65) : CommonHelper.NextInt(35, 130);
                await Task.Delay(releaseHold, cancellationToken);
                totalDelay += releaseHold;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            finally
            {
                if (touchStarted)
                {
                    try
                    {
                        await client.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
                        {
                            ["type"] = "touchEnd",
                            ["touchPoints"] = Array.Empty<object>(),
                            ["modifiers"] = 0
                        });
                    }
                    catch
                    {
                    }
                }
            }

            return new HumanTouchTrace
            {
                Start = points[0],
                End = points[^1],
                Points = points,
                Direction = direction,
                DistancePx = distancePx,
                TotalDelayMs = totalDelay,
                IsMicroSwipe = microSwipe
            };
        }

        private static object CreateTouchPoint(Vector2 point, int radius, double force)
        {
            return new
            {
                x = MathF.Round(point.X, 2),
                y = MathF.Round(point.Y, 2),
                radiusX = radius,
                radiusY = radius,
                force = Math.Clamp(force, 0.35, 1.0),
                id = 0
            };
        }

        private static async Task<ScrollSnapshot> GetScrollSnapshotAsync(IPage page)
        {
            try
            {
                var result = await page.EvaluateAsync<ScrollSnapshot>(@"
                () => {
                    try {
                        const el = document.scrollingElement || document.documentElement || document.body;
                        return {
                            ScrollY: Number(window.scrollY || el?.scrollTop || 0),
                            ScrollHeight: Number(el?.scrollHeight || 0),
                            ClientHeight: Number(window.innerHeight || el?.clientHeight || 0)
                        };
                    } catch {
                        return { ScrollY: 0, ScrollHeight: 0, ClientHeight: 0 };
                    }
                }");

                return result ?? new ScrollSnapshot();
            }
            catch
            {
                return new ScrollSnapshot();
            }
        }

        private static async Task<bool> IsUsableTouchPointAsync(IPage page, float x, float y)
        {
            if (page == null || page.IsClosed)
                return false;

            try
            {
                return await page.EvaluateAsync<bool>(@"
                (arg) => {
                    const x = Number(arg.x || 0);
                    const y = Number(arg.y || 0);
                    const el = document.elementFromPoint(x, y);
                    if (!el) return false;

                    let p = el;
                    let depth = 0;
                    while (p && depth < 5) {
                        const tag = String(p.tagName || '').toLowerCase();
                        if (tag === 'input' || tag === 'textarea' || tag === 'select') return false;
                        if (tag === 'iframe' || tag === 'video' || tag === 'canvas') return false;

                        const style = getComputedStyle(p);
                        if (!style || style.display === 'none' || style.visibility === 'hidden') return false;
                        if (Number(style.opacity) < 0.05) return false;

                        p = p.parentElement;
                        depth++;
                    }

                    return true;
                }",
                new { x, y });
            }
            catch
            {
                return false;
            }
        }

        private static int ResolveSwipeDistance(
            int viewportHeight,
            PageScrollDirection direction,
            int? requestedDistance,
            bool microSwipe)
        {
            int vh = Math.Max(viewportHeight, 320);
            int distance = requestedDistance ?? (microSwipe
                ? CommonHelper.NextInt((int)(vh * 0.07), (int)(vh * 0.16))
                : direction == PageScrollDirection.Down
                    ? CommonHelper.NextInt((int)(vh * 0.08), (int)(vh * 0.22))
                    : CommonHelper.NextInt((int)(vh * 0.22), (int)(vh * 0.56)));

            int min = Math.Max(18, (int)(vh * (microSwipe ? 0.035 : 0.06)));
            int max = Math.Max(min + 1, (int)(vh * (microSwipe ? 0.24 : 0.68)));
            return Math.Clamp(distance, min, max);
        }

        private static int ResolveStepCount(
            int viewportHeight,
            int distancePx,
            int? requestedStepCount,
            bool microSwipe)
        {
            if (requestedStepCount.HasValue && requestedStepCount.Value > 0)
                return requestedStepCount.Value;

            int min = microSwipe ? 16 : 34;
            int max = microSwipe ? 42 : 88;
            double ratio = Math.Min(distancePx / (viewportHeight * 0.68), 1.0);
            int steps = (int)(min + (max - min) * ratio) + CommonHelper.NextInt(-3, 4);

            return Math.Clamp(steps, min, max);
        }

        private static int GuessBeforeGestureDelay(int index, HumanTouchSwipeOptions options)
        {
            int delay = index == 0
                ? CommonHelper.NextInt(180, 520)
                : CommonHelper.NextInt(80, 260);

            if (options.MicroSwipe)
                delay += CommonHelper.NextInt(90, 260);

            if (CommonHelper.Chance(0.16))
                delay += CommonHelper.NextInt(260, 900);

            return delay;
        }

        private static int ResolvePause(HumanTouchScrollOptions options, int distancePx, PageScrollDirection direction)
        {
            if (options.PauseMinMs.HasValue || options.PauseMaxMs.HasValue)
            {
                int min = options.PauseMinMs ?? 0;
                int max = options.PauseMaxMs ?? min + 1;
                return NextIntSafe(min, max + 1);
            }

            if (direction == PageScrollDirection.Down)
                return CommonHelper.NextInt(420, 900);

            if (distancePx >= 420)
                return CommonHelper.NextInt(760, 1680);

            if (distancePx >= 220)
                return CommonHelper.NextInt(520, 1180);

            return CommonHelper.NextInt(320, 780);
        }

        private static int GetMoveDelay(float progress, bool microSwipe)
        {
            if (progress < 0.08f)
                return microSwipe ? CommonHelper.NextInt(14, 28) : CommonHelper.NextInt(20, 42);

            if (progress < 0.22f)
                return microSwipe ? CommonHelper.NextInt(9, 19) : CommonHelper.NextInt(13, 28);

            if (progress < 0.72f)
                return microSwipe ? CommonHelper.NextInt(6, 14) : CommonHelper.NextInt(8, 18);

            if (progress < 0.92f)
                return microSwipe ? CommonHelper.NextInt(10, 22) : CommonHelper.NextInt(14, 32);

            return microSwipe ? CommonHelper.NextInt(15, 32) : CommonHelper.NextInt(22, 46);
        }

        private static double GetForce(float progress, bool microSwipe)
        {
            double force;

            if (progress < 0.12f)
            {
                force = microSwipe
                    ? CommonHelper.NextDouble(0.58, 0.80)
                    : CommonHelper.NextDouble(0.64, 0.88);
            }
            else if (progress < 0.78f)
            {
                force = microSwipe
                    ? CommonHelper.NextDouble(0.68, 0.92)
                    : CommonHelper.NextDouble(0.74, 0.99);
            }
            else
            {
                force = microSwipe
                    ? CommonHelper.NextDouble(0.52, 0.76)
                    : CommonHelper.NextDouble(0.58, 0.84);
            }

            return Math.Clamp(force, 0.35, 1.0);
        }

        private static PageScrollDirection PickDirection(PageScrollDirection direction)
        {
            if (direction != PageScrollDirection.Random)
                return direction;

            return CommonHelper.Chance(0.88)
                ? PageScrollDirection.Up
                : PageScrollDirection.Down;
        }

        private static async Task<bool> ShouldStopAsync(IPage page, Func<IPage, Task<bool>>? shouldStop)
        {
            if (shouldStop == null)
                return false;

            if (page == null || page.IsClosed)
                return true;

            try
            {
                return await shouldStop(page);
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

        private static float EaseInOutQuint(float t)
        {
            t = Math.Clamp(t, 0f, 1f);

            return t < 0.5f
                ? 16 * t * t * t * t * t
                : 1 - MathF.Pow(-2 * t + 2, 5) / 2;
        }

        private static float SmoothStep(float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            return t * t * (3 - 2 * t);
        }
    }
}
