using Microsoft.Playwright;
using Newtonsoft.Json;
using SEM.Models;
using System.Diagnostics;

namespace SEM
{
    public static class CDPHelper
    {

        public static async Task SynthesizeScrollGesture(ICDPSession cdpSession)
        {
            await cdpSession.SendAsync("Input.synthesizeScrollGesture", new Dictionary<string, object>()
            {


            });
        }
        public static async Task InitCDPSession(ICDPSession cdpSession, int maxTouchPoints)
        {
            //await SetAutoDarkModeOverride(cdpSession, true);
            await CDPHelper.SetTouchEmulationEnabled(cdpSession, true, maxTouchPoints);
            await CDPHelper.SetScrollbarsHidden(cdpSession, true);
            await CDPHelper.SetEmitTouchEventsForMouse(cdpSession, true);
            await CDPHelper.ChangeDeviceOrientationAsync(cdpSession);
        }

        public static async Task SetDeviceMetricsOverride(ICDPSession cdpSession, int width, int height, float deviceScaleFactor, bool mobile = true)
        {
            try
            {
                await cdpSession.SendAsync("Emulation.setDeviceMetricsOverride", new Dictionary<string, object>() {
                    {"width",width },
                    {"height",height },
                    {"deviceScaleFactor",deviceScaleFactor },
                    {"mobile",mobile },
                });
            }
            catch (Exception)
            {
            }

        }

        public static async Task SetUserAgentOverride(ICDPSession cdpSession, string userAgent, string platform = "Android")
        {
            try
            {
                await cdpSession.SendAsync("Emulation.setUserAgentOverride", new Dictionary<string, object>()
                {
                     {"userAgent",userAgent },
                     {"platform",platform },
                });
            }
            catch (Exception)
            {
            }

        }

        public static async Task SetGeolocationOverride(ICDPSession cdpSession, double latitude, double longitude)
        {
            try
            {
                await cdpSession.SendAsync("Emulation.setGeolocationOverride", new Dictionary<string, object>()
                {
                     {"latitude",latitude },
                     {"longitude",longitude },
                });
            }
            catch (Exception)
            {
            }
        }

        public static async Task SetTimezoneOverride(ICDPSession cdpSession, string timezoneId)
        {
            try
            {
                await cdpSession.SendAsync("Emulation.setTimezoneOverride", new Dictionary<string, object>()
                {
                     {"timezoneId",timezoneId }
                });
            }
            catch (Exception)
            {
            }

        }

        public static async Task SetAutoDarkModeOverride(ICDPSession cdpSession, bool enabled)
        {
            try
            {
                await cdpSession.SendAsync("Emulation.setAutoDarkModeOverride", new Dictionary<string, object>()
                {
                     {"enabled",enabled }
                });
            }
            catch (Exception)
            {
            }

        }


        public static async Task SetBrowserPermission(ICDPSession cdpSession)
        {
            try
            {
                //var originUri = new Uri(url);
                //var origin = $"{originUri.Scheme}://{originUri.Host}";
                await cdpSession.SendAsync("Browser.setPermission", new Dictionary<string, object>
                {
                    ["permission"] = new Dictionary<string, object>
                    {
                        ["name"] = "geolocation"
                    },
                    ["setting"] = "granted"
                });
            }
            catch (Exception)
            {
            }

        }



        public static async Task SetScrollbarsHidden(ICDPSession cdpSession, bool hidden = true)
        {
            try
            {
                await cdpSession.SendAsync("Emulation.setScrollbarsHidden", new Dictionary<string, object>() {
                    {"hidden",hidden },
                });
            }
            catch (Exception)
            {
            }

        }


        public static async Task SetTouchEmulationEnabled(ICDPSession cdpSession, bool enabled = true, int maxTouchPoints = 1)
        {
            try
            {
                await cdpSession.SendAsync("Emulation.setTouchEmulationEnabled", new Dictionary<string, object>() {
                {"enabled",enabled },
                {"maxTouchPoints",maxTouchPoints },
            });
            }
            catch (Exception)
            {

            }

        }

