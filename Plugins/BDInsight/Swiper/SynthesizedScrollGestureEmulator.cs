using Microsoft.Playwright;
using SEM;
using System.Runtime.CompilerServices;

namespace BDInsight.Swiper
{
    /// <summary>
    /// 使用 Chrome DevTools Protocol 的 Input.synthesizeScrollGesture 实现滚动。
    /// 它由浏览器合成一个滚动手势，不逐点派发 touchStart/touchMove/touchEnd。
    /// 适合和 dispatchTouchEvent 的真实触屏事件实现做效果对比。
    ///
    /// 注意：
    /// 1. 这个类是“合成滚动手势”，不是完整真实触屏轨迹。
    /// 2. 当前方向逻辑按你的实测结果保留：
    ///    PageScrollDirection.Up   => yDistance < 0，自下向上滑动，页面向上滚动。
    ///    PageScrollDirection.Down => yDistance > 0，自上向下滑动，页面向下滚动。
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

        public sealed class SwipeToElementOptions
        {
            /// <summary>
            /// 最大滑动次数。
            /// </summary>
            public int MaxSwipes { get; set; } = 12;

            /// <summary>
            /// 元素可见比例达到多少算成功。
            /// 0.60 表示元素 60% 面积进入视口就算成功。
            /// </summary>
            public double MinVisibleRatio { get; set; } = 0.60;

            /// <summary>
            /// 元素距离顶部和底部的安全边距。
            /// 避免刚好贴着顶部/底部。
            /// </summary>
            public int ViewportMarginPx { get; set; } = 24;

            /// <summary>
            /// 每次滑动后等待页面稳定的时间。
            /// </summary>
            public int AfterSwipeDelayMs { get; set; } = 180;

            /// <summary>
            /// 如果元素比较大，允许降低可见比例要求。
            /// </summary>
            public bool RelaxLargeElementVisibleRatio { get; set; } = true;

            /// <summary>
            /// 是否验证页面滚动是否发生变化。
            /// </summary>
            public bool VerifyScrollChanged { get; set; } = true;

            /// <summary>
            /// 如果连续几次没有滚动变化，则停止。
            /// </summary>
            public int MaxConsecutiveNoMove { get; set; } = 3;
        }

        public sealed class SwipeToElementResult
        {
            public bool Success { get; set; }

            public int SwipeCount { get; set; }

            public PageScrollDirection? LastDirection { get; set; }

            public ElementViewportState? FinalState { get; set; }

            public List<SynthesizedScrollTrace> Traces { get; set; } = new();

            public string? Reason { get; set; }
        }

        public sealed class ElementViewportState
        {
            public double Top { get; set; }

            public double Bottom { get; set; }

            public double Left { get; set; }

            public double Right { get; set; }

            public double Width { get; set; }

            public double Height { get; set; }

            public double ViewportWidth { get; set; }

            public double ViewportHeight { get; set; }

            public double VisibleWidth { get; set; }

            public double VisibleHeight { get; set; }

            public double VisibleRatio { get; set; }

            public bool IsConnected { get; set; }

            public bool HasBox { get; set; }

            public bool IntersectsViewport { get; set; }

            public bool IsAboveViewport { get; set; }

            public bool IsBelowViewport { get; set; }

            public double CenterY { get; set; }
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

        private enum HandPreference
        {
            Left,
            Right,
            Center
        }

        private sealed class GestureMemory
        {
            public bool HasLastPoint { get; set; }
            public float LastX { get; set; }
            public float LastY { get; set; }
            public PageScrollDirection LastDirection { get; set; }
            public int GestureCount { get; set; }
            public HandPreference HandPreference { get; set; }
            public bool HasHandPreference { get; set; }
        }

        /// <summary>
        /// 按 page 记录连续滑动状态。
        /// 使用 ConditionalWeakTable，page 被释放后这里不会长期占用。
        /// </summary>
        private static readonly ConditionalWeakTable<IPage, GestureMemory> PageGestureMemory = new();

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

                    var profile = ResolveProfile(
                        viewport.Height,
                        actualDirection,
                        i,
                        noMoveCount,
                        options);

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

                PageScrollDirection actualDirection = PickDirection(direction);

                var memory = GetGestureMemory(page);

                var start = PickStartPoint(
                    viewportWidth: vw,
                    viewportHeight: vh,
                    direction: actualDirection,
                    memory: memory);

