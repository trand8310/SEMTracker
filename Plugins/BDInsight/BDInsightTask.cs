using BDInsight;
using BDInsight.LandingPolicy;
using BDInsight.Models;
using BDInsight.Swiper;
using Microsoft.Playwright;
using Newtonsoft.Json.Linq;
using SEM.Infrastructure;
using SEM.Models;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;
using System.Threading;




namespace SEM.Plugins
{
    public sealed class BDInsightTask : QTPServiceBase
    {

        public static SEMPlugin GetInfo()
        {
            return new SEMPlugin()
            {
                ClassName = "SEM.Plugins.BDInsightTask",
                Name = "BDInsight",
                FileName = "BDInsight.dll",
            };
        }
        private readonly ConcurrentDictionary<string, WorkerRunContext> _activeContexts = new();
        private int _disposeStarted;
        public override string Title => "百度搜索";
        private readonly TaskStatsAggregator _aggregator;
        private readonly AdeHelper _adeHelper;
        private ChineseNameGenerator _nameGenerator;
        private readonly IRootDomainService _domainService;
        private readonly IPlaywrightProvider _playwrightProvider;

        /// <summary>
        /// BDInsightTask
        /// </summary>
        /// <param name="domainService"></param>
        /// <param name="playwrightProvider"></param>
        /// <param name="aggregator"></param>
        /// <param name="adeHelper"></param>
        /// <param name="nameGenerator"></param>
        /// <param name="appSettings"></param>
        public BDInsightTask(
            IRootDomainService domainService,
            IPlaywrightProvider playwrightProvider,
            TaskStatsAggregator aggregator,
            AdeHelper adeHelper,
            ChineseNameGenerator nameGenerator,
            AppSettings appSettings) : base(appSettings)
        {
            _domainService = domainService;
            _playwrightProvider = playwrightProvider;
            _aggregator = aggregator;
            _adeHelper = adeHelper;
            _nameGenerator = nameGenerator;
        }

        /// <summary>
        /// 随机生成一个满足百分比的数字
        /// </summary>
        /// <param name="probability"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public static bool IsEventOccurring(double probability)
        {
            if (probability < 0 || probability > 1)
                throw new ArgumentOutOfRangeException(nameof(probability), "Probability must be between 0 and 1");
            double randomValue = Random.Shared.NextDouble();
            return randomValue < probability;
        }

        public static Task<bool> IsPageTop(IPage page)
        {
            return page.EvaluateAsync<bool>("window.pageYOffset == 0;");
        }

        public static Task<bool> IsPageEnd(IPage page)
        {
            return page.EvaluateAsync<bool>("(window.innerHeight + window.pageYOffset) >= document.body.offsetHeight || Math.abs((window.innerHeight + window.pageYOffset) - document.body.offsetHeight) < 10;");
        }
        private static List<string> InitFPArgs(JToken taskArgs, int maxTouchPoints)
        {
            var result = new List<string>();
            uint fingerprint = 0;
            if (taskArgs.SelectToken("dev.fingerprint") != null)
            {
                fingerprint = taskArgs.SelectToken("dev.fingerprint").Value<uint>();
            }
            else
            {
                fingerprint = CommonHelper.RandomNumber();
            }



            #region 指纹参数设置

            /*
            --platform="Android" 
            --platform-version="14" 
            --full-version="123.0.6261.171" 
            --brand="XiaoMiBrowser" 
            --brand-version="19.1.40221" 
            --product-model="23227RK66C" 
            --fingerprint=2113123  
            --time-zone=Asia/Shanghai
            --webrtc-ip=62.210.125.97 
            --webgl-vendor="Qualcomm" 
            --webgl-renderer="Adreno (TM) 610"  
            --max-touch-points=5  
            --hardware-concurrency=8 
            --device-memory=8 
            --device-pixel-ratio=2.625 
            --screen-size=412,915 
            --screen-avail-size=412,915 
            --enable-rects-noise 
            --enable-image-noise 
            --enable-text-noise 
            --enable-font-noise
            --enable-audio-noise 
            --disable-pdf-viewer
            --touch-emulator-point=0,82
            --netinfo-type=wifi
            --netinfo-effective=4g
            --netinfo-rtt=0
            --enable-battery-charging
            --battery-level=1.0
            --battery-charging-time=0
            --battery-discharging-time=0
            --cookie-enabled
             */
            #endregion

            var gpu = taskArgs.SelectToken("dev.gpu").Value<string>();
            var vendor = taskArgs.SelectToken("dev.vendor").Value<string>();
            var useragent = taskArgs.SelectToken("dev.ua").Value<string>();

            var os = taskArgs.SelectToken("os").Value<int>();

            if (os == 2)
            {
                result.Add("--platform=\"iOS\"");
                result.Add("--screen-color-depth=32");
            }
            else if (os == 7)
            {
                result.Add("--platform=\"Windows\"");
            }
            else
            {
                result.Add("--platform=\"Android\"");
            }


            var full_version = taskArgs.SelectToken("dev.full_version").Value<string>();
            var full_version_values = full_version.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);

            result.Add($"--platform-version=\"{taskArgs.SelectToken("dev.osv").Value<string>()}\"");
            result.Add($"--full-version={full_version}");

            if (!string.IsNullOrWhiteSpace(taskArgs.SelectToken("dev.brand")?.Value<string>()))
            {
                var brand = taskArgs.SelectToken("dev.brand")?.Value<string>();

                result.Add($"--brand=\"{brand}\"");
                result.Add($"--brand-name=\"{brand}\"");
                if (!string.IsNullOrWhiteSpace(taskArgs.SelectToken("dev.brand_version")?.Value<string>()))
                    result.Add($"--brand-version=\"{taskArgs.SelectToken("dev.brand_version")?.Value<string>()}\"");

                if (new bool[] { false, false, true, false, false, true, false, false, true, false }[CommonHelper.RandomRange(0, 10)])
                {
                    result.Add($"--disable-full-version-list");
                    if (new bool[] { false, false, true, false, false, true, false, false, true, false }[CommonHelper.RandomRange(0, 10)])
                    {
                        result.Add($"--disable-brand-version-list");
                    }
                }


                if (os == 1 || os == 2)
                {
                    var make = taskArgs.SelectToken("dev.make")?.Value<string>().ToLower();
                    if (!string.IsNullOrWhiteSpace(make))
                    {
                        if (make.Contains("xiaomi"))
                        {
                            result.Add($"--def-fontname=\"MiSans\"");
                        }
                        else if (make.ToLower().Contains("vivo"))
                        {
                            result.Add($"--def-fontname=\"vivo Sans\"");
                        }
                        else if (make.ToLower().Contains("oppo"))
                        {
                            result.Add($"--def-fontname=\"OPPO Sans 4.0\"");
                        }
                        else if (make.ToLower().Contains("huawei"))
                        {
                            result.Add($"--def-fontname=\"HarmonyOS Sans\"");
                        }
                    }
                }
                //def-fontname
            }

            if (os == 1 || os == 2)
            {
                if (!string.IsNullOrWhiteSpace(taskArgs.SelectToken("dev.model")?.Value<string>()))
                {
                    if (os == 1)
                    {
                        result.Add($"--product-model=\"{taskArgs.SelectToken("dev.model")?.Value<string>()}\"");
                    }
                }
            }

            result.Add($"--fingerprint={fingerprint}");
            var grease_cipher = Math.Abs(string.Join(".", full_version_values.Take(2)).GetHashCode()) % 65535;
            result.Add($"--ssl-grease-cipher={grease_cipher}");
            if (os == 1 || os == 2)
            {
                result.Add($"--netinfo-type={new string[] { "wifi", "cellular" }[CommonHelper.RandomRange(0, 2)]}");
                result.Add($"--netinfo-effective=4g");
                result.Add($"--netinfo-rtt={CommonHelper.RandomRange(0, 500)}");
            }

            result.Add($"--force-webrtc-ip-handling-policy");
            var isProxyMode = taskArgs.SelectToken("isProxyMode")?.Value<bool>() ?? false;
            if (isProxyMode)
            {
                var realIp = taskArgs.SelectToken("realIp")?.Value<string>() ?? taskArgs.SelectToken("ipInfo.query")?.Value<string>();
                if (!string.IsNullOrWhiteSpace(realIp))
                {
                    result.Add($"--webrtc-ip={realIp}");
                    if (new bool[] { false, false, true, false, false, true, false, false, true, false }[CommonHelper.RandomRange(0, 10)])
                    {
                        result.Add($"--webrtc-ip-handling-policy=disable_non_proxied_udp");
                    }
                    else
                    {
                        result.Add($"--webrtc-ip-handling-policy=default_public_interface_only");
                    }
                }
                else
                {
                    result.Add($"--webrtc-ip-handling-policy=disable_non_proxied_udp");
                }
            }
            else
            {
                result.Add($"--webrtc-ip-handling-policy=disable_non_proxied_udp");
            }

            var dev_hash = Math.Abs(taskArgs.SelectToken("dev").ToString().GetHashCode());
            #region webgl
            result.Add($"--webgl-vendor=\"{vendor}\"");
            result.Add($"--webgl-renderer=\"{gpu}\"");


            Random rand = new Random(Math.Abs($"{vendor}{gpu}".ToLower().GetHashCode()));

            var webgl_extensions = new string[] {
                "EXT_clip_control|WEBGL_stencil_texturing|WEBGL_provoking_vertex|WEBGL_polygon_mode|WEBGL_multi_draw|WEBGL_lose_context|WEBGL_debug_shaders|WEBGL_debug_renderer_info|WEBGL_compressed_texture_s3tc_srgb|WEBGL_compressed_texture_s3tc|WEBGL_clip_cull_distance|WEBGL_blend_func_extended|OVR_multiview2|OES_texture_float_linear|OES_shader_multisample_interpolation|OES_sample_variables|OES_draw_buffers_indexed|NV_shader_noperspective_interpolation|KHR_parallel_shader_compile|EXT_texture_norm16|EXT_texture_mirror_clamp_to_edge|EXT_texture_filter_anisotropic|EXT_texture_compression_rgtc|EXT_texture_compression_bptc|EXT_render_snorm|EXT_polygon_offset_clamp|EXT_float_blend|EXT_disjoint_timer_query_webgl2|EXT_depth_clamp|EXT_conservative_depth|EXT_color_buffer_half_float|EXT_color_buffer_float",
                "EXT_clip_control|WEBGL_stencil_texturing|WEBGL_polygon_mode|WEBGL_multi_draw|WEBGL_lose_context|WEBGL_debug_shaders|WEBGL_debug_renderer_info|WEBGL_compressed_texture_s3tc_srgb|WEBGL_compressed_texture_s3tc|WEBGL_compressed_texture_etc1|WEBGL_compressed_texture_etc|WEBGL_compressed_texture_astc|WEBGL_clip_cull_distance|OVR_multiview2|OES_texture_float_linear|OES_shader_multisample_interpolation|OES_sample_variables|OES_draw_buffers_indexed|NV_shader_noperspective_interpolation|EXT_texture_mirror_clamp_to_edge|EXT_texture_filter_anisotropic|EXT_texture_compression_rgtc|EXT_texture_compression_bptc|EXT_polygon_offset_clamp|EXT_float_blend|EXT_depth_clamp|EXT_conservative_depth|EXT_color_buffer_half_float|EXT_color_buffer_float",
                "EXT_color_buffer_float|WEBGL_multi_draw|WEBGL_lose_context|WEBGL_debug_shaders|WEBGL_debug_renderer_info|WEBGL_compressed_texture_s3tc_srgb|WEBGL_compressed_texture_s3tc|WEBGL_compressed_texture_etc1|WEBGL_compressed_texture_etc|WEBGL_compressed_texture_astc|OES_texture_float_linear|EXT_texture_norm16|EXT_texture_filter_anisotropic|EXT_texture_compression_rgtc|EXT_texture_compression_bptc|EXT_float_blend|EXT_color_buffer_half_float",
            };
            var webgl_extension_text = string.Join("|", webgl_extensions[rand.Next(0, 3)].Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries).OrderBy(o => Guid.NewGuid()));
            result.Add($"--webgl-extensions=\"{webgl_extension_text}\"");
            var webgl_vertex_shaders = new string[] {
                "32,256,16,31,1024,14,128,124,128,4,4,1-8,1-1023",
                "32,512,16,31,2048,36,128,124,64,4,4,1-4095.9375,1-1024",
                "32,1024,16,31,4096,48,128,124,64,4,4,1-16.7456,1-1024",
                "32,2048,16,31,8192,36,128,124,64,4,4,1-32.1247,1-1024",
                "32,4096,16,31,16384,48,128,124,64,4,4,1-1024.6631,1-1024",
                "32,8192,16,31,1024,36,128,124,64,4,4,1-2048.4475,1-1024",
            };

            result.Add($"--webgl-vertex-shader=\"{webgl_vertex_shaders[rand.Next(0, 6)]}\"");
            var webgl_fragment_shaders = new string[] {
                "256,16,1024,14,128,-8,7",
                "512,16,2048,36,124,-8,7",
                "1024,16,4096,36,124,-8,7",
                "2048,16,16384,36,124,-8,7",
                "4096,16,8192,36,124,-8,7",
                "8192,16,16384,36,124,-8,7",
            };

            result.Add($"--webgl-fragment-shader=\"{webgl_fragment_shaders[rand.Next(0, 6)]}\"");
            var webgl_frame_buffers = new string[] {
                "8,8,4,,16384,16384-16384,8,8,8,8,24,0",
                "8,8,4,,16383,16383-16383,8,8,8,8,24,0",
                "8,8,4,,65535,65535-65535,8,8,8,8,24,0"
            };
            result.Add($"--webgl-frame-buffer=\"{webgl_frame_buffers[rand.Next(0, 3)]}\"");


            var webgl_textures = new string[] {
                "4096,4096,96,16,2048,2048,16",
                "4096,4096,96,16,16383,4096,256",
                "4096,4096,96,16,8192,4096,128",
                "4096,4096,96,16,4096,4096,64",
                "4096,4096,96,16,16383,4096,128",
                "4096,4096,96,16,2048,4096,128",
                "4096,4096,96,16,2048,4096,256",
            };

            result.Add($"--webgl-textures=\"{webgl_textures[rand.Next(0, 7)]}\"");
            var webgl_uniform_buffers = new string[] {
                "84,65536,32,84,230400,230400",
                "24,65536,256,24,212988,200704",
                "48,65536,256,24,212988,200704",
                "96,65536,256,24,212988,200704",
                "192,65536,256,24,212988,200704",
                "216,65536,16,216,606208,626028",
                "512,65536,16,216,606208,626028",
            };
            result.Add($"--webgl-uniform-buffer=\"{webgl_uniform_buffers[rand.Next(0, 7)]}\"");
            #endregion

            result.Add($"--hardware-concurrency={(taskArgs.SelectToken("dev.cpu")?.Value<int>() ?? 8)}");

            var ram = taskArgs.SelectToken("dev.ram").Value<string>().Split(',', StringSplitOptions.RemoveEmptyEntries);
            int deviceMemory = Convert.ToInt32(ram[CommonHelper.RandomRange(0, ram.Length)].Trim());

            if (deviceMemory < 4)
                deviceMemory = 4;
            else if (deviceMemory > 8)
                deviceMemory = 8;

            result.Add($"--device-memory={(deviceMemory > 8 ? 8 : deviceMemory)}");