        public static async Task SetEmitTouchEventsForMouse(ICDPSession cdpSession, bool enabled = true, string configuration = "mobile")
        {
            try
            {
                await cdpSession.SendAsync("Emulation.setEmitTouchEventsForMouse", new Dictionary<string, object>() {
                {"enabled",enabled },
                {"configuration",configuration},
            });
            }
            catch (Exception)
            {

            }
        }

        public static async Task ClearDataForOriginAsync(ICDPSession cdpSession, string origin = "*", string storageTypes = "cache_storage,cookies,local_storage")
        {
            try
            {
                var dict = new System.Collections.Generic.Dictionary<string, object>();
                dict.Add("origin", origin);
                dict.Add("storageTypes", storageTypes);
                await cdpSession.SendAsync("Storage.clearDataForOrigin", dict);
            }
            catch (Exception)
            {

            }
        }






        public static async Task TouchMoveAsync(ICDPSession cdpSession, TouchPoint startPoint, TouchPoint endPoint)
        {
            //await SetEmitTouchEventsForMouse(cdpSession, true);
            try
            {
                await cdpSession.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>() {
                    { "type","touchStart"},
                    { "touchPoints",new object[] {startPoint}},
                    { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},
                    { "modifiers",0},
                });
                SpinWait.SpinUntil(() => false, new Random().Next(20, 50));
                await cdpSession.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>() {
                        { "type","touchMove"},
                        { "touchPoints",new object[] {endPoint}},
                        { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},
                        { "modifiers",0},
                 });
                SpinWait.SpinUntil(() => false, new Random().Next(20, 50));
                await cdpSession.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>() {
                        { "type","touchEnd"},
                        { "touchPoints",new object[] {}},
                        { "timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},
                        { "modifiers",0},
                    });
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
            finally
            {


            }

        }


        public static async Task TouchMove(ICDPSession cdpSession, TouchPoint startPoint, TouchPoint endPoint)
        {
            var args1 = new Dictionary<string, object>();
            args1.Add("type", "touchStart");
            args1.Add("touchPoints", new TouchPoint[] { startPoint });
            await cdpSession.SendAsync("Input.dispatchTouchEvent", args1);

            SpinWait.SpinUntil(() => false, new Random().Next(20, 30));
            var args2 = new Dictionary<string, object>();
            args2.Add("type", "touchMove");
            args2.Add("touchPoints", new TouchPoint[] { endPoint });
            args2.Add("timestamp", 0.1);
            await cdpSession.SendAsync("Input.dispatchTouchEvent", args2);
            SpinWait.SpinUntil(() => false, new Random().Next(20, 30));
            var args3 = new Dictionary<string, object>();
            args3.Add("type", "touchEnd");
            args3.Add("touchPoints", new TouchPoint[] { });
            await cdpSession.SendAsync("Input.dispatchTouchEvent", args3);
        }

        public static async Task Swipe(IPage page, ICDPSession cdpSession, ILocator element)
        {
            var bounding = await element.BoundingBoxAsync();
            var x = new Random().Next((int)(bounding.Width * 0.45), (int)(bounding.Width * 0.55));
            var y = new Random().Next((int)(bounding.Height * 0.45), (int)(bounding.Height * 0.55));
            await page.Mouse.MoveAsync(x, y);
            await page.Mouse.DownAsync(new MouseDownOptions() { Button = MouseButton.Left });
            await page.Mouse.MoveAsync(x + 100, y, new MouseMoveOptions() { Steps = 5 });
            await page.Mouse.UpAsync();
        }