                int actualDistance = distancePx ?? CommonHelper.NextInt((int)(vh * 0.24), (int)(vh * 0.52));
                actualDistance = ClampDistance(actualDistance, vh);

                // 按你当前实测结果保留：
                // Up   => yDistance < 0
                // Down => yDistance > 0
                double yDistance = actualDirection == PageScrollDirection.Up
                    ? -actualDistance
                    : actualDistance;

                double xDistance = BuildXDistance(actualDirection, memory);

                int actualSpeed = speed ?? GuessSpeed(actualDistance, vh);
                int actualRepeatDelay = Math.Clamp(repeatDelayMs, 60, 1200);
                int actualRepeatCount = Math.Clamp(repeatCount, 0, 3);

                double beforeY = verifyScrollChanged
                    ? await HumanScrollHelper.GetPageScrollYSafeAsync(page)
                    : 0;

                var parameters = new Dictionary<string, object>
                {
                    ["x"] = MathF.Round(start.x, 2),
                    ["y"] = MathF.Round(start.y, 2),
                    ["xDistance"] = Math.Round(xDistance, 2),
                    ["yDistance"] = Math.Round(yDistance, 2),
                    ["speed"] = actualSpeed,
                    ["gestureSourceType"] = "default",
                    ["preventFling"] = preventFling,
                    ["repeatCount"] = actualRepeatCount,
                    ["repeatDelayMs"] = actualRepeatDelay,
                    ["interactionMarkerName"] = $"synth_scroll_{actualDirection.ToString().ToLowerInvariant()}"
                };

                await client.SendAsync("Input.synthesizeScrollGesture", parameters);

                int settleDelay = CommonHelper.NextInt(80, 180) + actualRepeatCount * actualRepeatDelay;
                await Task.Delay(settleDelay, cancellationToken);

                bool changed = true;

                if (verifyScrollChanged)
                {
                    double afterY = await HumanScrollHelper.GetPageScrollYSafeAsync(page);
                    changed = Math.Abs(afterY - beforeY) >= 3;
                }

                UpdateGestureMemory(memory, start.x, start.y, actualDirection);

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

        public static async Task<SwipeToElementResult> SwipeToElementAsync(
            IPage page,
            ICDPSession client,
            ILocator locator,
            SwipeToElementOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var result = new SwipeToElementResult();

            if (page == null || page.IsClosed || client == null || locator == null)
            {
                result.Reason = "Invalid argument.";
                return result;
            }

            options ??= new SwipeToElementOptions();

            try
            {
                for (int i = 0; i <= options.MaxSwipes; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (page.IsClosed)
                    {
                        result.Reason = "Page closed.";
                        return result;
                    }

                    var state = await GetElementViewportStateAsync(locator);
                    result.FinalState = state;

                    if (state == null)
                    {
                        result.Reason = "Element state is null.";
                        return result;
                    }

                    if (!state.IsConnected)
                    {
                        result.Reason = "Element is detached.";
                        return result;
                    }

                    if (!state.HasBox)
                    {
                        result.Reason = "Element has no layout box.";
                        return result;
                    }

                    if (IsElementVisibleEnough(state, options))
                    {
                        result.Success = true;
                        result.Reason = "Element is visible.";
                        return result;
                    }

                    if (i >= options.MaxSwipes)
                    {
                        result.Reason = "Reached max swipes.";
                        await locator.ScrollIntoViewIfNeededAsync();
                        return result;
                    }

                    var direction = ResolveDirectionToElement(state);

                    if (direction == null)
                    {
                        result.Reason = "Cannot resolve swipe direction.";
                        return result;
                    }

                    int distance = ResolveDistanceToElement(state, direction.Value);

                    var trace = await SynthesizeScrollOnceAsync(
                        page: page,
                        client: client,
                        direction: direction.Value,
                        distancePx: distance,
                        speed: null,
                        repeatCount: 0,
                        repeatDelayMs: CommonHelper.NextInt(180, 360),
                        preventFling: true,
                        verifyScrollChanged: options.VerifyScrollChanged,
                        cancellationToken: cancellationToken);

                    if (trace != null)
                    {
                        result.Traces.Add(trace);
                        result.LastDirection = direction.Value;
                        result.SwipeCount++;
                    }

                    if (options.AfterSwipeDelayMs > 0)
                        await Task.Delay(options.AfterSwipeDelayMs, cancellationToken);

                    if (trace == null)
                    {
                        result.Reason = "Swipe failed.";
                        return result;
                    }

                    if (options.VerifyScrollChanged && !trace.ScrollChanged)
                    {
                        int noMoveCount = CountTailNoMove(result.Traces);
                        if (noMoveCount >= options.MaxConsecutiveNoMove)
                        {
                            result.Reason = "Page did not move.";
                            return result;
                        }
                    }
                }

                result.Reason = "Finished.";
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Reason = ex.Message;
                return result;
            }
        }

