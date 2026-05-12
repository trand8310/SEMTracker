namespace BDInsight
{
    using Microsoft.Playwright;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public static class TouchDragHelper
    {
        public static async Task<bool> WaitAllVisibleWithTextsAsync(
        IPage page,
        int timeout = 5000)
        {
            try
            {
                var btnSlide = page.Locator(".btn_slide").First;
                var slideToUnlock = page.Locator(".slidetounlock").First;

                await btnSlide.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = timeout
                });

                await slideToUnlock.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = timeout
                });

                var text1 = page.GetByText("亲，请拖动下方滑块完成验证", new PageGetByTextOptions
                {
                    Exact = false
                }).First;

                var text2 = page.GetByText("向右滑动验证", new PageGetByTextOptions
                {
                    Exact = false
                }).First;

                await text1.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = timeout
                });

                await text2.WaitForAsync(new LocatorWaitForOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = timeout
                });

                return await btnSlide.IsVisibleAsync()
                    && await slideToUnlock.IsVisibleAsync()
                    && await text1.IsVisibleAsync()
                    && await text2.IsVisibleAsync();
            }
            catch
            {
                return false;
            }
        }


        public static async Task<bool> DragSliderAsync(
            IPage page,
            ICDPSession cdpSession,
            string handleSelector,
            string trackSelector,
            CancellationToken token = default)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (cdpSession == null) throw new ArgumentNullException(nameof(cdpSession));

            var handle = page.Locator(handleSelector).First;
            var track = page.Locator(trackSelector).First;

            if (await handle.CountAsync() == 0 || await track.CountAsync() == 0)
                return false;

            var handleBox = await handle.BoundingBoxAsync();
            var trackBox = await track.BoundingBoxAsync();

            if (handleBox == null || trackBox == null)
                return false;

            double startX = handleBox.X + handleBox.Width / 2.0;
            double startY = handleBox.Y + handleBox.Height / 2.0;

            // 终点留一点边距，避免拖过头
            double endX = trackBox.X + trackBox.Width - Math.Max(2, handleBox.Width / 3.0);
            double endY = startY;

            // 拖动总距离
            double totalDx = endX - startX;
            if (totalDx <= 3)
                return false;

            // 步数可以稍多一点，更平滑
            int steps = Random.Shared.Next(15, 25);

            // 起点按下
            await DispatchTouchAsync(cdpSession, "touchStart", startX, startY);
            await Task.Delay(Random.Shared.Next(60, 120), token);

            for (int i = 1; i <= steps; i++)
            {
                token.ThrowIfCancellationRequested();

                double t = (double)i / steps;

                // easeInOut，让前后慢、中间快
                double eased = EaseInOutCubic(t);

                // 主位移
                double currentX = startX + totalDx * eased;

                // 轻微抖动
                double jitterY = Random.Shared.NextDouble() * 2.4 - 1.2;
                double jitterX = Random.Shared.NextDouble() * 1.2 - 0.6;

                // 后段微调，避免过于机械
                if (i > steps * 0.8)
                {
                    jitterX *= 0.5;
                    jitterY *= 0.5;
                }

                await DispatchTouchAsync(
                    cdpSession,
                    "touchMove",
                    currentX + jitterX,
                    endY + jitterY);

                await Task.Delay(Random.Shared.Next(12, 25), token);
            }

            // 结束前轻微补一点点，模拟人手放开前的稳定动作
            await DispatchTouchAsync(cdpSession, "touchMove", endX - 1, endY);
            await Task.Delay(Random.Shared.Next(20, 50), token);

            await DispatchTouchEndAsync(cdpSession);
            return true;
        }

        private static async Task DispatchTouchAsync(
            ICDPSession cdpSession,
            string type,
            double x,
            double y)
        {
            await cdpSession.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
            {
                ["type"] = type,
                ["touchPoints"] = new object[]
                {
                new Dictionary<string, object>
                {
                    ["x"] = x,
                    ["y"] = y,
                    ["radiusX"] = 2,
                    ["radiusY"] = 2,
                    ["force"] = 1,
                    ["id"] = 0
                }
                },
                ["modifiers"] = 0
            });
        }

        private static async Task DispatchTouchEndAsync(ICDPSession cdpSession)
        {
            await cdpSession.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
            {
                ["type"] = "touchEnd",
                ["touchPoints"] = Array.Empty<object>(),
                ["modifiers"] = 0
            });
        }

        private static double EaseInOutCubic(double t)
        {
            return t < 0.5
                ? 4 * t * t * t
                : 1 - Math.Pow(-2 * t + 2, 3) / 2;
        }
    }
}