        public static async Task<bool> TapAsync(IPage page, ICDPSession cdpSession, ILocator element, int dir = 0, Action<string>? action = null)
        {
            try
            {
                await ClearDeviceOrientationOverrideAsync(cdpSession);
                //await CDPHelper.SetEmitTouchEventsForMouse(cdpSession, true);
                var bounding = await element.BoundingBoxAsync();
                if (bounding != null)
                {
                    var xmin = 15;
                    var xmax = 85;
                    var ymin = 15;
                    var ymax = 85;
                    if (dir == 1)
                    {
                        //靠上
                        ymin = 15;
                        ymax = 45;
                    }
                    else if (dir == 2)
                    {
                        //居中
                        ymin = 45;
                        ymax = 65;
                    }
                    else if (dir == 3)
                    {
                        //靠下
                        ymin = 55;
                        ymax = 85;
                    }

                    var x = bounding.Width * (CommonHelper.RandomRange(xmin, xmax) * 0.01);
                    var y = bounding.Height * (CommonHelper.RandomRange(ymin, ymax) * 0.01);
                    if (x == 0)
                        x = (double)(bounding.Width / 2.0);
                    if (y == 0)
                        y = (double)(bounding.Height / 2.0);

                    action?.Invoke($"Tap:bounding={JsonConvert.SerializeObject(bounding)},action:x={x},y={y}");

                    await ChangeDeviceOrientationAsync(cdpSession);
                    await element.TapAsync(new LocatorTapOptions() { Position = new Position() { X = (float)x, Y = (float)y }, Force = true, Timeout = 5000 });
                    return true;
                }
            }
            catch (TimeoutException)
            {
                action?.Invoke($"Tap:超时");
            }
            catch
            {
                throw;
            }
            finally
            {
                //await SetEmitTouchEventsForMouse(cdpSession, true);
            }
            return false;

        }

        public static async Task<bool> TapAsync(IPage page, ICDPSession cdpSession, IElementHandle element, int dir = 0, Action<string>? action = null)
        {
            try
            {
                await ClearDeviceOrientationOverrideAsync(cdpSession);
                //await CDPHelper.SetEmitTouchEventsForMouse(cdpSession, true);
                //await element.ScrollIntoViewIfNeededAsync();
                var bounding = await element.BoundingBoxAsync();
                if (bounding != null)
                {
                    var xmin = 15;
                    var xmax = 85;
                    var ymin = 15;
                    var ymax = 85;
                    if (dir == 1)
                    {
                        //靠上
                        ymin = 15;
                        ymax = 45;
                    }
                    else if (dir == 2)
                    {
                        //居中
                        ymin = 45;
                        ymax = 65;
                    }
                    else if (dir == 3)
                    {
                        //靠下
                        ymin = 55;
                        ymax = 85;
                    }

                    var x = bounding.Width * (CommonHelper.RandomRange(xmin, xmax) * 0.01);
                    var y = bounding.Height * (CommonHelper.RandomRange(ymin, ymax) * 0.01);
                    if (x == 0)
                        x = (double)(bounding.Width / 2.0);
                    if (y == 0)
                        y = (double)(bounding.Height / 2.0);
                    action?.Invoke($"Tap:bounding={JsonConvert.SerializeObject(bounding)},action:x={x},y={y}");


                    await ChangeDeviceOrientationAsync(cdpSession);
                    await element.TapAsync(new ElementHandleTapOptions() { Position = new Position() { X = (float)x, Y = (float)y }, Force = true, Timeout = 5000 });
                    return true;
                }
            }
            catch (TimeoutException)
            {
                action?.Invoke($"Tap:超时");
            }
            catch
            {
                throw;
            }
            finally
            {
                // await SetEmitTouchEventsForMouse(cdpSession, true);
            }
            return false;
        }