        public static async Task<SwipeToElementResult> SwipeToElementAsync(
            IPage page,
            ICDPSession client,
            IElementHandle elementHandle,
            SwipeToElementOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var result = new SwipeToElementResult();

            if (page == null || page.IsClosed || client == null || elementHandle == null)
            {
                result.Reason = "Invalid argument.";
                return result;
            }

            options ??= new SwipeToElementOptions();

            try
            {
                for (int i = 0; i <= options.MaxSwipes; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (page.IsClosed)
                    {
                        result.Reason = "Page closed.";
                        return result;
                    }

                    var state = await GetElementViewportStateAsync(elementHandle);
                    result.FinalState = state;

                    if (state == null)
                    {
                        result.Reason = "Element state is null.";
                        return result;
                    }

                    if (!state.IsConnected)
                    {
                        result.Reason = "Element is detached.";
                        return result;
                    }

                    if (!state.HasBox)
                    {
                        result.Reason = "Element has no layout box.";
                        return result;
                    }

                    if (IsElementVisibleEnough(state, options))
                    {
                        result.Success = true;
                        result.Reason = "Element is visible.";
                        return result;
                    }

                    if (i >= options.MaxSwipes)
                    {
                        result.Reason = "Reached max swipes.";
                        await elementHandle.ScrollIntoViewIfNeededAsync();
                        return result;
                    }

                    var direction = ResolveDirectionToElement(state);

                    if (direction == null)
                    {
                        result.Reason = "Cannot resolve swipe direction.";
                        return result;
                    }

                    int distance = ResolveDistanceToElement(state, direction.Value);

                    var trace = await SynthesizeScrollOnceAsync(
                        page: page,
                        client: client,
                        direction: direction.Value,
                        distancePx: distance,
                        speed: null,
                        repeatCount: 0,
                        repeatDelayMs: CommonHelper.NextInt(180, 360),
                        preventFling: true,
                        verifyScrollChanged: options.VerifyScrollChanged,
                        cancellationToken: cancellationToken);

                    if (trace != null)
                    {
                        result.Traces.Add(trace);
                        result.LastDirection = direction.Value;
                        result.SwipeCount++;
                    }

                    if (options.AfterSwipeDelayMs > 0)
                        await Task.Delay(options.AfterSwipeDelayMs, cancellationToken);

                    if (trace == null)
                    {
                        result.Reason = "Swipe failed.";
                        return result;
                    }

                    if (options.VerifyScrollChanged && !trace.ScrollChanged)
                    {
                        int noMoveCount = CountTailNoMove(result.Traces);
                        if (noMoveCount >= options.MaxConsecutiveNoMove)
                        {
                            result.Reason = "Page did not move.";
                            return result;
                        }
                    }
                }

                result.Reason = "Finished.";
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Reason = ex.Message;
                return result;
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

                // 向下滑通常更短、更谨慎
                if (direction == PageScrollDirection.Down)
                    distance = (int)(distance * CommonHelper.NextDouble(0.55, 0.78));
            }

            // 如果没动，下一次稍微加大距离
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
                return CommonHelper.Chance(0.70)
                    ? HumanScrollMode.FineTune
                    : HumanScrollMode.Short;

            if (index == 0 && CommonHelper.Chance(0.46))
                return HumanScrollMode.Long;

            double r = CommonHelper.NextDouble();

            if (r < 0.46)
                return HumanScrollMode.Short;

            if (r < 0.78)
                return HumanScrollMode.Long;

            if (r < 0.93)
                return HumanScrollMode.Probe;

            return HumanScrollMode.FineTune;
        }