            var js_memory_info = new string[] { "10000000|10000000|1136000000", "29400000|31200000|1130000000", "10000000|10000000|1136000000", "29400000|31200000|1130000000", "29400000|31200000|1130000000" };
            result.Add($"--js-memory-info=\"{js_memory_info[(dev_hash % 4)]}\"");
            if (os == 1 || os == 2)
            {
                result.Add($"--max-touch-points={maxTouchPoints}");
            }

            //--storage
            //268435456
            //2147483648
            //69250036530
            var storage = taskArgs.SelectToken("dev.storage").Value<long>() * 1024 * 1024 * 1024;
            var usage_storage = (long)Math.Ceiling(storage * (CommonHelper.RandomRange(30, 80) * 0.01));


            result.Add($"--storage-quota=\"0|{(storage - usage_storage)}\"");

            result.Add("--enable-rects-noise");
            result.Add("--enable-canvas-noise");
            result.Add("--enable-image-noise");
            result.Add("--enable-text-noise");
            result.Add("--enable-font-noise");
            result.Add("--enable-audio-noise");

            if (new bool[] { false, false, true, false, false, true, false, false, true, false }[CommonHelper.RandomRange(0, 10)])
            {
                result.Add("--disable-pdf-viewer");
            }

            if (new bool[] { true, true, false, true, true, false, true, true, false, true }[CommonHelper.RandomRange(0, 10)])
            {
                result.Add("--disable-geolocation");
            }


            if (os == 1 || os == 2)
            {

                {
                    var touch_ix = CommonHelper.NextInt(0, 2);
                    var touch_iy = CommonHelper.NextInt(80, 100);
                    if (new bool[] { true, true, false, true, true, false, true, true, false, true }[CommonHelper.RandomRange(0, 10)])
                    {
                        string combined_x = $"{fingerprint}_touch_offset_x";
                        string combined_y = $"{fingerprint}_touch_offset_y";
                        uint hash_val_x = CommonHelper.GetStableHash(combined_x);
                        uint hash_val_y = CommonHelper.GetStableHash(combined_y);
                        double norm_x = (hash_val_x / 4294967295.0) - 0.5;
                        double norm_y = (hash_val_y / 4294967295.0) - 0.5;
                        double noise_factor_x_ = norm_x * 0.2;
                        double noise_factor_y_ = norm_y * 0.2;
                        var touch_dx = touch_ix * (1 + noise_factor_x_);
                        var touch_dy = touch_iy * (1 + noise_factor_y_);
                        result.Add($"--touch-emulator-point=\"{touch_dx},{touch_dy}\"");
                    }
                    else
                    {
                        result.Add($"--touch-emulator-point=\"{touch_ix},{touch_iy}\"");
                    }
                }


                int level = CommonHelper.RandomRange(10, 101);
                if (new bool[] { false, false, true, false, false, true, false, false, true, false }[CommonHelper.RandomRange(0, 10)])
                {
                    result.Add($"--enable-battery-charging=1");
                    result.Add($"--battery-level={Convert.ToDecimal((level * 0.01).ToString("f2"))}");
                    result.Add($"--battery-charging-time=0");
                    result.Add($"--battery-discharging-time=0");
                }
                else
                {
                    result.Add($"--enable-battery-charging=0");
                    result.Add($"--battery-level={Convert.ToDecimal((level * 0.01).ToString("f2"))}");
                    result.Add($"--battery-charging-time=0");
                    result.Add($"--battery-discharging-time=0");
                }
            }