        public static async Task<bool> MouseClickAsync(IPage page, ICDPSession cdpSession, ILocator element, int dir = 0, int timeout = 5000, Action<string>? action = null)
        {
            try
            {
                await ClearDeviceOrientationOverrideAsync(cdpSession);
                var bounding = await element.BoundingBoxAsync();
                if (bounding != null)
                {
                    var xmin = 20;
                    var xmax = 80;
                    var ymin = 20;
                    var ymax = 80;
                    if (dir == 1)
                    {
                        //靠上
                        ymin = 15;
                        ymax = 45;
                    }
                    else if (dir == 2)
                    {
                        //居中
                        ymin = 45;
                        ymax = 65;
                    }
                    else if (dir == 3)
                    {
                        //靠下
                        ymin = 55;
                        ymax = 85;
                    }

                    var x = bounding.Width * (CommonHelper.RandomRange(xmin, xmax) * 0.01);
                    var y = bounding.Height * (CommonHelper.RandomRange(ymin, ymax) * 0.01);
                    if (x == 0)
                        x = (double)(bounding.Width / 2.0);
                    if (y == 0)
                        y = (double)(bounding.Height / 2.0);

                    action?.Invoke($"MouseClick::bounding={JsonConvert.SerializeObject(bounding)},action:x={x},y={y}");

                    await ChangeDeviceOrientationAsync(cdpSession);
                    await element.ClickAsync(new LocatorClickOptions() { Position = new Position() { X = (float)x, Y = (float)y }, Force = true, Timeout = timeout });
                    return true;
                }
            }
            catch (TimeoutException)
            {
                action?.Invoke($"MouseClick:超时");
            }
            catch
            {
                throw;
            }
            finally
            {

            }
            return false;
        }

        public static async Task DispatchMouseMoveAsync(
        ICDPSession cdp,
        double x,
        double y,
        CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await cdp.SendAsync("Input.dispatchMouseEvent", new Dictionary<string, object>
            {
                ["type"] = "mouseMoved",
                ["x"] = x,
                ["y"] = y,
                ["button"] = "none",
                ["buttons"] = 0,
                ["pointerType"] = "mouse"
            });
        }


        public static async Task DispatchMouseClickAsync(
           ICDPSession cdp,
           double x,
           double y,
           CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await cdp.SendAsync("Input.dispatchMouseEvent", new Dictionary<string, object>
            {
                ["type"] = "mousePressed",
                ["x"] = x,
                ["y"] = y,
                ["button"] = "left",
                ["buttons"] = 1,
                ["clickCount"] = 1,
                ["pointerType"] = "mouse"
            });

            await Task.Delay(50, cancellationToken);

            await cdp.SendAsync("Input.dispatchMouseEvent", new Dictionary<string, object>
            {
                ["type"] = "mouseReleased",
                ["x"] = x,
                ["y"] = y,
                ["button"] = "left",
                ["buttons"] = 1,
                ["clickCount"] = 1,
                ["pointerType"] = "mouse"
            });
        }