        /// <summary>
        /// 根据方向选择起点。
        ///
        /// Up:
        ///     页面向上滚动，模拟手指自下向上滑，所以起点在屏幕中下部。
        ///
        /// Down:
        ///     页面向下滚动，模拟手指自上向下滑，所以起点在屏幕中上部。
        ///
        /// 同时增加：
        /// 1. 左手/右手/居中持握偏移。
        /// 2. 连续滑动起点轻微漂移。
        /// 3. 少量边缘滑动概率。
        /// </summary>
        private static (float x, float y) PickStartPoint(
            int viewportWidth,
            int viewportHeight,
            PageScrollDirection direction,
            GestureMemory memory)
        {
            EnsureHandPreference(memory);

            float x;
            float y;

            bool useLastPointDrift =
                memory.HasLastPoint &&
                memory.LastDirection == direction &&
                CommonHelper.Chance(0.62);

            if (useLastPointDrift)
            {
                // 连续滑动时，不要每次完全随机，基于上次点位轻微漂移
                x = memory.LastX + (float)CommonHelper.NextDouble(
                    -viewportWidth * 0.045,
                    viewportWidth * 0.045);

                if (direction == PageScrollDirection.Up)
                {
                    y = memory.LastY + (float)CommonHelper.NextDouble(
                        -viewportHeight * 0.055,
                        viewportHeight * 0.055);

                    y = ClampFloat(
                        y,
                        viewportHeight * 0.62f,
                        viewportHeight * 0.86f);
                }
                else
                {
                    y = memory.LastY + (float)CommonHelper.NextDouble(
                        -viewportHeight * 0.050,
                        viewportHeight * 0.050);

                    y = ClampFloat(
                        y,
                        viewportHeight * 0.18f,
                        viewportHeight * 0.45f);
                }

                x = ApplyHandClampX(x, viewportWidth, memory.HandPreference);
            }
            else
            {
                x = PickXByHandPreference(viewportWidth, memory.HandPreference);

                switch (direction)
                {
                    case PageScrollDirection.Up:
                        // 自下向上滑，起点偏中下
                        y = (float)CommonHelper.NextDouble(
                            viewportHeight * 0.64,
                            viewportHeight * 0.84);
                        break;

                    case PageScrollDirection.Down:
                        // 自上向下滑，起点偏中上
                        y = (float)CommonHelper.NextDouble(
                            viewportHeight * 0.20,
                            viewportHeight * 0.42);
                        break;

                    default:
                        y = (float)CommonHelper.NextDouble(
                            viewportHeight * 0.36,
                            viewportHeight * 0.66);
                        break;
                }
            }

            // 少量情况下，模拟左/右边缘附近操作。
            // 不要贴边，避免点到滚动条、侧边菜单、返回手势区。
            if (CommonHelper.Chance(0.08))
            {
                if (CommonHelper.Chance(0.5))
                {
                    x = (float)CommonHelper.NextDouble(
                        viewportWidth * 0.18,
                        viewportWidth * 0.30);
                }
                else
                {
                    x = (float)CommonHelper.NextDouble(
                        viewportWidth * 0.70,
                        viewportWidth * 0.82);
                }
            }

            // 最终安全边界
            x = ClampFloat(x, 16, viewportWidth - 16);

            if (direction == PageScrollDirection.Up)
            {
                y = ClampFloat(y, viewportHeight * 0.58f, viewportHeight - 36);
            }
            else if (direction == PageScrollDirection.Down)
            {
                y = ClampFloat(y, 48, viewportHeight * 0.48f);
            }
            else
            {
                y = ClampFloat(y, 48, viewportHeight - 48);
            }

            return (x, y);
        }

        private static float PickXByHandPreference(
            int viewportWidth,
            HandPreference handPreference)
        {
            return handPreference switch
            {
                HandPreference.Left => (float)CommonHelper.NextDouble(
                    viewportWidth * 0.30,
                    viewportWidth * 0.50),

                HandPreference.Right => (float)CommonHelper.NextDouble(
                    viewportWidth * 0.50,
                    viewportWidth * 0.70),

                _ => (float)CommonHelper.NextDouble(
                    viewportWidth * 0.38,
                    viewportWidth * 0.62)
            };
        }

        private static float ApplyHandClampX(
            float x,
            int viewportWidth,
            HandPreference handPreference)
        {
            return handPreference switch
            {
                HandPreference.Left => ClampFloat(
                    x,
                    viewportWidth * 0.24f,
                    viewportWidth * 0.56f),

                HandPreference.Right => ClampFloat(
                    x,
                    viewportWidth * 0.44f,
                    viewportWidth * 0.76f),

                _ => ClampFloat(
                    x,
                    viewportWidth * 0.30f,
                    viewportWidth * 0.70f)
            };
        }