            return result;
        }
        private static readonly TimeSpan CleanupStepTimeout = TimeSpan.FromSeconds(8);

        private async Task RunCleanupStepAsync(string uniqueId, string stepName, Func<Task> cleanupAction)
        {
            var sw = Stopwatch.StartNew();
            Task cleanupTask;
            try
            {
                cleanupTask = cleanupAction();
            }
            catch (Exception ex)
            {
                LogWriteLine($"{this.Title}:ForceCleanup:{uniqueId}:{stepName} 启动异常: {ex.GetType().Name}: {ex.Message}");
                return;
            }

            var timeoutTask = Task.Delay(CleanupStepTimeout);
            if (await Task.WhenAny(cleanupTask, timeoutTask) != cleanupTask)
            {
                LogWriteLine($"{this.Title}:ForceCleanup:{uniqueId}:{stepName} 超过 {CleanupStepTimeout.TotalSeconds:N0}s 仍未完成，跳过等待，避免清理流程卡死");
                _ = cleanupTask.ContinueWith(t =>
                {
                    LogWriteLine($"{this.Title}:ForceCleanup:{uniqueId}:{stepName} 超时后最终异常: {t.Exception?.GetBaseException().GetType().Name}: {t.Exception?.GetBaseException().Message}");
                }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                return;
            }

            try
            {
                await cleanupTask;
                sw.Stop();
                if (sw.Elapsed > TimeSpan.FromSeconds(1))
                    LogWriteLine($"{this.Title}:ForceCleanup:{uniqueId}:{stepName} 完成，耗时 {sw.Elapsed.TotalSeconds:N2}s");
            }
            catch (Exception ex)
            {
                sw.Stop();
                LogWriteLine($"{this.Title}:ForceCleanup:{uniqueId}:{stepName} 异常，耗时 {sw.Elapsed.TotalSeconds:N2}s: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private async Task ForceCleanupSessionAsync(WorkerRunContext ctx, string uniqueId)
        {
            await ctx.CleanupLock.WaitAsync();
            try
            {
                // Playwright 推荐的释放顺序：
                // 1. 先释放/分离我们额外创建的 CDP session，避免后续关闭页面时还有 CDP 监听或命令挂着。
                // 2. 再关闭显式创建的 BrowserContext，让页面 close 事件、HAR/video 等上下文产物有机会正常落盘。
                // 3. 最后关闭 Browser；Playwright 文档说明 Browser.CloseAsync 更接近强制退出浏览器，且调用后 Browser 不可再用。
                // IPlaywright 由 PlaywrightProvider 单例统一持有，不能在单个 worker 清理时 Dispose，否则会影响其它 worker。
                var cdpManager = ctx.CdpManager;
                if (cdpManager != null)
                    await RunCleanupStepAsync(uniqueId, "CDP.DetachAsync", () => cdpManager.DisposeAsync().AsTask());

                var browserContext = ctx.Context;
                if (browserContext != null)
                    await RunCleanupStepAsync(uniqueId, "Context.CloseAsync", () => browserContext.CloseAsync());

                var browser = ctx.Browser;
                if (browser != null)
                {
                    if (browser.IsConnected)
                    {
                        await RunCleanupStepAsync(uniqueId, "Browser.CloseAsync", () => browser.CloseAsync(new BrowserCloseOptions
                        {
                            Reason = $"ForceCleanupSession:{uniqueId}"
                        }));
                    }
                    else
                    {
                        LogWriteLine($"{this.Title}:ForceCleanup:{uniqueId}:Browser.CloseAsync 跳过，Browser 已断开连接");
                    }
                }

                ctx.CdpManager = null;
                ctx.Context = null;
                ctx.Browser = null;
                ctx.Page = null;
            }
            finally
            {
                ctx.CleanupLock.Release();
            }
        }

        public override async Task ForceStopWorkerAsync(string uniqueId, string reason, CancellationToken token = default)
        {
            if (!_activeContexts.TryGetValue(uniqueId, out var ctx))
                return;

            LogWriteLine($"{this.Title}:ExecuteWorker:ForceStop: {reason}");

            try
            {
                if (!ctx.Config.LinkedCts.IsCancellationRequested)
                    await ctx.Config.LinkedCts.CancelAsync();
            }
            catch
            {
            }

            token.ThrowIfCancellationRequested();
            await ForceCleanupSessionAsync(ctx, uniqueId);
        }

        public override async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) == 1)
                return;

            try
            {
                foreach (var pair in _activeContexts.ToArray())
                {
                    var activeUniqueId = pair.Key;
                    var activeContext = pair.Value;

                    try
                    {
                        if (!activeContext.Config.LinkedCts.IsCancellationRequested)
                            await activeContext.Config.LinkedCts.CancelAsync();
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"{this.Title}:DisposeAsync:{activeUniqueId}:取消任务异常: {ex.GetType().Name}: {ex.Message}");
                    }

                    try
                    {
                        await ForceCleanupSessionAsync(activeContext, activeUniqueId);
                    }
                    finally
                    {
                        _activeContexts.TryRemove(activeUniqueId, out _);
                    }
                }
            }
            finally
            {
                await base.DisposeAsync();
            }
        }
        private TaskConfig BuildTaskConfig(string uniqueId, JObject taskArgs, CancellationTokenSource linkedCts)
        {

            var os = taskArgs.SelectToken("os")!.Value<int>();
            var sw1 = taskArgs.SelectToken("dev.sw")?.Value<int>() ?? 1080;
            var sh1 = taskArgs.SelectToken("dev.sh")?.Value<int>() ?? 1920;
            float deviceScale = 1.0f;
            int sw = 0;
            int sh = 0;
            if (os == 1 || os == 2)
            {
                var profileResult = AndroidViewportMatcher.Match(sw1, sh1);
                deviceScale = profileResult.DeviceScaleFactor;
                sw = profileResult.CssWidth;
                sh = profileResult.CssHeight;

            }
            else
            {
                var profileResult = WindowsViewportMatcher.Match(sw1, sh1);
                deviceScale = profileResult.DeviceScaleFactor;
                sw = profileResult.CssWidth;
                sh = profileResult.CssHeight;
            }
            var maxTouchPoints = os == 1 || os == 2 ? CommonHelper.RandomRange(4, 6) : 0;
            var kernelVersion = taskArgs.SelectToken("kernelVersion")?.Value<string>() ?? _appSettings.KernelVersion;
            var processIndex = taskArgs.SelectToken("processIndex")?.Value<int>() ?? 1;
            var cacheName = taskArgs.SelectToken("cacheName")!.Value<string>();
            var isProxyMode = taskArgs.SelectToken("isProxyMode")?.Value<bool>() ?? false;
            var proxy_server = taskArgs.SelectToken("proxy_server")?.Value<string>();
            return new TaskConfig
            {
                UniqueId = uniqueId,
                TaskArgs = taskArgs,
                LinkedCts = linkedCts,
                TaskId = taskArgs.SelectToken("task.id")!.Value<int>(),
                TaskUrl = taskArgs.SelectToken("task.url")!.Value<string>(),
                IsProxyMode = isProxyMode,
                ProxyServer = proxy_server ?? "",
                SleepMs = ParseSleepMilliseconds(taskArgs),
                IsLocalAdWord = taskArgs.SelectToken("isLocalAdWord")?.Value<bool>() ?? false,
                PageLoadingTimeoutMs = taskArgs.SelectToken("pageLoadingTimeout")?.Value<int>() * 1000 ?? 30000,
                PageLoadedDelayMs = ParsePageLoadedDelayMilliseconds(taskArgs),
                UserAgent = taskArgs.SelectToken("dev.ua")!.Value<string>(),
                Os = os,
                DeviceScale = deviceScale,
                Sw = sw,
                Sh = sh,
                PvsTriggerOne = taskArgs.SelectToken("pvsTriggerOne")?.Value<bool>() ?? true,
                CurrentUV = taskArgs.SelectToken("currentUV")?.Value<int>() ?? 0,
                KernelVersion = kernelVersion,
                MaxTouchPoints = maxTouchPoints,
                ProcessIndex = processIndex,
                IsTest = taskArgs.SelectToken("isTest")?.Value<bool>() ?? false,
                TotalPV = taskArgs.SelectToken("totalPV")?.Value<int>() ?? 1,
                CacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp", "Chrome", kernelVersion, "User_Cache", cacheName),
                UserDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp", "Chrome", kernelVersion, "User_Data", $"{processIndex}_{Guid.NewGuid():n}")
            };
        }
        /// <summary>
        /// 解析Sleep值
        /// </summary>
        /// <param name="taskArgs"></param>
        /// <param name="defaultMinMs"></param>
        /// <param name="defaultMaxMs"></param>
        /// <returns></returns>
        private static int ParseSleepMilliseconds(JToken taskArgs, int defaultMinMs = 8000, int defaultMaxMs = 15000)
        {
            var sleep = CommonHelper.RandomRange(defaultMinMs, defaultMaxMs);
            var sleepText = taskArgs.SelectToken("task.sleep")?.Value<string>()?.Trim();
            if (string.IsNullOrWhiteSpace(sleepText))
                return sleep;
            if (sleepText.Contains('-'))
            {
                var parts = sleepText
                    .Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out var minSeconds) &&
                    int.TryParse(parts[1], out var maxSeconds) &&
                    minSeconds >= 0 &&
                    maxSeconds >= 0)
                {
                    if (minSeconds > maxSeconds)
                        (minSeconds, maxSeconds) = (maxSeconds, minSeconds);

                    return CommonHelper.RandomRange(minSeconds * 1000, maxSeconds * 1000);
                }
            }
            else if (int.TryParse(sleepText, out var seconds) && seconds >= 0)
            {
                return seconds * 1000;
            }

            return sleep;
        }
        private int ParsePageLoadedDelayMilliseconds(JObject taskArgs)
        {
            var pageloadedDelay = CommonHelper.RandomRange(8000, 15000);
            var token = taskArgs.SelectToken("pageloadedDelay");
            if (token == null)
                return pageloadedDelay;

            var str = token.Value<string>();
            if (string.IsNullOrWhiteSpace(str))
                return pageloadedDelay;

            if (str.Contains("-"))
            {
                var values = str.Split('-', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => Convert.ToInt32(s))
                    .ToArray();

                if (values.Length == 2)
                    return CommonHelper.RandomRange(values[0] * 1000, values[1] * 1000);
            }

            if (int.TryParse(str, out var v))
                return v * 1000;

            return pageloadedDelay;
        }
        private List<string> BuildChromiumArgs(TaskConfig config)
        {
            var args = new List<string>
            {



                //"--disable-extensions",
                //"--disable-default-apps",
                //"--no-first-run",
                //"--no-default-browser-check",
                //"--disable-component-update",
                //"--disable-background-networking",
                //"--metrics-recording-only",
                //"--disable-client-side-phishing-detection",
                //"--disable-popup-blocking",
                //"--disable-infobars",
                //"--use-mock-keychain",
                //"--no-service-autorun",
                //"--force-color-profile=srgb",
                //"--disable-features=LensOverlay,Translate",
                //"--virtual-clipboard",
                //"--touch-events=enabled",
                "--disable-logging",
                "--use-fake-ui-for-media-stream",
                "--use-fake-device-for-media-stream",
                "--show-avatar-button=never",
                "--disable-http2-grease-settings",
                "--hide-bad-flags",
                "--hide-crashed-bubble",
                $"--user-agent=\"{config.UserAgent}\"",
                $"--window-size=\"{config.Sw + 16},{config.Sh + 96}\"",
                "--window-position=0,0",
                //$"--device-pixel-ratio={config.DeviceScale}",
                //$"--screen-size=\"{config.Sw},{config.Sh}\"",
               // $"--screen-avail-size=\"{config.Sw},{config.Sh}\"",
            };

            if (config.Os == 1 || config.Os == 2)
            {

            }

            //proxyServer = string.Empty;
            //var isProxyMode = config.TaskArgs.SelectToken("isProxyMode")?.Value<bool>() ?? false;
            //if (isProxyMode)
            //{
            //    proxyServer = config.TaskArgs.SelectToken("proxy_server")!.Value<string>();
            //    args.Add($"--proxy-server=\"{proxyServer}\"");
            //}

            ////if (config.TaskArgs.SelectToken("isHiddenMode")?.Value<bool>() ?? false)
            ////    args.Add("--headless");

            if (config.TaskArgs.SelectToken("incognito")?.Value<bool>() ?? false)
            {
                //args.Add("--incognito");
                //args.Add("--enable-incognito-themes");
            }
            else
            {
                //args.Add($"--disk-cache-dir=\"{config.CacheDir}\"");
            }

            args.AddRange(InitFPArgs(config.TaskArgs, config.MaxTouchPoints));
            return args;
        }
        private async Task<IBrowser?> LaunchBrowserAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var args = BuildChromiumArgs(ctx.Config);
            var chromePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "File", "chrome-win", ctx.Config.KernelVersion, "chrome.exe");
            Exception? lastException = null;
            const int maxAttempts = 3;
            var proxyServer = string.Empty;
            var headless = ctx.Config.TaskArgs.SelectToken("isHiddenMode")?.Value<bool>() ?? false;
            var isProxyMode = ctx.Config.TaskArgs.SelectToken("isProxyMode")?.Value<bool>() ?? false;
            if (isProxyMode)
            {
                proxyServer = ctx.Config.TaskArgs.SelectToken("proxy_server")!.Value<string>();
            }

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                token.ThrowIfCancellationRequested();
                IBrowser? browser = null;
                try
                {
                    var launchOptions = new BrowserTypeLaunchOptions
                    {
                        Headless = headless,
                        Channel = "chrome",
                        ChromiumSandbox = false,
                        IgnoreDefaultArgs = new List<string>()
                        {
                            "--enable-automation",
                        },
                        ExecutablePath = chromePath,
                        Args = args,
                    };
                    if (isProxyMode)
                    {
                        launchOptions.Proxy = new Proxy { Server = proxyServer! };
                    }

                    browser = await ctx.Playwright!.Chromium.LaunchAsync(launchOptions);

                    if (browser == null)
                        throw new InvalidOperationException("LaunchAsync returned null browser.");
                    if (!browser.IsConnected)
                        throw new InvalidOperationException("Browser is not connected after LaunchAsync.");

                    // LaunchAsync 不会自动创建 BrowserContext，这里主动创建一个，保持后续流程不变。

                    BrowserNewContextOptions contextOptions = new()
                    {
                        UserAgent = ctx.Config.UserAgent,
                        ViewportSize = new ViewportSize() { Width = ctx.Config.Sw, Height = ctx.Config.Sh },
                        DeviceScaleFactor = ctx.Config.DeviceScale,
                        HasTouch = true,
                        IsMobile = true,
                        //Locale = envLocale,
                        //TimezoneId = taskArgs.SelectToken("ipInfo.timezone")?.Value<string>() ?? envTimezoneId,
                        IgnoreHTTPSErrors = true,
                    };

                    ctx.Context = await browser.NewContextAsync(contextOptions);
                    if (ctx.Context == null)
                        throw new InvalidOperationException("NewContextAsync returned null context.");
                    return browser;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    if (browser != null)
                    {
                        try
                        {
                            await browser.CloseAsync();
                        }
                        catch
                        {
                        }
                    }

                    if (attempt < maxAttempts)
                    {
                        await Task.Delay(200, token);
                    }
                }
            }

            if (lastException != null)
            {
                LogWriteLine($"LaunchBrowserAsync 最终失败: {lastException.Message}");
            }
            return null;
        }
        private async Task ConfigureContextAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (ctx.Context == null)
                throw new InvalidOperationException("Context is null.");
            if (ctx.Config.TaskArgs.SelectToken("ipInfo.lon") != null &&
                ctx.Config.TaskArgs.SelectToken("ipInfo.lat") != null)
            {
                await ctx.Context.SetGeolocationAsync(new Geolocation
                {
                    Latitude = ctx.Config.TaskArgs.SelectToken("ipInfo.lat")!.Value<float>(),
                    Longitude = ctx.Config.TaskArgs.SelectToken("ipInfo.lon")!.Value<float>()
                });
            }
            if (ctx.Context.Pages == null || ctx.Context.Pages.Count == 0)
            {
                ctx.Page = await ctx.Context.NewPageAsync();
            }
            else
            {
                ctx.Page = ctx.Context.Pages[0];
            }
            if (ctx.Page == null)
                throw new InvalidOperationException("Failed to initialize page for context.");
            await InitPageAsync(ctx, ctx.Page, token);
        }
        private async Task InitPageAsync(WorkerRunContext ctx, IPage page, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            await page.SetViewportSizeAsync(ctx.Config.Sw, ctx.Config.Sh);
            var cdpSession = await ctx.CdpManager!.GetOrCreateSessionAsync(page);
            await cdpSession.SendAsync("Page.enable");
            cdpSession.Event("Page.downloadWillBegin").OnEvent += (_, _) =>
            {

            };
            if (ctx.Config.Os == 1 || ctx.Config.Os == 2)
            {
                await CDPHelper.InitCDPSession(cdpSession, ctx.Config.MaxTouchPoints);
            }
            else
            {

            }
            await CDPHelper.SetDeviceMetricsOverride(cdpSession, ctx.Config.Sw, ctx.Config.Sh, ctx.Config.DeviceScale, (ctx.Config.Os == 1 || ctx.Config.Os == 2 ? true : false));
            await CDPHelper.SetBrowserPermission(cdpSession);
            page.Dialog += async (_, dialog) =>
            {
                try { await dialog.DismissAsync(); } catch { }
            };
            page.Crash += (_, _) =>
            {
                try
                {
                    ctx.PageCrashed = true;
                    ctx.LastFailureReason = "Page crashed";
                    CancelLinkedContext(ctx, "PageCrashed");
                }
                catch { }
            };
            page.RequestFailed += (_, e) =>
            {
                try
                {
                    var failure = e.Failure ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(failure))
                        return;

                    var reqUrl = e.Url ?? string.Empty;
                    var pageUrl = page.Url ?? string.Empty;

                    // 统一记录最后失败原因，便于排查
                    ctx.LastFailureReason = $"RequestFailed: {failure}, req={reqUrl}, page={pageUrl}";

                    bool isProxyFailureAnyRequest =
                        (failure.Contains("ERR_INVALID_AUTH_CREDENTIALS", StringComparison.OrdinalIgnoreCase) ||
                        failure.Contains("ERR_TUNNEL_CONNECTION_FAILED", StringComparison.OrdinalIgnoreCase) && pageUrl.Equals(reqUrl));

                    //bool isMainPageEmptyResponse =
                    //    failure.Contains("ERR_EMPTY_RESPONSE", StringComparison.OrdinalIgnoreCase) &&
                    //    IsMainPageRequest(e, page);

                    if (!isProxyFailureAnyRequest)// && !isMainPageEmptyResponse
                        return;

                    if (ctx.ProxyFailed)
                        return;

                    ctx.ProxyFailed = true;
                    ctx.ProxyFailedReason = $"请求失败: {failure}, req={reqUrl}, page={pageUrl}";

                    CancelLinkedContext(ctx, "RequestFailedProxy");
                }
                catch
                {
                }
            };





            //page.RequestFailed += (_, e) =>
            //{

            //    //try
            //    //{
            //    //    if (!string.IsNullOrWhiteSpace(e.Failure) &&
            //    //        (e.Failure.Contains("ERR_INVALID_AUTH_CREDENTIALS") ||
            //    //         (e.Failure.Contains("ERR_TUNNEL_CONNECTION_FAILED") && page.Url.Equals(e.Url))))
            //    //    {
            //    //        LogWriteLine($"page.RequestFailed:{e.Failure},{e.Url},{page.Url}");
            //    //        if (!ctx.Config.LinkedCts.IsCancellationRequested)
            //    //            ctx.Config.LinkedCts.Cancel();
            //    //    }
            //    //}
            //    //catch { }
            //};

            page.Download += async (_, download) =>
            {
                try { await download.CancelAsync(); } catch { }
            };

            if (ctx.Page == page)
                ctx.CdpSession = cdpSession;
        }
        private void CancelLinkedContext(WorkerRunContext ctx, string reason)
        {
            try
            {
                if (!ctx.Config.LinkedCts.IsCancellationRequested)
                {
                    if (string.IsNullOrWhiteSpace(ctx.LastFailureReason))
                        ctx.LastFailureReason = reason;
                    ctx.Config.LinkedCts.Cancel();
                }
            }
            catch
            {
            }
        }
        private Task AttachLifecycleEventsAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (ctx.Browser == null || ctx.Context == null)
                return Task.CompletedTask;
            ctx.Browser.Disconnected += (_, _) =>
            {
                try
                {
                    CancelLinkedContext(ctx, "BrowserDisconnected");
                }
                catch
                {
                }
            };

            ctx.Context.Page += (_, newPage) =>
            {
                _ = HandleContextPageAsync(ctx, newPage);
            };

            return Task.CompletedTask;
        }
        private async Task HandleContextPageAsync(WorkerRunContext ctx, IPage newPage)
        {
            try
            {
                if (!ctx.Config.LinkedCts.IsCancellationRequested)
                    await InitPageAsync(ctx, newPage, ctx.Config.LinkedCts.Token);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }
        private async Task EnsureSinglePageAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            while (ctx.Context!.Pages.Count > 1)
            {
                token.ThrowIfCancellationRequested();
                await ctx.Context.Pages[^1].CloseAsync();
            }
            ctx.Page = ctx.Context.Pages[0];
            ctx.CdpSession = await ctx.CdpManager!.GetOrCreateSessionAsync(ctx.Page);
        }
        private async Task<EntryPreparationResult> PrepareEntryAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var result = new EntryPreparationResult
            {
                Success = true,
                FirstPageUrl = ctx.Config.TaskUrl,
                EndTask = false
            };

            if (!result.FirstPageUrl.Contains("[QUERY]"))
                return result;

            var retry = await RetryPolicy.ExecuteAsync(
                async ct =>
                {
                    ct.ThrowIfCancellationRequested();
                    return await _adeHelper.GetWordAsync();
                },
                maxAttempts: 6,
                successPredicate: q => !string.IsNullOrWhiteSpace(q),
                onRetry: (attempt, ex) =>
                {
                    if (ex != null)
                        LogWriteLine($"获取词条重试:{attempt}, ex={ex.Message}");
                    else
                        LogWriteLine($"获取词条重试:{attempt}");
                },
                delayMsFactory: _ => CommonHelper.RandomRange(300, 500),
                token: token);

            if (!retry.IsSuccess || string.IsNullOrWhiteSpace(retry.Value))
            {
                LogWriteLine("无法获取词条,请检查服务器");
                await Task.Delay(TimeSpan.FromSeconds(30), token);
                result.Success = false;
                result.EndTask = true;
                return result;
            }

            result.FirstPageUrl = result.FirstPageUrl.Replace("[QUERY]", retry.Value);
            LogWriteLine($"{this.Title}:搜索词条{retry.Value}");
            return result;
        }
        private async Task<bool> NavigateToEntryAsync(WorkerRunContext ctx, string url, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                await ctx.Page!.GotoAsync(url, new PageGotoOptions
                {
                    WaitUntil = WaitUntilState.DOMContentLoaded,
                    Timeout = ctx.Config.PageLoadingTimeoutMs
                });
            }
            catch (TimeoutException ex)
            {
                LogWriteLine($"加载超时:{ex.Message}");
                if (ctx.Page!.Url.Contains("sm.cn"))
                {
                    var title = await ctx.Page!.TitleAsync();
                    if (!title.StartsWith("精彩热文") && !title.StartsWith("精彩"))
                        return false;
                }
            }
            catch
            {
                throw;
            }

            ctx.CurrentPageUrl = ctx.Page!.Url;
            ctx.PagesCount = ctx.Context!.Pages.Count;
            this.QTPExecuteDSP(ctx.Config.TaskId);
            return true;
        }
        private static bool IsClosedPlaywrightException(PlaywrightException ex)
        {
            if (string.IsNullOrWhiteSpace(ex.Message))
                return false;

            return ex.Message.Contains("Target page, context or browser has been closed", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("has been closed", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("Target closed", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("browser has been closed", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("context has been closed", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("page has been closed", StringComparison.OrdinalIgnoreCase);
        }
        private bool CompleteSuccess(WorkerRunContext ctx)
        {
            this.QTPExecuteComplete(ctx.Config.TaskId);
            LogWriteLine($"{this.Title}:ExecuteWorker:Complete");
            return true;
        }

        public async Task ScrollWithTimeoutAsync(
           IPage page,
           CDPSessionManager cdpManager,
           int durationMs,
           CancellationToken cancellationToken = default)
        {
            if (page == null) throw new ArgumentNullException(nameof(page));
            if (cdpManager == null) throw new ArgumentNullException(nameof(cdpManager));
            if (durationMs <= 0) return;
            var first_delay_ms = CommonHelper.RandomRange(5432, 7890);
            await Task.Delay(first_delay_ms, cancellationToken);

            var cdpSession = await cdpManager.GetOrCreateSessionAsync(page);
            long deadline = Environment.TickCount64 + (durationMs - first_delay_ms);

            while (Environment.TickCount64 < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var scrollStateBefore = await BDInsightHelper.GetPageScrollStateAsync(page);

                // 当前不能再向下滚动（手势向上滑）
                // 不立即结束，而是在 durationMs 时间窗口内继续等待一下再检查
                if (!scrollStateBefore.CanScrollDown)
                {
                    int remainMs = (int)Math.Max(0, deadline - Environment.TickCount64);
                    if (remainMs <= 0)
                        break;

                    int waitMs = Math.Min(CommonHelper.RandomRange(300, 700), remainMs);
                    await Task.Delay(waitMs, cancellationToken);
                    continue;
                }

                var beforeY = scrollStateBefore.ScrollY;

                await HumanScrollHelper.TouchPageLongScrollAsync(
                    page,
                    cdpSession,
                    scrollCount: 1,
                    direction: PageScrollDirection.Up,
                    cancellationToken: cancellationToken);

                var scrollStateAfter = await BDInsightHelper.GetPageScrollStateAsync(page);
                var afterY = scrollStateAfter.ScrollY;
                bool moved = afterY > beforeY;

                int remainAfterScroll = (int)Math.Max(0, deadline - Environment.TickCount64);
                if (remainAfterScroll <= 0)
                    break;

                // 没移动，说明这次滑动无效；在剩余时间内稍等后继续判断
                if (!moved)
                {
                    int waitMs = Math.Min(CommonHelper.RandomRange(400, 800), remainAfterScroll);
                    await Task.Delay(waitMs, cancellationToken);
                    continue;
                }

                int delayMs = Math.Min(CommonHelper.RandomRange(1000, 2000), remainAfterScroll);
                await Task.Delay(delayMs, cancellationToken);
            }
        }

        private async Task<bool> RunMainFlowAsync(WorkerRunContext ctx, CancellationToken token)
        {
            for (ctx.PvIndex = 1; ctx.PvIndex <= ctx.Config.TotalPV; ctx.PvIndex++)
            {
                token.ThrowIfCancellationRequested();
                LogWriteLine($"{this.Title}:pv：{ctx.Config.TotalPV}/{ctx.PvIndex}");
                await EnsureSinglePageAsync(ctx, token);
                if (ctx.Page == null || ctx.Page.IsClosed)
                {
                    LogWriteLine($"{this.Title}:RunMainFlow: EnsureSinglePage后 Page为空或已关闭");
                    return false;
                }
                if (ctx.Browser == null || !ctx.Browser.IsConnected)
                {
                    LogWriteLine($"{this.Title}:RunMainFlow: EnsureSinglePage后 Browser为空或已断开");
                    return false;
                }
                var entry = await PrepareEntryAsync(ctx, token);
                if (!entry.Success)
                {
                    if (entry.EndTask)
                        return CompleteSuccess(ctx);
                    continue;
                }

                if (ctx.Config.IsTest)
                {


                }
                if (string.IsNullOrWhiteSpace(entry.FirstPageUrl))
                {
                    LogWriteLine($"{this.Title}:RunMainFlow: FirstPageUrl为空");
                    continue;
                }

                if (ctx.Page == null || ctx.Page.IsClosed)
                {
                    LogWriteLine($"{this.Title}:RunMainFlow: Navigate前 Page为空或已关闭");
                    continue;
                }

                if (ctx.Browser == null || !ctx.Browser.IsConnected)
                {
                    LogWriteLine($"{this.Title}:RunMainFlow: Navigate前 Browser为空或已断开");
                    continue;
                }

                bool gotoOk;
                try
                {
                    gotoOk = await NavigateToEntryAsync(ctx, entry.FirstPageUrl!, token);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (PlaywrightException ex) when (IsClosedPlaywrightException(ex))
                {
                    LogWriteLine($"{this.Title}:RunMainFlow: NavigateToEntryAsync 页面已关闭: {ex.Message}");
                    continue;
                }

                if (!gotoOk)
                    continue;

                ctx.ResetPerPvState();

                if (ctx.Page == null || ctx.Page.IsClosed)
                {
                    LogWriteLine($"{this.Title}:RunMainFlow: 导航后 Page为空或已关闭");
                    continue;
                }

                if (ctx.Page.Url.Contains("punish?x5secdata"))
                {
                    this.X5Secdata(ctx.Config.TaskId, 1, ctx.Page.Url);
                    return CompleteSuccess(ctx);
                }

                LogWriteLine($"{this.Title}:ExecuteWorker: {((ctx.Config.PageLoadedDelayMs) / 1000.0):N2}");
                await Task.Delay(ctx.Config.PageLoadedDelayMs, token);
                if (ctx.Config.IsTest)
                {
                    await RunTestBranchAsync(ctx, entry, token);
                    return CompleteSuccess(ctx);
                }

                var adsOk = await DetectAdAsync(ctx, token);
                if (!adsOk)
                    continue;

                if (ctx.Page == null || ctx.Page.IsClosed)
                {
                    LogWriteLine($"{this.Title}:RunMainFlow: DecideJumpClick前 Page为空或已关闭");
                    continue;
                }

                if (ctx.Browser == null || !ctx.Browser.IsConnected)
                {
                    LogWriteLine($"{this.Title}:RunMainFlow: DecideJumpClick前 Browser已断开");
                    continue;
                }
                await DecideJumpClickAsync(ctx, token);
                if (ctx.JumpClick)
                {
                    var clickFlow = await TryExecuteJumpClickAsync(ctx, token);
                    if (clickFlow == StepFlow.EndTask)
                        return CompleteSuccess(ctx);

                    if (clickFlow == StepFlow.NextPv)
                    {
                        continue;
                    }
                }

                var sleepFlow = await ExecuteTaskSleepPhaseAsync(ctx, token);
                if (sleepFlow == StepFlow.EndTask)
                    return CompleteSuccess(ctx);

                if (sleepFlow == StepFlow.NextPv)
                {
                    continue;
                }
                return CompleteSuccess(ctx);
            }

            return CompleteSuccess(ctx);
        }



        public static async Task<List<BaiduAdIframeInfo>> GetBaiduAdIframesAsync(IPage page)
        {
            var result = new List<BaiduAdIframeInfo>();

            var iframeElements = await page
                .Locator("iframe[src*='pos.baidu.com']")
                .ElementHandlesAsync();

            foreach (var iframeElement in iframeElements)
            {
                try
                {
                    var src = await iframeElement.GetAttributeAsync("src");

                    var frame = await iframeElement.ContentFrameAsync();
                    if (frame == null)
                    {
                        continue;
                    }

                    result.Add(new BaiduAdIframeInfo
                    {
                        Src = src,
                        IframeElement = iframeElement,
                        Frame = frame
                    });

                    Console.WriteLine($"找到 iframe：{src}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"处理 iframe 失败：{ex.Message}");
                }
            }

            return result;
        }






        #region Test Branch
        /// <summary>
        /// 测试方法
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="entry"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task RunTestBranchAsync(WorkerRunContext ctx, EntryPreparationResult entry, CancellationToken token)
        {

            //await ScrollUpGesture(ctx);

            //await SynthesizedScrollGestureEmulator.PageScrollAsync(ctx.Page, ctx.CdpSession, 3, PageScrollDirection.Up);


            //var trace = await SwipeEmulator.SwipeOnceHumanAsync(
            //    page: ctx.Page!,
            //    client: ctx.CdpSession!,
            //    direction: ScrollDirection.Up);

            //if (trace != null)
            //{
            //    SwipeTraceRenderer.DrawEachTraceGif(
            //        traces: new List<SwipeTrace>() { trace },
            //        outputDir: Path.Combine(AppContext.BaseDirectory, "swipe-traces"),
            //        width: ctx.Page!.ViewportSize!.Width,
            //        height: ctx.Page!.ViewportSize!.Height);


            //}



            //await DecideJumpClickAsync(ctx, token);
            // var adsOk = await DetectAndUploadAdWordsAsync(ctx, entry.QueryWord, token);

            //var handleFlow = await HandleLandingPageAsync(ctx, token);
            //ctx.JumpClick = true;
            //ctx.PageTriggerClick = true;
            //var sleepFlow = await ExecuteTaskSleepPhaseAsync(ctx, token);

            //await Task.Delay(CommonHelper.RandomRange(5000, 8000));
            //this.QTPExecuteSuccess(ctx.Config.TaskId);
            //LogWriteLine($"{this.Title}:ExecuteWorker:Success");
            //await Task.Delay(ctx.Config.SleepMs, token);


            await Task.Delay(TimeSpan.FromSeconds(150), token);
            //var anchorCenten = ctx.Page!.Locator(".anchor-centen").Filter(new()
            //{
            //    Has = ctx.Page!.Locator("span").Filter(new()
            //    {
            //        HasTextString = "人在看"
            //    })

            //});
            //try
            //{
            //    await anchorCenten.First.WaitForAsync(new LocatorWaitForOptions
            //    {
            //        State = WaitForSelectorState.Visible,
            //        Timeout = 15000
            //    });
            //}
            //catch (TimeoutException)
            //{


            //}

            //await Task.Delay(CommonHelper.RandomRange(5000, 8000));
            //var count = await anchorCenten.CountAsync();
            //if (count > 0)
            //{
            //    for (int i = 0; i < count; i++)
            //    {
            //        var target = anchorCenten.Nth(CommonHelper.RandomRange(0, count));
            //        try
            //        {
            //            await target.First.ScrollIntoViewIfNeededAsync();
            //            var url = ctx.Page.Url;
            //            var click = await ClickAndDetectNavigationAsync(ctx, target.First, token);
            //            if (click.Navigated || !ctx.Page.Url.Equals(url))
            //            {
            //                this.QTPExecuteSuccess(ctx.Config.TaskId);
            //                LogWriteLine($"{this.Title}:ExecuteWorker:Success");
            //                await Task.Delay(ctx.Config.SleepMs, token);
            //                break;
            //            }
            //        }
            //        catch (Exception)
            //        {


            //        }

            //    }
            //}

        }

        #endregion

        #region Ads / JumpClick

        /// <summary>
        /// 检测页面广标记
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="q"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<bool> DetectAdAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ctx.Page == null || ctx.Page.IsClosed)
            {
                LogWriteLine("广告检测终止: Page为空或已关闭");
                return false;
            }

            if (ctx.Browser == null || !ctx.Browser.IsConnected)
            {
                LogWriteLine("广告检测终止: Browser为空或已断开");
                return false;
            }

            try
            {
                var baiduAds = await GetBaiduAdIframesAsync(ctx.Page!);
                ctx.PageAdsCount = baiduAds.Count();
                if (ctx.PageAdsCount <= 0)
                {
                    LogWriteLine("没有广告标记,重试");
                    return false;
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (PlaywrightException ex) when (IsClosedPlaywrightException(ex))
            {
                LogWriteLine($"广告检测失败: 页面/上下文/浏览器已关闭, {ex.Message}");
                return false;
            }
        }


        /// <summary>
        /// 处理点击比例
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task DecideJumpClickAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int clickRate = ctx.Config.TaskArgs.SelectToken("task.click_rate")!.Value<int>();
            ctx.JumpClick = false;
            ctx.PageTriggerClick = false;
            if (clickRate <= 0)
            {
                return;
            }
            var ctr = await _aggregator.GetClickRatioAsync(ctx.Config.TaskId, clickRate);
            LogWriteLine($"点击比率:{(ctr * 100):N2}%");
            ctx.JumpClick = await _aggregator.CanClickthroughAsync(ctx.Config.TaskId, clickRate);
        }

        /// <summary>
        /// 触发广告
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<StepFlow> TryExecuteJumpClickAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var sponsoreds = await GetBaiduAdIframesAsync(ctx.Page!);
            var sponsoredCount = sponsoreds.Count();
            if (sponsoredCount <= 0)
            {
                return StepFlow.NextPv;
            }

            var candidates = Enumerable.Range(0, sponsoredCount)
                    .OrderBy(_ => Guid.NewGuid())
                    .Select(i => sponsoreds[i])
                    .ToList();

            foreach (var sponsored in candidates)
            {
                token.ThrowIfCancellationRequested();
                //await SwipeEmulator.SwipeToElementAsync(ctx.Page!, ctx.CdpSession!, sponsored.IframeElement);
                await SynthesizedScrollGestureEmulator.SwipeToElementAsync(ctx.Page!, ctx.CdpSession!, sponsored.IframeElement);




                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                var box = await sponsored.IframeElement.BoundingBoxAsync();
                if (box != null)
                    LogWriteLine($"触发广告位:{sponsored.Src}:({box.X},{box.Y},{box.Width},{box.Height})");
                else
                    LogWriteLine($"触发广告位:{sponsored.Src}");
                var click = await ClickAndDetectNavigationAsync(ctx, sponsored.IframeElement, token);
                if (!click.Attempted)
                    continue;
                if (click.Navigated)
                {
                    if (!ctx.Page!.Url.StartsWith("https://m.baidu.com/"))
                    {
                        this.QTPExecuteClickthrough(ctx.Config.TaskId);
                        LogWriteLine($"{this.Title}:ExecuteWorker:Clickthrough");
                        ctx.PageTriggerClick = true;

                    }
                    await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                    return await HandleLandingPageAsync(ctx, token);
                }
            }
            return StepFlow.NextPv;
        }




        public async Task<ClickResult> TryRandomViewportClickableClickAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                var elements = await BDInsightHelper.GetCurrentViewportClickableElementsAsync(ctx.Page!, token);
                if (elements.Count == 0)
                    return ClickResult.NoNavigation();

                foreach (var target in elements.OrderBy(_ => Guid.NewGuid()))
                {
                    token.ThrowIfCancellationRequested();
                    var result = await ClickAndDetectNavigationAsync(ctx, target, token);
                    if (result.Navigated)
                        return result;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch { }

            return ClickResult.NoNavigation();
        }


        private async Task<ILocator?> PickSponsoredTargetAsync(ILocator sponsored, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var alis = sponsored.Locator("a.c-title,a[data-url^='http']");
            var visible = await BDInsightHelper.GetVisibleElementsAsync(alis);
            if (visible.Count == 0)
                return null;

            var urls = new List<(ILocator Locator, string Url)>();
            foreach (var el in visible)
            {
                token.ThrowIfCancellationRequested();

                var dataUrl = await el.GetAttributeAsync("data-url");
                if (!string.IsNullOrWhiteSpace(dataUrl))
                    urls.Add((el, dataUrl));
            }

            if (urls.Count == 0)
                return null;

            var exts = new[] { ".apk", ".zip", ".exe", ".7z", ".rar" };
            var filtered = urls
                .Where(x => !exts.Any(ext => x.Url.Contains(ext, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(x => x.Url.Length)
                .ToList();

            if (filtered.Count > 0)
            {
                var groups = filtered
                       .GroupBy(x => new Uri(x.Url).Host, StringComparer.OrdinalIgnoreCase)
                       .OrderByDescending(g => g.Count())
                       .ToList();

                foreach (var g in groups)
                {
                    var list = g.ToList();

                    var uMob = list
                        .Where(x => x.Url.Contains(".u-mob.", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (uMob.Count > 0)
                        return uMob[Random.Shared.Next(uMob.Count)].Locator;

                    if (list.Count > 0)
                        return list[Random.Shared.Next(list.Count)].Locator;
                }
            }

            return urls.OrderByDescending(x => x.Url.Length).First().Locator;
        }

        #endregion

        #region Landing Dispatcher / Strategies

        private async Task<StepFlow> HandleLandingPageAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (ctx.LandingDispatcher == null)
                return StepFlow.Continue;

            return await ctx.LandingDispatcher.DispatchAsync(ctx, token);
        }

        #endregion



        #region Generic Landing Helpers

        public async Task TryHandle1688RecommendWordsAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            _aggregator.AddLocalMetric(ctx.Config.TaskId, "dsp_p4psearch");
            var metrics = _aggregator.GetLocalMetrics(ctx.Config.TaskId, "dsp_p4psearch", "dsp_p4psearch_click");

            if (metrics["dsp_p4psearch"] > 0)
                LogWriteLine($"1688询价比率:{(metrics["dsp_p4psearch_click"] / (double)metrics["dsp_p4psearch"] * 100):N2}%");

            bool canClick = _appSettings.p4psearchRate == 100
                || metrics["dsp_p4psearch_click"] == 0
                || ((metrics["dsp_p4psearch_click"] / (double)metrics["dsp_p4psearch"]) * 100 < _appSettings.p4psearchRate);

            if (!canClick)
                return;

            try
            {

                await HumanScrollHelper.TouchPageLongScrollAsync(
                  ctx.Page!,
                  ctx.CdpSession!,
                  scrollCount: CommonHelper.RandomRange(8, 10),
                  direction: PageScrollDirection.Up,
                  predexp: async _ =>
                  {
                      var panel = ctx.Page!.Locator("div[class*='ab-recommend-words']");
                      if (await panel.CountAsync() > 0)
                      {
                          await panel.First.ScrollIntoViewIfNeededAsync();
                          return true;
                      }
                      return false;
                  },
                  cancellationToken: token);




                token.ThrowIfCancellationRequested();







                await Task.Delay(CommonHelper.RandomRange(100, 200), token);

                var container = ctx.Page!.Locator("div[class*='ab-recommend-words']").First;
                if (await container.CountAsync() == 0)
                {
                    return;
                }
                var links = container.Locator("a.word");
                int count = await links.CountAsync();
                if (count == 0)
                {
                    return;
                }
                var containerBox = await container.BoundingBoxAsync();
                var recommends = new List<ILocator>();
                if (containerBox != null)
                {
                    try
                    {
                        double containerTop = containerBox.Y;
                        double containerBottom = containerBox.Y + containerBox.Height;
                        double containerLeft = containerBox.X;
                        double containerRight = containerBox.X + containerBox.Width;

                        for (int i = 0; i < count; i++)
                        {
                            var link = links.Nth(i);
                            var box = await link.BoundingBoxAsync();
                            if (box == null)
                                continue;

                            double linkTop = box.Y;
                            double linkBottom = box.Y + box.Height;
                            double linkLeft = box.X;
                            double linkRight = box.X + box.Width;

                            // 判断是否和容器可视区域有交集
                            bool verticallyVisible = linkBottom > containerTop && linkTop < containerBottom;
                            bool horizontallyVisible = linkRight > containerLeft && linkLeft < containerRight;

                            if (verticallyVisible && horizontallyVisible)
                            {
                                recommends.Add(link);
                            }
                        }
                    }
                    catch (Exception)
                    {

                    }

                }
                if (recommends.Count() == 0)
                    return;
                _aggregator.AddLocalMetric(ctx.Config.TaskId, "dsp_p4psearch_click");
                foreach (var target_index in Enumerable.Range(0, recommends.Count()).OrderBy(o => Guid.NewGuid()))
                {
                    var recommend = recommends[target_index];
                    var click = await ClickAndDetectNavigationAsync(ctx, recommend, token);
                    if (click.Navigated)
                    {
                        await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                        break;
                    }

                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch { }
        }

        public async Task<ILocator?> ResolveOfferItemsAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var url = ctx.Page!.Url;
            ILocator? offerItems = null;

            if (url.Contains("m.p4psearch.1688.com"))
            {
                if (_appSettings.Rfq1688 && _appSettings.Rfq1688Rate > 0)
                    _aggregator.AddLocalMetric(ctx.Config.TaskId, "dsp_rfq1688");

                await HumanScrollHelper.TouchPageLongScrollAsync(
                    ctx.Page!,
                    ctx.CdpSession!,
                    scrollCount: CommonHelper.RandomRange(1, 4),
                    direction: PageScrollDirection.Up,
                    cancellationToken: token);
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                offerItems = ctx.Page.Locator("//div[starts-with(@class,'offer-item')]");
            }
            else if (url.Contains("m.1688.com"))
            {
                await HumanScrollHelper.TouchPageLongScrollAsync(
                    ctx.Page!,
                    ctx.CdpSession!,
                    scrollCount: CommonHelper.RandomRange(1, 4),
                    direction: PageScrollDirection.Up,
                    cancellationToken: token);
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);


                offerItems = ctx.Page.Locator("//div[starts-with(@class,'offer-item')]");
            }
            else if (url.Contains("b2b.baidu.com"))
            {
                await HumanScrollHelper.TouchPageLongScrollAsync(
                    ctx.Page!,
                    ctx.CdpSession!,
                    scrollCount: CommonHelper.RandomRange(1, 4),
                    direction: PageScrollDirection.Up,
                    cancellationToken: token);
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);

                offerItems = ctx.Page.Locator(".img-content,.list-title,.content-without-title");
                if (await offerItems.CountAsync() == 0)
                    offerItems = ctx.Page.Locator("a.product-item-link");
            }
            else if (url.Contains("aden.baidu.com") || url.Contains("ada.baidu.com"))
            {
                //https://ada.baidu.com/site
                await Task.Delay(CommonHelper.RandomRange(3000, 5000));
                var info = ctx.Page!.GetByText(
                    new Regex(@"法律咨询|律师|客服")
                );
                var info_count = await info.CountAsync();
                if (info_count > 0)
                {
                    var input_area = ctx.Page!.Locator(".input-area .fake-input");
                    if (await input_area.CountAsync() > 0)
                    {
                        await Task.Delay(CommonHelper.RandomRange(800, 1200));
                        await CDPHelper.MouseClickAsync(ctx.Page!, ctx.CdpSession!, input_area.First);
                        await Task.Delay(CommonHelper.RandomRange(800, 1200));
                    }
                    input_area = ctx.Page!.Locator(".input-area textarea");
                    if (await input_area.CountAsync() > 0)
                    {
                        await Task.Delay(CommonHelper.RandomRange(800, 1200));
                        await input_area.FillAsync("");
                        await Task.Delay(CommonHelper.RandomRange(800, 1200));
                        var info_text = await _adeHelper.GetTalkAsync("law");
                        if (!string.IsNullOrWhiteSpace(info_text))
                        {
                            await input_area.PressSequentiallyAsync(info_text);
                            await Task.Delay(CommonHelper.RandomRange(3000, 5000));
                        }
                        else
                        {
                            await input_area.PressSequentiallyAsync("你好,有事咨询");
                        }
                    }
                    var send_btn = ctx.Page!.Locator(".input-area .send-btn");
                    if (await send_btn.CountAsync() > 0)
                    {
                        await Task.Delay(CommonHelper.RandomRange(800, 1200));
                        await CDPHelper.MouseClickAsync(ctx.Page!, ctx.CdpSession!, send_btn.First);
                        await Task.Delay(CommonHelper.RandomRange(3000, 5000));
                    }
                }
                else
                {
                    await HumanScrollHelper.TouchPageLongScrollAsync(
                        ctx.Page!,
                        ctx.CdpSession!,
                        scrollCount: CommonHelper.RandomRange(1, 4),
                        direction: PageScrollDirection.Up,
                        cancellationToken: token);
                    await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                    offerItems = ctx.Page.Locator("//div[contains(@class,'ec_content')]");
                    if (await offerItems.CountAsync() == 0)
                    {
                        for (int i = 1; i < 4; i++)
                        {
                            var locator = ctx.Page
                                .Locator("body,iframe")
                                .Filter(new() { Visible = true })
                                .First;
                            if (await locator.CountAsync() > 0)
                            {
                                await locator.First.ScrollIntoViewIfNeededAsync();
                                var result = await ClickAndDetectNavigationAsync(ctx, locator.First, token);
                                if (result.Navigated)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else if (url.Contains("uland.taobao.com"))
            {
                await HumanScrollHelper.TouchPageLongScrollAsync(
                    ctx.Page!,
                    ctx.CdpSession!,
                    scrollCount: CommonHelper.RandomRange(1, 4),
                    direction: PageScrollDirection.Up,
                    cancellationToken: token);
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                offerItems = ctx.Page.Locator("//a[starts-with(@class,'link')]");
            }
            else if (url.StartsWith("https://pro.m.jd.com/mall/active"))
            {
                //https://pro.m.jd.com/mall/active/6PRJiy2LHsUc6oezS9u5rjfYqmj/index.htm
                await HandleJdActivePageAsync(ctx, token);

                if (!ctx.Page.Url.StartsWith("https://plogin.m.jd.com/"))
                {
                    await Task.Delay(CommonHelper.RandomRange(3000, 5000), token);
                    offerItems = ctx.Page.Locator(".masonryCard,.commodity-list .commodity-desc,.list-con .product,a.goods,.feed-product-container");
                    //if (await offerItems.CountAsync() == 0)
                    //    offerItems = ctx.Page.Locator(".feed-product-container");
                    //if (await offerItems.CountAsync() == 0)
                    //    offerItems = ctx.Page.Locator(".feed-product-container,a.goods,.list-con .product");
                    //if (await offerItems.CountAsync() == 0)
                    //    offerItems = ctx.Page.Locator("img");
                }
            }
            else if (url.Contains("m.jd.com"))
            {
                await HumanScrollHelper.TouchPageLongScrollAsync(
                    ctx.Page!,
                    ctx.CdpSession!,
                    scrollCount: CommonHelper.RandomRange(1, 4),
                    direction: PageScrollDirection.Up,
                    cancellationToken: token);
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);

                offerItems = ctx.Page.Locator(".commodity-list .commodity-desc,.list-con .product,a.goods,.feed-product-container");
                if (await offerItems.CountAsync() == 0)
                    offerItems = ctx.Page.Locator(".feed-product-container");
                if (await offerItems.CountAsync() == 0)
                    offerItems = ctx.Page.Locator(".feed-product-container,a.goods,.list-con .product");
                if (await offerItems.CountAsync() == 0)
                {
                    var count = await CenterClickableFinder.MarkCandidatesAsync(ctx.Page);
                    if (count > 0)
                    {
                        var locator_list = CenterClickableFinder.GetMarkedLocator(ctx.Page);
                        var locator_count = await locator_list.CountAsync();
                        if (locator_count > 0)
                        {
                            foreach (var target_index in Enumerable.Range(0, locator_count).OrderBy(o => Guid.NewGuid()))
                            {
                                var target = locator_list.Nth(target_index);
                                var target_text = await target.InnerTextAsync();
                                if (!string.IsNullOrWhiteSpace(target_text))
                                    LogWriteLine(target_text);
                                var result = await ClickAndDetectNavigationAsync(ctx, target, token);
                                if (result.Navigated)
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            else if (url.StartsWith("https://cunliangtech.com/"))
            {
                await Task.Delay(CommonHelper.RandomRange(3000, 5000));
                var info_handler = async () =>
                {
                    try
                    {
                        var info = ctx.Page!.GetByText(
                            new Regex(@"法律咨询|律师|客服")
                        );
                        var info_count = await info.CountAsync();
                        if (info_count > 0)
                        {
                            var input_area = ctx.Page!.Locator(".input-area .fake-input");
                            if (await input_area.CountAsync() > 0)
                            {
                                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                                await ClickAndDetectNavigationAsync(ctx, input_area.First, token);
                                await Task.Delay(CommonHelper.RandomRange(800, 1200));
                            }
                            input_area = ctx.Page!.Locator(".input-area textarea");
                            if (await input_area.CountAsync() > 0)
                            {
                                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                                await input_area.FillAsync("");
                                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                                var info_text = await _adeHelper.GetTalkAsync("law", token);
                                if (!string.IsNullOrWhiteSpace(info_text))
                                {
                                    await input_area.PressSequentiallyAsync(info_text);
                                }
                                else
                                {
                                    await input_area.PressSequentiallyAsync("你好,有事咨询");
                                }
                            }
                            var send_btn = ctx.Page!.Locator(".input-area .send-btn");
                            if (await send_btn.CountAsync() > 0)
                            {
                                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                                await CDPHelper.MouseClickAsync(ctx.Page!, ctx.CdpSession!, send_btn.First);
                                await Task.Delay(CommonHelper.RandomRange(3000, 5000), token);
                            }
                        }
                    }
                    catch (Exception)
                    {


                    }


                };

                var locator_list = ctx.Page!.GetByText(
                    new Regex(@"查看更多")
                );
                var locator_count = await locator_list.CountAsync();
                if (locator_count > 0)
                {
                    foreach (var target_index in Enumerable.Range(0, locator_count).OrderBy(o => Guid.NewGuid()))
                    {
                        var target = locator_list.Nth(target_index);
                        await target.ScrollIntoViewIfNeededAsync();
                        await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                        var target_text = await target.InnerTextAsync();
                        if (!string.IsNullOrWhiteSpace(target_text))
                            LogWriteLine(target_text);
                        var result = await ClickAndDetectNavigationAsync(ctx, target, token);
                        if (result.Navigated)
                        {
                            if (ctx.Page.Url.Contains("aden.baidu.com") || ctx.Page.Url.Contains("ada.baidu.com"))
                            {
                                await Task.Delay(CommonHelper.RandomRange(3000, 5000), token);
                                await info_handler();
                            }
                            break;
                        }
                    }
                }
                else
                {
                    List<ILocator?> elements = new List<ILocator?>(); ;
                    var element_count = 0;
                    foreach (var frame in ctx.Page!.Frames)
                    {
                        try
                        {
                            var loc = frame.GetByText(new Regex(@"查看更多"));
                            var count = await loc.CountAsync();
                            if (count > 0)
                            {
                                elements.Add(loc);
                                element_count++;
                            }
                        }
                        catch
                        {
                            // 某些 frame 可能临时不可用，跳过
                        }
                    }

                    if (elements.Count() > 0 && element_count > 0)
                    {
                        foreach (var target_index in Enumerable.Range(0, element_count).OrderBy(_ => Guid.NewGuid()))
                        {

                            var raw = elements[target_index]!;
                            ILocator target;
                            var clickableAncestor = raw.Locator("xpath=ancestor-or-self::*[self::a or self::button or @role='button' or @onclick][1]");

                            if (await clickableAncestor.CountAsync() > 0)
                                target = clickableAncestor.First;
                            else
                                target = raw.Locator("..");
                            var box = await target.BoundingBoxAsync();
                            var target_text = await target.InnerTextAsync();
                            if (!string.IsNullOrWhiteSpace(target_text))
                                LogWriteLine(target_text);
                            var click = await ClickAndDetectNavigationAsync(ctx, target, token);
                            if (click != null && click.Navigated)
                            {
                                if (ctx.Page.Url.Contains("aden.baidu.com") || ctx.Page.Url.Contains("ada.baidu.com"))
                                {
                                    await Task.Delay(CommonHelper.RandomRange(3000, 5000), token);
                                    await info_handler();
                                }
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int i = 1; i < 4; i++)
                        {
                            var locator = ctx.Page
                                .Locator("body,iframe")
                                .Filter(new() { Visible = true })
                                .First;
                            if (await locator.CountAsync() > 0)
                            {
                                await locator.First.ScrollIntoViewIfNeededAsync();
                                var result = await ClickAndDetectNavigationAsync(ctx, locator.First, token);
                                if (result.Navigated)
                                {
                                    if (ctx.Page.Url.Contains("aden.baidu.com") || ctx.Page.Url.Contains("ada.baidu.com"))
                                    {
                                        await Task.Delay(CommonHelper.RandomRange(3000, 5000), token);
                                        await info_handler();
                                    }
                                    break;
                                }
                            }

                        }
                    }
                }
            }
            return offerItems;
        }


        /// <summary>
        /// JD
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<bool> HandleJdActivePageAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var his1 = ctx.Page!.Locator("*:has-text('医院')");
            var his2 = ctx.Page.Locator("*:has-text('问诊')");
            bool medical = await his1.CountAsync() > 0 || await his2.CountAsync() > 0;
            ClickResult? result = null;
            token.ThrowIfCancellationRequested();


            await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
            await HumanScrollHelper.TouchPageLongScrollAsync(
                ctx.Page!,
                ctx.CdpSession!,
                scrollCount: CommonHelper.RandomRange(1, 4),
                direction: PageScrollDirection.Up,
                cancellationToken: token);
            await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            if (medical)
            {
                //|图文.*起|电话.*起
                var locator_list = ctx.Page.Locator("text=/剩.*个名额/").Filter(new() { Visible = true });
                var locator_count = await locator_list.CountAsync();
                if (locator_count > 0)
                {
                    foreach (var target_index in Enumerable.Range(0, locator_count).OrderBy(o => Guid.NewGuid()))
                    {
                        var target = locator_list.Nth(target_index);
                        await target.ScrollIntoViewIfNeededAsync();
                        await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                        var target_text = await target.InnerTextAsync();
                        if (!string.IsNullOrWhiteSpace(target_text))
                            LogWriteLine(target_text);
                        result = await ClickAndDetectNavigationAsync(ctx, target, token);
                        if (result.Navigated)
                        {
                            break;
                        }
                    }
                }
            }

            if ((result == null || !result.Navigated))
            {
                var count = await CenterClickableFinder.MarkCandidatesAsync(ctx.Page);
                if (count > 0)
                {
                    var locator_list = CenterClickableFinder.GetMarkedLocator(ctx.Page);
                    var locator_count = await locator_list.CountAsync();
                    if (locator_count > 0)
                    {
                        foreach (var target_index in Enumerable.Range(0, locator_count).OrderBy(o => Guid.NewGuid()))
                        {
                            var target = locator_list.Nth(target_index);
                            var target_text = await target.InnerTextAsync();
                            if (!string.IsNullOrWhiteSpace(target_text))
                                LogWriteLine(target_text);
                            result = await ClickAndDetectNavigationAsync(ctx, target, token);
                            if (result.Navigated)
                            {
                                break;
                            }
                        }
                    }
                }
                else
                {
                    var locator = ctx.Page
                    .Locator("body,iframe")
                    .Filter(new() { Visible = true })
                    .First;
                    if (await locator.CountAsync() > 0)
                    {
                        await locator.First.ScrollIntoViewIfNeededAsync();
                        result = await ClickAndDetectNavigationAsync(ctx, locator.First, token);
                    }
                }
            }
            //await TouchPageScroll(ctx.Page, ctx.CdpSession!, CommonHelper.RandomRange(medical ? 1 : 0, medical ? 5 : 3), 1);
            //if (ctx.Page.Url.StartsWith("https://plogin.m.jd.com/"))
            //{
            //    return true;
            //}
            //// var result = await TryRandomViewportClickableClickAsync(ctx, token);

            //if (ctx.Page.Url.StartsWith("https://pro.m.jd.com/mall/active"))
            //{

            //}
            //if (ctx.Page.Url.StartsWith("https://laputa.healthjd.com/doctor_home"))
            //{
            //    await Task.Delay(CommonHelper.RandomRange(3000, 5000), token);
            //    return true;
            //}
            if (result != null && result.Navigated)
            {
                await Task.Delay(CommonHelper.RandomRange(3000, 5000), token);
                return true;
            }

            return false;
        }

        #endregion


        #region Task Sleep Phase

        private async Task<StepFlow> ExecuteTaskSleepPhaseAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (ctx.Page!.Url.Contains("1688.com") && ctx.Page.Url.Contains("_tmd_") && ctx.Page!.Url.Contains("punish?x5secdata"))
            {
                return StepFlow.EndTask;
            }
            if (ctx.JumpClick && ctx.PageTriggerClick)
            {
                await TryHandleRfq1688Async(ctx, token);
                await TryHandleQianhuFormAsync(ctx, token);
                await TryHandleLouisvuittonAsync(ctx, token);
            }
            this.QTPExecuteSuccess(ctx.Config.TaskId);
            LogWriteLine($"{this.Title}:ExecuteWorker:Success");
            if (ctx.Config.TotalPV > 1)
            {
                if (ctx.JumpClick && !ctx.PageTriggerClick)
                {
                    await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                    return StepFlow.NextPv;
                }

                if (ctx.JumpClick && ctx.PageTriggerClick && !ctx.Config.PvsTriggerOne)
                {
                    await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                    return StepFlow.NextPv;
                }
            }


            if (ctx.Page!.Url.StartsWith("https://login.m.taobao.com")
                || ctx.Page.Url.StartsWith("https://havanalogin.taobao.com")
                || ctx.Page.Url.StartsWith("https://plogin.m.jd.com"))
            {
                await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                return StepFlow.EndTask;
            }
            if (ctx.Page!.Url.StartsWith("https://h5.m.taobao.com"))
            {
                if (await ctx.Page.GetByText("获取验证码").CountAsync() > 0)
                {
                    await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                    return StepFlow.EndTask;
                }
            }
            DateTime start = DateTime.Now;

            if (ctx.JumpClick && ctx.PageTriggerClick)
            {
                await TryHandleAllAsync(ctx, token);
            }


            LogWriteLine("延时停留");
            PageScrollDirection direction = PageScrollDirection.Up;
            var loop = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                loop++;

                try
                {
                    LogWriteLine("滑动操作");

                    await HumanScrollHelper.TouchPageLongScrollAsync(
                    ctx.Page!,
                    ctx.CdpSession!,
                    scrollCount: 1,
                    direction: direction,
                    cancellationToken: token);

                    token.ThrowIfCancellationRequested();

                    if (await IsPageEnd(ctx.Page))
                        direction = PageScrollDirection.Down;
                    else if (await IsPageTop(ctx.Page))
                        direction = PageScrollDirection.Up;

                    if ((int)(DateTime.Now - start).TotalMilliseconds >= ctx.Config.SleepMs)
                        break;
                    await Task.Delay(CommonHelper.RandomRange(1000, 2000), token);

                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    break;
                }
            }

            LogWriteLine("动作完成");
            return StepFlow.EndTask;
        }

        private async Task TryHandleNoRfq1688Async(WorkerRunContext ctx, CancellationToken token)
        {
            try
            {
                await Task.Delay(CommonHelper.RandomRange(1500, 2000), token);
                await ClearPageCloseBtn(ctx.Page!, ctx.CdpSession!);
                await Task.Delay(CommonHelper.RandomRange(200, 300), token);
                await ClearSuccessTipNewCloseNew(ctx.Page!, ctx.CdpSession!);
                await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                await HumanScrollHelper.TouchPageLongScrollAsync(
                ctx.Page!,
                ctx.CdpSession!,
                scrollCount: CommonHelper.RandomRange(0, 3),
                direction: PageScrollDirection.Up,
                cancellationToken: token);
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                var locator_detail = ctx.Page!.Locator("*:text-is('全部商品')");
                var locator_detail_count = await locator_detail.CountAsync();
                if (locator_detail_count == 0)
                {
                    locator_detail = ctx.Page.Locator("*:text-is('进店看看')");
                    locator_detail_count = await locator_detail.CountAsync();
                }
                if (locator_detail_count == 0)
                {
                    locator_detail = ctx.Page.Locator("*:text-is('进店看厂')");
                    locator_detail_count = await locator_detail.CountAsync();
                }
                if (locator_detail_count == 0)
                {
                    locator_detail = ctx.Page.Locator(".recommend-container");
                    locator_detail_count = await locator_detail.CountAsync();
                }

                if (locator_detail_count > 0)
                {
                    await locator_detail.ScrollIntoViewIfNeededAsync();
                    await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                    var clickRes2 = await ClickAndDetectNavigationAsync(ctx, locator_detail.First, token);
                    if (clickRes2.Navigated)
                    {
                        await Task.Delay(CommonHelper.RandomRange(1500, 2000), token);
                        await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
                        await Task.Delay(CommonHelper.RandomRange(200, 300), token);
                        await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
                        await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                        await HumanScrollHelper.TouchPageLongScrollAsync(
                        ctx.Page!,
                        ctx.CdpSession!,
                        scrollCount: CommonHelper.RandomRange(0, 3),
                        direction: PageScrollDirection.Up,
                        cancellationToken: token);
                        await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                        locator_detail = ctx.Page
                            .Locator("body,iframe")
                            .Filter(new() { Visible = true })
                            .First;

                        if (await locator_detail.CountAsync() > 0)
                        {
                            await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                            await locator_detail.First.ScrollIntoViewIfNeededAsync();
                            var clickRes3 = await ClickAndDetectNavigationAsync(ctx, locator_detail.First, token);
                            if (clickRes3.Navigated)
                            {
                                await Task.Delay(CommonHelper.RandomRange(1500, 2000), token);
                                await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
                                await Task.Delay(CommonHelper.RandomRange(200, 300), token);
                                await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
                                await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                                await HumanScrollHelper.TouchPageLongScrollAsync(
                                ctx.Page!,
                                ctx.CdpSession!,
                                scrollCount: CommonHelper.RandomRange(0, 3),
                                direction: PageScrollDirection.Up,
                                cancellationToken: token);
                                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                            }

                        }
                    }

                }
                else
                {
                    locator_detail = ctx.Page
                        .Locator("body,iframe")
                        .Filter(new() { Visible = true })
                        .First;

                    if (await locator_detail.CountAsync() > 0)
                    {
                        var clickRes3 = await ClickAndDetectNavigationAsync(ctx, locator_detail.First, token);
                        if (clickRes3.Navigated)
                        {
                            await Task.Delay(CommonHelper.RandomRange(1500, 2000), token);
                            await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
                            await Task.Delay(CommonHelper.RandomRange(200, 300), token);
                            await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
                            await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                            await HumanScrollHelper.TouchPageLongScrollAsync(
                            ctx.Page!,
                            ctx.CdpSession!,
                            scrollCount: CommonHelper.RandomRange(0, 3),
                            direction: PageScrollDirection.Up,
                            cancellationToken: token);
                            await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                        }

                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
            }
            return;
        }

        private async Task TryHandleRfq1688Async(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (!ctx.Page!.Url.Contains("1688.com"))
                return;

            #region RFQ
            //if (CommonHelper.RandomRange(0, 11) % 2 > 0)
            //{
            //    if (ctx.Page!.Url.Contains("m.1688.com"))
            //    {
            //        await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
            //        await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
            //        await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
            //        await HumanScrollHelper.TouchPageLongScrollAsync(
            //        ctx.Page!,
            //        ctx.CdpSession!,
            //        scrollCount: CommonHelper.RandomRange(0, 3),
            //        direction: PageScrollDirection.Up,
            //        cancellationToken: token);
            //        await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            //        var locator = ctx.Page.Locator("*:text-is('全部商品')");
            //        var locator_count = await locator.CountAsync();
            //        if (locator_count == 0)
            //        {
            //            locator = ctx.Page.Locator("*:text-is('进店看看')");
            //            locator_count = await locator.CountAsync();
            //        }
            //        if (locator_count == 0)
            //        {
            //            locator = ctx.Page.Locator("*:text-is('进店看厂')");
            //            locator_count = await locator.CountAsync();
            //        }
            //        if (locator_count == 0)
            //        {
            //            locator = ctx.Page.Locator(".recommend-container");
            //            locator_count = await locator.CountAsync();
            //        }

            //        if (locator_count > 0)
            //        {
            //            await locator.ScrollIntoViewIfNeededAsync();
            //            await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            //            var clickRes2 = await ClickElementAndDetectNavigationAsync(ctx, locator.First, token);
            //            if (clickRes2.Navigated)
            //            {
            //                await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
            //                await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
            //                await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
            //                await HumanScrollHelper.TouchPageLongScrollAsync(
            //                ctx.Page!,
            //                ctx.CdpSession!,
            //                scrollCount: CommonHelper.RandomRange(0, 3),
            //                direction: PageScrollDirection.Up,
            //                cancellationToken: token);
            //                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            //                locator = ctx.Page
            //                    .Locator("body,iframe")
            //                    .Filter(new() { Visible = true })
            //                    .First;

            //                if (await locator.CountAsync() > 0)
            //                {
            //                    await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            //                    await locator.First.ScrollIntoViewIfNeededAsync();
            //                    var clickRes3 = await ClickElementAndDetectNavigationAsync(ctx, locator.First, token);
            //                    if (clickRes3.Navigated)
            //                    {
            //                        await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
            //                        await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
            //                        await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
            //                        await HumanScrollHelper.TouchPageLongScrollAsync(
            //                        ctx.Page!,
            //                        ctx.CdpSession!,
            //                        scrollCount: CommonHelper.RandomRange(0, 3),
            //                        direction: PageScrollDirection.Up,
            //                        cancellationToken: token);
            //                        await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            //                    }

            //                }
            //            }

            //        }
            //        else
            //        {
            //            locator = ctx.Page
            //                .Locator("body,iframe")
            //                .Filter(new() { Visible = true })
            //                .First;

            //            if (await locator.CountAsync() > 0)
            //            {
            //                var clickRes3 = await ClickElementAndDetectNavigationAsync(ctx, locator.First, token);
            //                if (clickRes3.Navigated)
            //                {
            //                    await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
            //                    await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
            //                    await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
            //                    await HumanScrollHelper.TouchPageLongScrollAsync(
            //                    ctx.Page!,
            //                    ctx.CdpSession!,
            //                    scrollCount: CommonHelper.RandomRange(0, 3),
            //                    direction: PageScrollDirection.Up,
            //                    cancellationToken: token);
            //                    await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            //                }

            //            }
            //        }
            //        return;
            //    }

            //    if (!(_appSettings.Rfq1688 && _appSettings.Rfq1688Rate > 0 && ctx.Page!.Url.Contains("m.p4psearch.1688.com")))
            //    {
            //        await ClearPageCloseBtn(ctx.Page!, ctx.CdpSession!);
            //        await ClearSuccessTipNewCloseNew(ctx.Page!, ctx.CdpSession!);
            //        await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
            //        await HumanScrollHelper.TouchPageLongScrollAsync(
            //        ctx.Page!,
            //        ctx.CdpSession!,
            //        scrollCount: CommonHelper.RandomRange(0, 3),
            //        direction: PageScrollDirection.Up,
            //        cancellationToken: token);
            //        await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            //        var locator = ctx.Page.Locator("*:text-is('全部商品')");
            //        var locator_count = await locator.CountAsync();
            //        if (locator_count == 0)
            //        {
            //            locator = ctx.Page.Locator("*:text-is('进店看看')");
            //            locator_count = await locator.CountAsync();
            //        }
            //        if (locator_count == 0)
            //        {
            //            locator = ctx.Page.Locator("*:text-is('进店看厂')");
            //            locator_count = await locator.CountAsync();
            //        }
            //        if (locator_count == 0)
            //        {
            //            locator = ctx.Page.Locator(".recommend-container");
            //            locator_count = await locator.CountAsync();
            //        }

            //        if (locator_count > 0)
            //        {
            //            await locator.ScrollIntoViewIfNeededAsync();
            //            await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            //            var clickRes2 = await ClickElementAndDetectNavigationAsync(ctx, locator.First, token);
            //            if (clickRes2.Navigated)
            //            {
            //                await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
            //                await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
            //                await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
            //                await HumanScrollHelper.TouchPageLongScrollAsync(
            //                ctx.Page!,
            //                ctx.CdpSession!,
            //                scrollCount: CommonHelper.RandomRange(0, 3),
            //                direction: PageScrollDirection.Up,
            //                cancellationToken: token);
            //                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            //                locator = ctx.Page
            //                    .Locator("body,iframe")
            //                    .Filter(new() { Visible = true })
            //                    .First;

            //                if (await locator.CountAsync() > 0)
            //                {
            //                    var clickRes3 = await ClickElementAndDetectNavigationAsync(ctx, locator.First, token);
            //                    if (clickRes3.Navigated)
            //                    {
            //                        await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
            //                        await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
            //                        await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
            //                        await HumanScrollHelper.TouchPageLongScrollAsync(
            //                        ctx.Page!,
            //                        ctx.CdpSession!,
            //                        scrollCount: CommonHelper.RandomRange(0, 3),
            //                        direction: PageScrollDirection.Up,
            //                        cancellationToken: token);
            //                        await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            //                    }

            //                }
            //            }

            //        }
            //        else
            //        {
            //            locator = ctx.Page
            //                .Locator("body,iframe")
            //                .Filter(new() { Visible = true })
            //                .First;

            //            if (await locator.CountAsync() > 0)
            //            {
            //                var clickRes3 = await ClickElementAndDetectNavigationAsync(ctx, locator.First, token);
            //                if (clickRes3.Navigated)
            //                {
            //                    await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
            //                    await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
            //                    await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
            //                    await HumanScrollHelper.TouchPageLongScrollAsync(
            //                    ctx.Page!,
            //                    ctx.CdpSession!,
            //                    scrollCount: CommonHelper.RandomRange(0, 3),
            //                    direction: PageScrollDirection.Up,
            //                    cancellationToken: token);
            //                    await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            //                }

            //            }
            //        }
            //        return;
            //    }
            //}
            #endregion

            try
            {
                var metrics = _aggregator.GetLocalMetrics(ctx.Config.TaskId, "dsp_rfq1688", "dsp_rfq1688_click");
                if (metrics["dsp_rfq1688"] > 0)
                    LogWriteLine($"1688询价比率:{(metrics["dsp_rfq1688_click"] / (double)metrics["dsp_rfq1688"] * 100):N2}%");

                bool canClick = _appSettings.Rfq1688 && (_appSettings.Rfq1688Rate == 100
                    || metrics["dsp_rfq1688_click"] == 0
                    || ((metrics["dsp_rfq1688_click"] / (double)metrics["dsp_rfq1688"]) * 100 < _appSettings.Rfq1688Rate));

                if (!canClick)
                {
                    await TryHandleNoRfq1688Async(ctx, token);
                    return;
                }


                await Task.Delay(CommonHelper.RandomRange(3000, 5000), token);
                var el = ctx.Page!.Locator("#od_xst_phone_input_val_new,#new_od_xst_phone_input_val_new");
                if (await el.CountAsync() == 0)
                {
                    var queryBtn = ctx.Page.Locator(".queryBtnTitleTop");
                    if (await queryBtn.CountAsync() == 0)
                        queryBtn = ctx.Page.GetByText("立即询价");

                    if (await queryBtn.CountAsync() > 0)
                    {
                        await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, queryBtn.First, timeout: 1500);
                        await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                        el = ctx.Page.Locator("#od_xst_phone_input_val_new,#new_od_xst_phone_input_val_new");
                    }
                }

                if (await el.CountAsync() == 0)
                {
                    await TryHandleNoRfq1688Async(ctx, token);
                    return;
                }

                _aggregator.AddLocalMetric(ctx.Config.TaskId, "dsp_rfq1688_click");

                var phone = await _adeHelper.GetPhoneNumberAsync();
                if (string.IsNullOrWhiteSpace(phone))
                {
                    await TryHandleNoRfq1688Async(ctx, token);
                    return;
                }

                await el.First.FillAsync("");
                await Task.Delay(CommonHelper.RandomRange(50, 100), token);
                await el.First.PressSequentiallyAsync(phone);
                await Task.Delay(CommonHelper.RandomRange(1500, 2000), token);

                var answerContents = ctx.Page.Locator("div.new_answer_content span,div.answer_content span");
                if (await answerContents.CountAsync() > 0)
                {
                    int count = await answerContents.CountAsync();
                    var answer = answerContents.Nth(CommonHelper.RandomRange(0, count));
                    await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, answer.First, timeout: 1000);
                    await Task.Delay(CommonHelper.RandomRange(1500, 2000), token);
                }
                else
                {
                    var texts = new[]
                    {
                        "有没有现货","价格还有空间吗","什么时间发货","有活动吗","工厂在哪里","实物图是否一致",
                        "能否提供质检","可以寄样品给我吗","批发价是多少","可以开发票吧","这款支持一件代发吗","包邮吗"
                    };

                    el = ctx.Page.Locator("textarea#new_od_xst_msg_input_val_new_message,textarea#od_xst_msg_input_val_new_message");
                    if (await el.CountAsync() > 0)
                    {
                        await el.First.FillAsync("");
                        await Task.Delay(CommonHelper.RandomRange(50, 100), token);
                        await el.First.PressSequentiallyAsync(texts[CommonHelper.RandomRange(0, texts.Length)]);
                        await Task.Delay(CommonHelper.RandomRange(1500, 2000), token);
                    }
                }

                el = ctx.Page.Locator(".new_successTipNew_wangwang_new,.successTipNew_call_new");
                if (await el.CountAsync() > 0)
                {
                    try
                    {
                        await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, el.First, timeout: 2000);
                        await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);

                        var sms = ctx.Page.GetByText("获取验证码");
                        if (await sms.CountAsync() > 0)
                        {
                            var close1 = ctx.Page.Locator(".successTipNew_close_new,.newSuccessTipNew_close_new");
                            if (await close1.CountAsync() > 0)
                                await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, close1.First, timeout: 1500);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch { }

                    var close2 = ctx.Page.Locator(".successTipNew_close_new,.newSuccessTipNew_close_new,.newCloseIcon_content");
                    if (await close2.CountAsync() > 0)
                        await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, close2.First, timeout: 1500);
                }
                await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
                await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
                await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                await HumanScrollHelper.TouchPageLongScrollAsync(
                ctx.Page!,
                ctx.CdpSession!,
                scrollCount: CommonHelper.RandomRange(0, 3),
                direction: PageScrollDirection.Up,
                cancellationToken: token);
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);


                var locator = ctx.Page.Locator("*:text-is('进店看看')");
                var locator_count = await locator.CountAsync();
                if (locator_count == 0)
                {
                    locator = ctx.Page.Locator("*:text-is('进店看厂')");
                    locator_count = await locator.CountAsync();
                }
                if (locator_count == 0)
                {
                    locator = ctx.Page.Locator("*:text-is('全部商品')");
                    locator_count = await locator.CountAsync();
                }
                if (locator_count == 0)
                {
                    locator = ctx.Page.Locator(".recommend-container");
                    locator_count = await locator.CountAsync();
                }

                if (locator_count > 0)
                {
                    await locator.ScrollIntoViewIfNeededAsync();
                    await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                    var clickRes2 = await ClickAndDetectNavigationAsync(ctx, locator.First, token);
                    if (clickRes2.Navigated)
                    {
                        await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
                        await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
                        await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                        await HumanScrollHelper.TouchPageLongScrollAsync(
                        ctx.Page!,
                        ctx.CdpSession!,
                        scrollCount: CommonHelper.RandomRange(0, 3),
                        direction: PageScrollDirection.Up,
                        cancellationToken: token);
                        await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                        locator = ctx.Page
                            .Locator("body,iframe")
                            .Filter(new() { Visible = true })
                            .First;

                        if (await locator.CountAsync() > 0)
                        {
                            var clickRes3 = await ClickAndDetectNavigationAsync(ctx, locator.First, token);
                            if (clickRes3.Navigated)
                            {
                                await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
                                await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
                                await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                                await HumanScrollHelper.TouchPageLongScrollAsync(
                                ctx.Page!,
                                ctx.CdpSession!,
                                scrollCount: CommonHelper.RandomRange(0, 3),
                                direction: PageScrollDirection.Up,
                                cancellationToken: token);
                                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                            }

                        }
                    }

                }
                else
                {
                    locator = ctx.Page
                        .Locator("body,iframe")
                        .Filter(new() { Visible = true })
                        .First;

                    if (await locator.CountAsync() > 0)
                    {
                        var clickRes3 = await ClickAndDetectNavigationAsync(ctx, locator.First, token);
                        if (clickRes3.Navigated)
                        {
                            await ClearPageCloseBtn(ctx.Page, ctx.CdpSession!);
                            await ClearSuccessTipNewCloseNew(ctx.Page, ctx.CdpSession!);
                            await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                            await HumanScrollHelper.TouchPageLongScrollAsync(
                            ctx.Page!,
                            ctx.CdpSession!,
                            scrollCount: CommonHelper.RandomRange(0, 3),
                            direction: PageScrollDirection.Up,
                            cancellationToken: token);
                            await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                        }

                    }
                }














            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch { }
        }

        private async Task TryHandleQianhuFormAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!ctx.Page!.Url.StartsWith("https://qianhu.wejianzhan.com/"))
                return;

            try
            {
                var phone = await _adeHelper.GetPhoneNumberAsync();
                if (string.IsNullOrWhiteSpace(phone))
                    return;

                var surname = _nameGenerator.GetDisplayName(phone);

                var inputName = ctx.Page.Locator("input[placeholder='请输入您的称呼']").First;
                if (await inputName.CountAsync() > 0)
                {
                    await inputName.FillAsync("");
                    await inputName.PressSequentiallyAsync(surname);
                }

                await Task.Delay(CommonHelper.RandomRange(300, 500), token);

                var inputPhone = ctx.Page.Locator("input[placeholder='请输入手机号']").First;
                if (await inputPhone.CountAsync() > 0)
                {
                    await inputPhone.FillAsync("");
                    await inputPhone.PressSequentiallyAsync(phone);
                }

                var radio = ctx.Page.Locator(".phone-agrement-container .phone-agrement-radio");
                if (await radio.CountAsync() > 0)
                    await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, radio.First);

                var btnSubmit = ctx.Page.Locator("div:has-text('免费领票')").First;
                if (await btnSubmit.CountAsync() > 0)
                {
                    await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, btnSubmit);
                    await Task.Delay(CommonHelper.RandomRange(3000, 5000), token);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch { }
        }

        private async Task TryHandleLouisvuittonAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            if (!ctx.Page!.Url.StartsWith("https://www.louisvuitton.cn"))
                return;

            try
            {
                await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                var cookieBtn = await BDInsightHelper.WaitVisibleLocatorAsync(new[]
                {
                    ctx.Page.GetByText("同意全部第三方Cookie", new() { Exact = true }),
                    ctx.Page.GetByRole(AriaRole.Button, new() { Name = "同意全部第三方Cookie" }),
                    ctx.Page.Locator("button").Filter(new() { HasTextString = "同意全部第三方Cookie" }),
                }, token, timeoutMs: 10000);
                if (cookieBtn != null)
                {
                    await Task.Delay(CommonHelper.RandomRange(300, 600), token);
                    await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, cookieBtn);
                }
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);


                await HumanScrollHelper.TouchPageLongScrollAsync(
                ctx.Page!,
                ctx.CdpSession!,
                scrollCount: CommonHelper.RandomRange(2, 4),
                direction: PageScrollDirection.Up,
                cancellationToken: token);
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);

                ClickResult? clickResult = null;
                var options = new ClickAreaOptions
                {
                    MinXPercent = 0.1,
                    MaxXPercent = 0.9,
                    MinYPercent = 0.30,
                    MaxYPercent = 0.70,
                    StrictPreferredArea = false,
                    MaxCount = 50
                };
                var nodes = await PlaywrightClickableHelper.GetClickableNodesAsync(ctx.Page, options);
                if (nodes.Count() > 0)
                {
                    foreach (var node in nodes.Take(2).OrderByDescending(g => Guid.NewGuid()))
                    {
                        if (string.IsNullOrWhiteSpace(node.Selector))
                            continue;
                        try
                        {
                            var locator = ctx.Page.Locator(node.Selector).First;
                            if (await locator.CountAsync() == 0)
                                continue;
                            clickResult = await ClickAndDetectNavigationAsync(ctx, locator.First, token);
                            if (clickResult.Navigated)
                            {
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                if (clickResult != null && clickResult.Navigated)
                {
                    await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);

                    await HumanScrollHelper.TouchPageLongScrollAsync(
                      ctx.Page!,
                      ctx.CdpSession!,
                      scrollCount: CommonHelper.RandomRange(2, 4),
                      direction: PageScrollDirection.Up,
                      cancellationToken: token);

                    await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                    nodes = await PlaywrightClickableHelper.GetClickableNodesAsync(ctx.Page, options);
                    if (nodes.Count() > 0)
                    {
                        foreach (var node in nodes.Take(3).OrderByDescending(g => Guid.NewGuid()))
                        {
                            if (string.IsNullOrWhiteSpace(node.Selector))
                                continue;
                            try
                            {
                                var locator = ctx.Page.Locator(node.Selector).First;
                                if (await locator.CountAsync() == 0)
                                    continue;
                                clickResult = await ClickAndDetectNavigationAsync(ctx, locator.First, token);
                                if (clickResult.Navigated)
                                {
                                    break;
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }

            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch { }
        }

        private async Task TryHandleAllAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (ctx.Page!.Url.Contains("taobao.com") || ctx.Page!.Url.Contains("1688.com") || ctx.Page!.Url.Contains("jd.com") || ctx.Page!.Url.Contains("baidu.com"))
                return;


            try
            {
                await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                var acceptBtn = ctx.Page!.Locator(
                      "button:visible, a:visible, [role='button']:visible, input[type='button']:visible, input[type='submit']:visible, div:visible, span:visible"
                  ).Filter(new()
                  {
                      HasTextRegex = new Regex(
                          @"同意|接受|允许|我同意|我接受|允许全部|全部接受|全部同意|确认|继续|知道了|Agree|Accept|Allow|Accept All|Allow All|I Agree|I Accept|Consent|Got it|Continue|Accept Cookies|Allow Cookies",
                          RegexOptions.IgnoreCase
                      )
                  }).First;
                if (await acceptBtn.CountAsync() > 0)
                {
                    await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                    await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, acceptBtn.First);
                }
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);

                await HumanScrollHelper.TouchPageLongScrollAsync(
                ctx.Page!,
                ctx.CdpSession!,
                scrollCount: CommonHelper.RandomRange(2, 4),
                direction: PageScrollDirection.Up,
                cancellationToken: token);
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                ClickResult? clickResult = null;
                var bd_locator = ctx.Page.Locator("div.baidu-ad").Filter(new() { Visible = true });
                var bd_locator_count = await bd_locator.CountAsync();
                if (bd_locator_count > 0)
                {
                    var nodes = Enumerable.Range(0, bd_locator_count)
                     .OrderBy(_ => Guid.NewGuid())
                     .Select(i => bd_locator.Nth(i))
                     .ToList();
                    foreach (var node in nodes)
                    {
                        await node.ScrollIntoViewIfNeededAsync();
                        var box = await node.BoundingBoxAsync();
                        clickResult = await ClickAndDetectNavigationAsync(ctx, node, token);
                        if (clickResult.Navigated)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    if (ctx.Page.Frames.Count > 0)
                    {
                        foreach (var frame in ctx.Page.Frames)
                        {
                            if (!frame.Url.Contains("baidu.com"))
                                continue;
                            var el = await frame.FrameElementAsync();
                            if (el == null)
                                continue;
                            var box = await el.BoundingBoxAsync();
                            if (box == null || box.Width <= 0 || box.Height <= 0)
                                continue;

                            clickResult = await ClickAndDetectNavigationAsync(ctx, el, token);
                            if (clickResult.Navigated)
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        var options = new ClickAreaOptions
                        {
                            MinXPercent = 0.1,
                            MaxXPercent = 0.9,
                            MinYPercent = 0.30,
                            MaxYPercent = 0.70,
                            StrictPreferredArea = false,
                            MaxCount = 50
                        };
                        var nodes = await PlaywrightClickableHelper.GetClickableNodesAsync(ctx.Page, options);
                        if (nodes.Count() > 0)
                        {
                            foreach (var node in nodes.Take(2).OrderByDescending(g => Guid.NewGuid()))
                            {
                                if (string.IsNullOrWhiteSpace(node.Selector))
                                    continue;
                                try
                                {
                                    var locator = ctx.Page.Locator(node.Selector).First;
                                    if (await locator.CountAsync() == 0)
                                        continue;
                                    clickResult = await ClickAndDetectNavigationAsync(ctx, locator.First, token);
                                    if (clickResult.Navigated)
                                    {
                                        break;
                                    }
                                }
                                catch
                                {
                                }
                            }
                        }
                    }

                }

                if (clickResult != null && clickResult.Navigated)
                {
                    await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);

                    await HumanScrollHelper.TouchPageLongScrollAsync(
                      ctx.Page!,
                      ctx.CdpSession!,
                      scrollCount: CommonHelper.RandomRange(0, 3),
                      direction: PageScrollDirection.Up,
                      cancellationToken: token);

                    await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                    var options = new ClickAreaOptions
                    {
                        MinXPercent = 0.1,
                        MaxXPercent = 0.9,
                        MinYPercent = 0.30,
                        MaxYPercent = 0.70,
                        StrictPreferredArea = false,
                        MaxCount = 50
                    };

                    var nodes = await PlaywrightClickableHelper.GetClickableNodesAsync(ctx.Page, options);
                    if (nodes.Count() > 0)
                    {
                        foreach (var node in nodes.Take(3).OrderByDescending(g => Guid.NewGuid()))
                        {
                            if (string.IsNullOrWhiteSpace(node.Selector))
                                continue;
                            try
                            {
                                var locator = ctx.Page.Locator(node.Selector).First;
                                if (await locator.CountAsync() == 0)
                                    continue;
                                clickResult = await ClickAndDetectNavigationAsync(ctx, locator.First, token);
                                if (clickResult.Navigated)
                                {
                                    break;
                                }
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch { }
        }

        #endregion

        /// <summary>
        /// 处理页面元素
        /// </summary>
        /// <param name="page"></param>
        /// <param name="cdpSession"></param>
        /// <param name="token"></param>

        public void ProcessingPageElementTask(IPage page, ICDPSession cdpSession, CancellationToken token)
        {
            _ = Task.Run(async () =>
            {
                int redoTryCount = 0;

                while (redoTryCount++ < 10 && !token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(CommonHelper.RandomRange(800, 1200), token);

                        if (page == null || page.IsClosed)
                            break;

                        var closeBtn = page.Locator(".androidOpenModal .closeBtn, .iosOpenModal .closeIcon");
                        if (await closeBtn.CountAsync() <= 0)
                            continue;


                        //else if (await page.Locator(".enquiryFormContentnew").CountAsync() > 0 && await page.Locator(".successTipNew_close_new").CountAsync() > 0)
                        //{
                        //    var closeBtn = page.Locator(".successTipNew_close_new");
                        //    if (await closeBtn.CountAsync() > 0)
                        //    {
                        //        await CDPHelper.MouseClickAsync(page, cdpSession, closeBtn);
                        //        break;
                        //    }
                        //}

                        var target = closeBtn.First;
                        if (!await target.IsVisibleAsync())
                            continue;

                        if (page.IsClosed)
                            break;

                        await CDPHelper.MouseClickAsync(page, cdpSession, target);
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (PlaywrightException ex) when (IsClosedPlaywrightException(ex))
                    {
                        LogWriteLine($"{this.Title}:ProcessingPageElementTask 页面已关闭: {ex.Message}");
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogWriteLine($"{this.Title}:ProcessingPageElementTask异常: {ex.Message}");
                        break;
                    }
                }
            }, token);
        }

        /// <summary>
        /// 清除1688APP下载
        /// </summary>
        /// <param name="page"></param>
        /// <param name="cdpSession"></param>
        /// <returns></returns>
        private async Task ClearPageCloseBtn(IPage page, ICDPSession cdpSession)
        {
            try
            {
                var closeBtn = page.Locator(".androidOpenModal .closeBtn, .iosOpenModal .closeIcon");
                if (await closeBtn.CountAsync() > 0)
                {
                    var target = closeBtn.First;
                    if (await target.IsVisibleAsync())
                    {
                        await CDPHelper.MouseClickAsync(page, cdpSession, target);
                    }
                }
            }
            catch (Exception)
            {

            }
        }

        /// <summary>
        /// 清除1688询价对话框
        /// </summary>
        /// <param name="page"></param>
        /// <param name="cdpSession"></param>
        /// <returns></returns>
        private async Task ClearSuccessTipNewCloseNew(IPage page, ICDPSession cdpSession)
        {
            try
            {
                var closeBtn = page.Locator(".successTipNew_close_new,.newSuccessTipNew_close_new,.androidOpenModal .closeBtn, .iosOpenModal .closeIcon");
                if (await closeBtn.CountAsync() > 0)
                {
                    var target = closeBtn.First;
                    if (await target.IsVisibleAsync())
                    {
                        await CDPHelper.MouseClickAsync(page, cdpSession, target);
                    }
                }
            }
            catch (Exception)
            {

            }
        }

        /// <summary>
        /// 点击节点并检测页面是否跳转
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="target"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<ClickResult> ClickAndDetectNavigationAsync(WorkerRunContext ctx, ILocator target, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                ctx.PagesCount = ctx.Context!.Pages.Count;
                ctx.CurrentPageUrl = ctx.Page!.Url;
                await CDPHelper.MouseClickAsync(ctx.Page!, ctx.CdpSession!, target);
                await Task.Delay(CommonHelper.RandomRange(50, 100), token);
                try
                {
                    await ctx.Page!.WaitForURLAsync(
                        u => !u.Equals(ctx.CurrentPageUrl),
                        new PageWaitForURLOptions
                        {
                            WaitUntil = WaitUntilState.DOMContentLoaded,
                            Timeout = 10000
                        });
                }
                catch (TimeoutException) { }
                if (ctx.Context.Pages.Count > ctx.PagesCount)
                {
                    ctx.Page = ctx.Context.Pages[^1];
                    ctx.CdpSession = await ctx.CdpManager!.GetOrCreateSessionAsync(ctx.Page);
                    await CDPHelper.InitCDPSession(ctx.CdpSession, ctx.Config.MaxTouchPoints);
                    return ClickResult.SuccessNewPage();
                }
                if (!ctx.Page.Url.StartsWith(ctx.CurrentPageUrl))
                    return ClickResult.SuccessSamePage();

                return ClickResult.NoNavigation();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return ClickResult.Fail();
            }
        }
        public async Task<ClickResult> ClickAndDetectNavigationAsync(WorkerRunContext ctx, IElementHandle handle, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                ctx.PagesCount = ctx.Context!.Pages.Count;
                ctx.CurrentPageUrl = ctx.Page!.Url;
                await CDPHelper.MouseClickAsync(ctx.Page!, ctx.CdpSession!, handle);
                await Task.Delay(CommonHelper.RandomRange(50, 100), token);
                try
                {
                    await ctx.Page!.WaitForURLAsync(
                        u => !u.Equals(ctx.CurrentPageUrl),
                        new PageWaitForURLOptions
                        {
                            WaitUntil = WaitUntilState.DOMContentLoaded,
                            Timeout = 10000
                        });
                }
                catch (TimeoutException) { }

                if (ctx.Context.Pages.Count > ctx.PagesCount)
                {
                    ctx.Page = ctx.Context.Pages[^1];
                    ctx.CdpSession = await ctx.CdpManager!.GetOrCreateSessionAsync(ctx.Page);
                    await CDPHelper.InitCDPSession(ctx.CdpSession, ctx.Config.MaxTouchPoints);
                    return ClickResult.SuccessNewPage();
                }

                if (!ctx.Page.Url.StartsWith(ctx.CurrentPageUrl))
                    return ClickResult.SuccessSamePage();

                return ClickResult.NoNavigation();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return ClickResult.Fail();
            }
        }
        public async Task<ClickResult> TryRandomLinkClickAsync(WorkerRunContext ctx, string selector, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var locators = ctx.Page!.Locator(selector);
            int count = await locators.CountAsync();

            var clickable = new List<ILocator>();
            for (int i = 0; i < count; i++)
            {
                token.ThrowIfCancellationRequested();

                var link = locators.Nth(i);
                if (await link.IsVisibleAsync() && await link.IsEnabledAsync())
                    clickable.Add(link);
            }

            foreach (var link in clickable.OrderBy(_ => Guid.NewGuid()))
            {
                token.ThrowIfCancellationRequested();

                await link.ScrollIntoViewIfNeededAsync();
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);

                var result = await ClickAndDetectNavigationAsync(ctx, link.First, token);
                if (result.Navigated)
                    return result;
            }

            return ClickResult.NoNavigation();
        }
        public override async Task<(bool, bool, int)> ExecuteWorkerAsync(string uniqueId, JObject taskArgs, CancellationToken token)
        {
            WorkerRunContext? ctx = null;
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token);


            try
            {

                var config = BuildTaskConfig(uniqueId, taskArgs, linkedCts);
                ctx = new WorkerRunContext(config)
                {
                    LandingDispatcher = new LandingPageStrategyDispatcher(new ILandingPageStrategy[]
                    {
                        new BaiduMobPageStrategy(this),
                        new BaiduAdenPageStrategy(this),

                        new UMobLandingPageStrategy(this),
                        new AiSiteLandingPageStrategy(this),
                        new AiStudyLandingPageStrategy(this),
                        new DefaultLandingPageStrategy(this),
                    })
                };
                _activeContexts[uniqueId] = ctx;



                this.QTPExecuteStart(config.TaskId);
                LogWriteLine($"{this.Title}:ExecuteWorker:Start");
                ctx.Playwright = await _playwrightProvider.GetAsync();
                linkedCts.Token.ThrowIfCancellationRequested();

                var browser = await LaunchBrowserAsync(ctx, linkedCts.Token);
                if (browser == null)
                {
                    if (ctx.ProxyFailed)
                    {
                        LogWriteLine($"{this.Title}:ExecuteWorker:浏览器/CDP建立失败，疑似代理异常: {ctx.ProxyFailedReason}");
                    }
                    else
                    {
                        LogWriteLine($"{this.Title}:ExecuteWorker:浏览器启动或CDP连接失败");
                    }

                    return (false, false, 0);
                }
                ctx.Browser = browser;
                if (!ctx.Browser.IsConnected)
                {
                    ctx.ProxyFailed = true;
                    ctx.ProxyFailedReason ??= "Browser.IsConnected == false";
                    LogWriteLine($"{this.Title}:ExecuteWorker:Browser未连接: {ctx.ProxyFailedReason}");
                    return (false, false, 0);
                }
                if (ctx.Browser.Contexts == null || ctx.Browser.Contexts.Count == 0)
                {
                    ctx.ProxyFailed = true;
                    ctx.ProxyFailedReason ??= "Browser.Contexts.Count == 0";
                    LogWriteLine($"{this.Title}:ExecuteWorker:Browser无可用Context: {ctx.ProxyFailedReason}");
                    return (false, false, 0);
                }
                ctx.CdpManager = new CDPSessionManager(ctx.Context!);
                await ConfigureContextAsync(ctx, linkedCts.Token);
                await AttachLifecycleEventsAsync(ctx, linkedCts.Token);
                if (ctx.ProxyFailed)
                {
                    LogWriteLine($"{this.Title}:ExecuteWorker:初始化阶段已判定代理异常: {ctx.ProxyFailedReason}");
                    return (false, ctx.PageTriggerClick, ctx.PageAdsCount);
                }
                if (ctx.PageCrashed)
                {
                    LogWriteLine($"{this.Title}:ExecuteWorker:初始化阶段页面崩溃: {ctx.LastFailureReason}");
                    return (false, ctx.PageTriggerClick, ctx.PageAdsCount);
                }

                var ok = await RunMainFlowAsync(ctx, linkedCts.Token);
                if (!ok)
                {
                    if (ctx.ProxyFailed)
                    {
                        LogWriteLine($"{this.Title}:ExecuteWorker:任务失败，代理异常: {ctx.ProxyFailedReason}");
                    }
                    else if (ctx.PageCrashed)
                    {
                        LogWriteLine($"{this.Title}:ExecuteWorker:任务失败，页面崩溃: {ctx.LastFailureReason}");
                    }
                    else if (!string.IsNullOrWhiteSpace(ctx.LastFailureReason))
                    {
                        LogWriteLine($"{this.Title}:ExecuteWorker:任务失败: {ctx.LastFailureReason}");
                    }
                }
                return (ok, ctx.PageTriggerClick, ctx.PageAdsCount);
            }
            catch (OperationCanceledException)
            {
                if (ctx?.ProxyFailed == true)
                {
                    LogWriteLine($"{this.Title}:ExecuteWorker:Canceled(代理异常): {ctx.ProxyFailedReason}");
                    return (false, ctx.PageTriggerClick, ctx.PageAdsCount);
                }

                if (ctx?.PageCrashed == true)
                {
                    LogWriteLine($"{this.Title}:ExecuteWorker:Canceled(页面崩溃): {ctx.LastFailureReason}");
                    return (false, ctx.PageTriggerClick, ctx.PageAdsCount);
                }

                if (ctx != null && !string.IsNullOrWhiteSpace(ctx.LastFailureReason))
                {
                    LogWriteLine($"{this.Title}:ExecuteWorker:Canceled: {ctx.LastFailureReason}");
                    return (false, ctx.PageTriggerClick, ctx.PageAdsCount);
                }

                LogWriteLine($"{this.Title}:ExecuteWorker:Canceled");
                return (false, ctx?.PageTriggerClick ?? false, ctx?.PageAdsCount ?? 0);
            }
            catch (PlaywrightException ex)
            {
                if (ctx != null)
                    ctx.LastFailureReason = ex.Message;

                if (ctx?.ProxyFailed == true)
                {
                    LogWriteLine($"{this.Title}:ExecuteWorker:PlaywrightException(代理异常): {ctx.ProxyFailedReason}");
                    return (false, ctx.PageTriggerClick, ctx.PageAdsCount);
                }

                if (ctx?.PageCrashed == true)
                {
                    LogWriteLine($"{this.Title}:ExecuteWorker:PlaywrightException(页面崩溃): {ctx.LastFailureReason}");
                    return (false, ctx.PageTriggerClick, ctx.PageAdsCount);
                }

                LogWriteLine($"{this.Title}:ExecuteWorker:PlaywrightException: {ex}");
                return (false, ctx?.PageTriggerClick ?? false, ctx?.PageAdsCount ?? 0);
            }
            catch (Exception ex)
            {
                if (ctx != null)
                    ctx.LastFailureReason = ex.Message;

                if (ctx?.ProxyFailed == true)
                {
                    LogWriteLine($"{this.Title}:ExecuteWorker:异常(代理异常): {ctx.ProxyFailedReason}");
                    return (false, ctx.PageTriggerClick, ctx.PageAdsCount);
                }

                if (ctx?.PageCrashed == true)
                {
                    _activeContexts.TryRemove(uniqueId, out _);
                    LogWriteLine($"{this.Title}:ExecuteWorker:异常(页面崩溃): {ctx.LastFailureReason}");
                    return (false, ctx.PageTriggerClick, ctx.PageAdsCount);
                }

                LogWriteLine(ex.ToString());
                return (false, ctx?.PageTriggerClick ?? false, ctx?.PageAdsCount ?? 0);
            }
            finally
            {
                try
                {
                    if (!linkedCts.IsCancellationRequested)
                        linkedCts.Cancel();
                }
                catch
                {

                }

                if (ctx != null)
                {
                    try
                    {
                        await ForceCleanupSessionAsync(ctx, uniqueId);
                    }
                    finally
                    {
                        _activeContexts.TryRemove(uniqueId, out _);
                    }
                }
            }
        }
    }
}