        public static async Task<bool> MouseClickAsync(IFrame page, ICDPSession cdpSession, ILocator element, int dir = 0, Action<string>? action = null)
        {
            try
            {
                await ClearDeviceOrientationOverrideAsync(cdpSession);
                var bounding = await element.BoundingBoxAsync();
                if (bounding != null)
                {
                    var xmin = 15;
                    var xmax = 85;
                    var ymin = 15;
                    var ymax = 85;
                    if (dir == 1)
                    {
                        //靠上
                        ymin = 15;
                        ymax = 45;
                    }
                    else if (dir == 2)
                    {
                        //居中
                        ymin = 45;
                        ymax = 65;
                    }
                    else if (dir == 3)
                    {
                        //靠下
                        ymin = 55;
                        ymax = 85;
                    }

                    var x = bounding.Width * (CommonHelper.RandomRange(xmin, xmax) * 0.01);
                    var y = bounding.Height * (CommonHelper.RandomRange(ymin, ymax) * 0.01);
                    if (x == 0)
                        x = (double)(bounding.Width / 2.0);
                    if (y == 0)
                        y = (double)(bounding.Height / 2.0);

                    action?.Invoke($"MouseClick::bounding={JsonConvert.SerializeObject(bounding)},action:x={x},y={y}");

                    await ChangeDeviceOrientationAsync(cdpSession);
                    await element.ClickAsync(new LocatorClickOptions() { Position = new Position() { X = (float)x, Y = (float)y }, Force = true, Timeout = 5000 });
                    return true;
                }
            }
            catch (TimeoutException)
            {
                action?.Invoke($"MouseClick:超时");
            }
            catch
            {
                throw;
            }
            finally
            {

            }
            return false;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="page"></param>
        /// <param name="cdpSession"></param>
        /// <param name="element"></param>
        /// <param name="dir"></param>
        /// <returns></returns>
        public static async Task<bool> MouseClickAsync(IPage page, ICDPSession cdpSession, IElementHandle element, int dir = 0, int timeout = 5000, Action<string>? action = null)
        {
            try
            {
                await ClearDeviceOrientationOverrideAsync(cdpSession);
                var bounding = await element.BoundingBoxAsync();
                if (bounding != null)
                {

                    var xmin = 15;
                    var xmax = 85;
                    var ymin = 15;
                    var ymax = 85;
                    if (dir == 1)
                    {
                        //靠上
                        ymin = 15;
                        ymax = 45;
                    }
                    else if (dir == 2)
                    {
                        //居中
                        ymin = 45;
                        ymax = 65;
                    }
                    else if (dir == 3)
                    {
                        //靠下
                        ymin = 55;
                        ymax = 85;
                    }

                    var x = bounding.Width * (CommonHelper.RandomRange(xmin, xmax) * 0.01);
                    var y = bounding.Height * (CommonHelper.RandomRange(ymin, ymax) * 0.01);
                    if (x == 0)
                        x = (double)(bounding.Width / 2.0);
                    if (y == 0)
                        y = (double)(bounding.Height / 2.0);

                    action?.Invoke($"MouseClick::bounding={JsonConvert.SerializeObject(bounding)},action:x={x},y={y}");
                    //var x = new Random().Next((int)(bounding.Width * 0.20), (int)(bounding.Width * 0.80));
                    //var y = new Random().Next((int)(bounding.Height * 0.20), (int)(bounding.Height * 0.80));
                    await ChangeDeviceOrientationAsync(cdpSession);
                    await element.ClickAsync(new ElementHandleClickOptions() { Position = new Position() { X = (float)x, Y = (float)y }, Force = true, Timeout = timeout });
                    return true;
                }
            }
            catch (TimeoutException)
            {
                action?.Invoke($"MouseClick:超时");
            }
            catch
            {
                throw;
            }
            finally
            {

            }
            return false;
        }


        public static async Task<bool> FindItemAndClickAsync(IPage page, ICDPSession cdpSession, string selector, int dir = 0, int timeout = 5000, Action<string>? action = null)
        {
            var element = page.Locator(selector);
            if (await element.CountAsync() > 0)
            {
                await CDPHelper.MouseClickAsync(page, cdpSession, element.First, dir, timeout, action);
                await Task.Delay(500);
                return true;

            }
            return false;
        }




        public static async Task<bool> TouchClickVisibleLocatorAsync(
           IPage page,
           ICDPSession client,
           ILocator locator,
           CancellationToken cancellationToken = default,
           int insetPercentMin = 30,
           int insetPercentMax = 70,
           int minHoldMs = 40,
           int maxHoldMs = 90,
           int minMoveDelayMs = 12,
           int maxMoveDelayMs = 35,
           int minPostDelayMs = 180,
           int maxPostDelayMs = 450,
           bool useTinyMove = true)
        {
            if (page == null || page.IsClosed || client == null || locator == null)
                return false;

            try
            {
                await locator.ScrollIntoViewIfNeededAsync();

                var box = await locator.BoundingBoxAsync();
                if (box == null || box.Width <= 2 || box.Height <= 2)
                    return false;

                var viewport = page.ViewportSize;
                if (viewport == null || viewport.Width <= 2 || viewport.Height <= 2)
                    return false;

                // 与视口无交集，直接返回
                var visibleLeft = Math.Max(0, box.X);
                var visibleTop = Math.Max(0, box.Y);
                var visibleRight = Math.Min(viewport.Width, box.X + box.Width);
                var visibleBottom = Math.Min(viewport.Height, box.Y + box.Height);

                var visibleWidth = visibleRight - visibleLeft;
                var visibleHeight = visibleBottom - visibleTop;

                if (visibleWidth <= 2 || visibleHeight <= 2)
                    return false;

                var rnd = Random.Shared;

                // 在可见区域中间偏内侧随机取点
                double x = visibleLeft + visibleWidth * CommonHelper.NextDouble(
                    insetPercentMin / 100.0,
                    insetPercentMax / 100.0);

                double y = visibleTop + visibleHeight * CommonHelper.NextDouble(
                    insetPercentMin / 100.0,
                    insetPercentMax / 100.0);

                // 保底限制在视口内
                x = Math.Clamp(x, 1, viewport.Width - 1);
                y = Math.Clamp(y, 1, viewport.Height - 1);

                // 轻微移动，模拟真人 tap
                double moveX = x + rnd.Next(-2, 3);
                double moveY = y + rnd.Next(-2, 3);

                moveX = Math.Clamp(moveX, 1, viewport.Width - 1);
                moveY = Math.Clamp(moveY, 1, viewport.Height - 1);

                await client.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
                {
                    ["type"] = "touchStart",
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

                await Task.Delay(rnd.Next(minHoldMs, maxHoldMs + 1), cancellationToken);

                if (useTinyMove)
                {
                    await client.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
                    {
                        ["type"] = "touchMove",
                        ["touchPoints"] = new object[]
                        {
                            new Dictionary<string, object>
                            {
                                ["x"] = moveX,
                                ["y"] = moveY,
                                ["radiusX"] = 2,
                                ["radiusY"] = 2,
                                ["force"] = 1,
                                ["id"] = 0
                            }
                        },
                        ["modifiers"] = 0
                    });

                    await Task.Delay(rnd.Next(minMoveDelayMs, maxMoveDelayMs + 1), cancellationToken);
                }

                await client.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
                {
                    ["type"] = "touchEnd",
                    ["touchPoints"] = Array.Empty<object>(),
                    ["modifiers"] = 0
                });

                await Task.Delay(rnd.Next(minPostDelayMs, maxPostDelayMs + 1), cancellationToken);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }




        #region 陀螺仪操作

        /// <summary>
        /// 修改浏览器的方向(陀螺仪)
        /// </summary>
        /// <param name="driver">Chrome驱动</param>
        /// <param name="alpha">在围绕z轴旋转时（即左右旋转时),y轴的度数差；是一个介于0到360之间的浮点数</param>
        /// <param name="beta">在围绕x轴旋转时（即前后旋转时）,z轴的度数差；是一个介于-180到180之间的浮点数</param>
        /// <param name="gamma">在围绕y轴旋转时（即扭转设备时)，z轴的度数差；是一个介于-90到90之间的浮点数</param>
        public static async Task ChangeDeviceOrientationAsync(ICDPSession cdpSession, int alpha, int beta, int gamma)
        {
            var args = new System.Collections.Generic.Dictionary<string, object>();
            args.Add("alpha", alpha);
            args.Add("beta", beta);
            args.Add("gamma", gamma);
            await cdpSession.SendAsync("DeviceOrientation.setDeviceOrientationOverride", args);
        }

        /// <summary>
        /// 随机修改浏览器的方向(陀螺仪)
        /// </summary>
        /// <param name="driver">Chrome驱动</param>
        public static async Task ChangeDeviceOrientationAsync(ICDPSession cdpSession)
        {
            try
            {

                var args = new System.Collections.Generic.Dictionary<string, object>();
                args.Add("alpha", new Random().Next(0, 10));
                args.Add("beta", new Random().Next(10, 75));
                args.Add("gamma", new Random().Next(-30, 30));
                await cdpSession.SendAsync("DeviceOrientation.setDeviceOrientationOverride", args);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

        }



        public static async Task ClearDeviceOrientationOverrideAsync(ICDPSession cdpSession)
        {
            try
            {

                var args = new System.Collections.Generic.Dictionary<string, object>();
                args.Add("alpha", 0);
                args.Add("beta", 90);
                args.Add("gamma", 0);
                await cdpSession.SendAsync("DeviceOrientation.clearDeviceOrientationOverride", args);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

        }
        #endregion 陀螺仪操作


    }
}