        private static void EnsureHandPreference(GestureMemory memory)
        {
            if (memory.HasHandPreference)
                return;

            double r = CommonHelper.NextDouble();

            // 右手稍多，居中其次，左手少量
            if (r < 0.20)
                memory.HandPreference = HandPreference.Left;
            else if (r < 0.72)
                memory.HandPreference = HandPreference.Right;
            else
                memory.HandPreference = HandPreference.Center;

            memory.HasHandPreference = true;
        }

        private static double BuildXDistance(
            PageScrollDirection direction,
            GestureMemory memory)
        {
            // 大多数情况下垂直滑动，横向距离非常小
            if (!CommonHelper.Chance(0.36))
                return 0;

            double drift = CommonHelper.NextDouble(-8, 8);

            // 根据持握习惯增加一点点倾斜感，不要太大
            if (memory.HandPreference == HandPreference.Right)
            {
                drift += direction == PageScrollDirection.Up
                    ? CommonHelper.NextDouble(-3, 1)
                    : CommonHelper.NextDouble(-1, 3);
            }
            else if (memory.HandPreference == HandPreference.Left)
            {
                drift += direction == PageScrollDirection.Up
                    ? CommonHelper.NextDouble(-1, 3)
                    : CommonHelper.NextDouble(-3, 1);
            }

            return Math.Round(Math.Clamp(drift, -12, 12), 2);
        }

        private static void UpdateGestureMemory(
            GestureMemory memory,
            float x,
            float y,
            PageScrollDirection direction)
        {
            memory.HasLastPoint = true;
            memory.LastX = x;
            memory.LastY = y;
            memory.LastDirection = direction;
            memory.GestureCount++;
        }

        private static GestureMemory GetGestureMemory(IPage page)
        {
            return PageGestureMemory.GetValue(page, _ => new GestureMemory());
        }

        private static async Task<ElementViewportState?> GetElementViewportStateAsync(ILocator locator)
        {
            if (locator == null)
                return null;

            try
            {
                return await locator.EvaluateAsync<ElementViewportState>(GetElementViewportStateScript());
            }
            catch
            {
                return null;
            }
        }

        private static async Task<ElementViewportState?> GetElementViewportStateAsync(IElementHandle elementHandle)
        {
            if (elementHandle == null)
                return null;

            try
            {
                return await elementHandle.EvaluateAsync<ElementViewportState>(GetElementViewportStateScript());
            }
            catch
            {
                return null;
            }
        }

        private static string GetElementViewportStateScript()
        {
            return @"el => {
                try {
                    const vw = window.innerWidth || document.documentElement.clientWidth || 0;
                    const vh = window.innerHeight || document.documentElement.clientHeight || 0;

                    if (!el) {
                        return {
                            top: 0,
                            bottom: 0,
                            left: 0,
                            right: 0,
                            width: 0,
                            height: 0,
                            viewportWidth: vw,
                            viewportHeight: vh,
                            visibleWidth: 0,
                            visibleHeight: 0,
                            visibleRatio: 0,
                            isConnected: false,
                            hasBox: false,
                            intersectsViewport: false,
                            isAboveViewport: false,
                            isBelowViewport: false,
                            centerY: 0
                        };
                    }

                    const isConnected = !!el.isConnected;
                    const r = el.getBoundingClientRect();

                    const width = Math.max(0, Number(r.width || 0));
                    const height = Math.max(0, Number(r.height || 0));
                    const hasBox = width > 0 && height > 0;

                    const visibleLeft = Math.max(0, r.left);
                    const visibleRight = Math.min(vw, r.right);
                    const visibleTop = Math.max(0, r.top);
                    const visibleBottom = Math.min(vh, r.bottom);

                    const visibleWidth = Math.max(0, visibleRight - visibleLeft);
                    const visibleHeight = Math.max(0, visibleBottom - visibleTop);

                    const area = width * height;
                    const visibleArea = visibleWidth * visibleHeight;
                    const visibleRatio = area > 0 ? visibleArea / area : 0;

                    const intersectsViewport =
                        hasBox &&
                        r.bottom > 0 &&
                        r.top < vh &&
                        r.right > 0 &&
                        r.left < vw;

                    return {
                        top: Number(r.top || 0),
                        bottom: Number(r.bottom || 0),
                        left: Number(r.left || 0),
                        right: Number(r.right || 0),
                        width: width,
                        height: height,
                        viewportWidth: vw,
                        viewportHeight: vh,
                        visibleWidth: visibleWidth,
                        visibleHeight: visibleHeight,
                        visibleRatio: visibleRatio,
                        isConnected: isConnected,
                        hasBox: hasBox,
                        intersectsViewport: intersectsViewport,
                        isAboveViewport: r.bottom <= 0,
                        isBelowViewport: r.top >= vh,
                        centerY: Number((r.top + r.bottom) / 2)
                    };
                } catch {
                    return null;
                }
            }";
        }

        private static bool IsElementVisibleEnough(
            ElementViewportState state,
            SwipeToElementOptions options)
        {
            if (state == null || !state.IsConnected || !state.HasBox)
                return false;

            if (!state.IntersectsViewport)
                return false;

            double minRatio = Math.Clamp(options.MinVisibleRatio, 0.05, 1.0);

            if (options.RelaxLargeElementVisibleRatio)
            {
                // 元素高度超过视口 70% 时，不强求 60% 面积都可见
                if (state.Height >= state.ViewportHeight * 0.70)
                    minRatio = Math.Min(minRatio, 0.35);

                // 元素高度超过视口时，只要中间部分进入视口即可
                if (state.Height >= state.ViewportHeight)
                    minRatio = Math.Min(minRatio, 0.25);
            }

            bool ratioOk = state.VisibleRatio >= minRatio;

            bool marginOk =
                state.Bottom >= options.ViewportMarginPx &&
                state.Top <= state.ViewportHeight - options.ViewportMarginPx;

            return ratioOk && marginOk;
        }

        private static PageScrollDirection? ResolveDirectionToElement(ElementViewportState state)
        {
            if (state == null)
                return null;

            // 元素在当前视口下方，需要页面向上滚动
            if (state.IsBelowViewport || state.Top > state.ViewportHeight * 0.72)
                return PageScrollDirection.Up;

            // 元素在当前视口上方，需要页面向下滚动
            if (state.IsAboveViewport || state.Bottom < state.ViewportHeight * 0.28)
                return PageScrollDirection.Down;

            // 元素已经和视口有交集，但还不够可见
            // 以元素中心点判断应该往哪边补一点
            if (state.CenterY > state.ViewportHeight * 0.60)
                return PageScrollDirection.Up;

            if (state.CenterY < state.ViewportHeight * 0.40)
                return PageScrollDirection.Down;

            return null;
        }

        private static int ResolveDistanceToElement(
            ElementViewportState state,
            PageScrollDirection direction)
        {
            int vh = Math.Max(320, (int)state.ViewportHeight);

            double distance;

            if (direction == PageScrollDirection.Up)
            {
                // 元素在下方，页面向上滚动。
                // 如果元素离得很远，用中等距离多次滑，不要一次过猛。
                double overflow = Math.Max(0, state.Bottom - state.ViewportHeight);
                double gap = Math.Max(0, state.Top - state.ViewportHeight);

                distance = Math.Max(overflow, gap);
                distance += vh * CommonHelper.NextDouble(0.18, 0.32);
            }
            else
            {
                // 元素在上方，页面向下滚动。
                double overflow = Math.Max(0, -state.Top);
                double gap = Math.Max(0, -state.Bottom);

                distance = Math.Max(overflow, gap);
                distance += vh * CommonHelper.NextDouble(0.16, 0.28);
            }

            // 保守一点，避免一次滑过目标
            int min = Math.Max(60, (int)(vh * 0.08));
            int max = Math.Max(min + 1, (int)(vh * 0.46));

            int finalDistance = (int)Math.Round(distance);
            finalDistance = Math.Clamp(finalDistance, min, max);

            // 加一点随机，不要每次刚好一样
            finalDistance += CommonHelper.NextInt(-12, 13);
            finalDistance = Math.Clamp(finalDistance, min, max);

            return finalDistance;
        }

        private static int CountTailNoMove(List<SynthesizedScrollTrace> traces)
        {
            if (traces == null || traces.Count == 0)
                return 0;

            int count = 0;

            for (int i = traces.Count - 1; i >= 0; i--)
            {
                if (traces[i].ScrollChanged)
                    break;

                count++;
            }

            return count;
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

        private static float ClampFloat(float value, float min, float max)
        {
            if (max < min)
                return value;

            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }
    }
}