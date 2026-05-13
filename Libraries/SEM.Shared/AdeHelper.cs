using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SEM.Extensions;
using SEM.Infrastructure;
using SEM.Models;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;


namespace SEM
{
    public class AdeHelper
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly AppSettings _appSettings;
        private readonly ILogger _logger;
        private readonly AdeOptions _options;

        public static HttpClient client = new HttpClient();
        public const string _apiVersion = "_v2";
        public AdeHelper(IHttpClientFactory httpClientFactory, AppSettings appSettings, ILogger<AdeHelper> logger, IOptions<AdeOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _appSettings = appSettings;
            _logger = logger;
            _options = options.Value;
        }

        /// <summary>
        /// 获取任务
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public async Task<string?> GetTaskAsync(string address, CancellationToken token = default)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            try
            {
                using var response = await client.GetAsync(address, token);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync(token);
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, $"GetTaskAsync request failed : {httpEx.Message}");
            }
            catch (TaskCanceledException cancelEx) when (!token.IsCancellationRequested)
            {
                _logger.LogError(cancelEx, $"GetTaskAsync request timeout : {cancelEx.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"GetTaskAsync Unexpected : {ex.Message}");
            }

            return null;
        }

        #region 系统设备
        private static ConcurrentQueue<JToken> ANDROID_QUEUE = new();
        private static ConcurrentQueue<JToken> iOS_QUEUE = new();
        private readonly SemaphoreSlim iOS_SIGNAL = new(1, 1);
        private readonly SemaphoreSlim ANDROID_SIGNAL = new(1, 1);
        private async Task<string?> GetDevByOSInternal(OSType os, int count)
        {
            try
            {
                var devApiUrl = _appSettings.DevApiUrl;
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
                var type = os == OSType.IOS ? "ios" : os == OSType.PC ? "win" : "android";

                var url = $"{devApiUrl}?type={type}&count={count}&t={System.DateTime.Now.Ticks}";
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
            }
            return null;
        }
        public async Task<JToken?> GetDevByOS(OSType os, int count = 5)
        {
            (ConcurrentQueue<JToken> devs, SemaphoreSlim sem) =
                os == OSType.IOS ?
                (iOS_QUEUE, iOS_SIGNAL) :
                (ANDROID_QUEUE, ANDROID_SIGNAL);

            if (devs.TryDequeue(out var cached))
            {
                return cached;
            }
            await sem.WaitAsync();
            try
            {
                if (devs.TryDequeue(out cached))
                {
                    return cached;
                }
                var text = await GetDevByOSInternal(os, count);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }
                var json = JObject.Parse(text);
                var data = json["data"] as JArray;
                if (data == null || data.Count == 0)
                {
                    return null;
                }
                JToken first = data[0];
                for (int i = 1; i < data.Count; i++)
                {
                    devs.Enqueue(data[i]);
                }

                return first;
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetDevByOS error: {ex.Message}");
                return null;
            }
            finally
            {
                sem.Release();
            }
        }

        public OSType GetOS(string devClientId)
        {
            return devClientId switch
            {
                "7" => OSType.PC,
                "4" => OSType.IOS,
                _ => OSType.ANDROID
            };
        }
        public async Task<JToken?> GetDeviceAsync(OSType os, int count)
        {
            int retry = 0;
            JToken? dev = null;
            while (retry++ < 5)
            {
                dev = await GetDevByOS(os, count);
                if (dev != null) break;
            }
            return dev;
        }

        #endregion

        #region  任务状态统计&更新

        /// <summary>
        /// 更新任务状态
        /// </summary>
        /// <param name="taskId">任务 ID</param>
        /// <param name="metrics">指标字典，例如 start, dsp, click, success</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        public async Task<JToken?> UpdateTaskStatusAsync(int taskId, Dictionary<string, long> metrics, CancellationToken token = default)
        {
            try
            {
                var host = await CommonHelper.GetHostAsync();
                var baseUrl = new Uri(_appSettings.TaskApiUrl).GetLeftPart(UriPartial.Authority);
                StringBuilder builder = new StringBuilder(baseUrl);
                builder.Append($"/api{_apiVersion}/task-status.php?action=update_task&_t={System.DateTime.Now.Ticks}");
                var bidRequest = new
                {
                    id = taskId,
                    host = host,
                    version = _options.AppVersion,
                    metrics = metrics
                };
                var postData = JsonConvert.SerializeObject(bidRequest);
                using var content = new StringContent(postData, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(builder.ToString(), content, token);
                response.EnsureSuccessStatusCode();
                return JObject.Parse(await response.Content.ReadAsStringAsync(token));
            }
            catch (OperationCanceledException)
            {
                // 请求被取消，安全退出
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpdateTaskStatusAsync TaskId={taskId} failed: {ex.Message}");
            }
            return null;
        }
        /// <summary>
        /// 更新任务状态
        /// </summary>
        /// <param name="taskId"></param>
        /// <param name="type"></param>
        /// <param name="count"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<JToken?> UpdateTaskStatusAsync(int taskId, StateType type = StateType.Start, int count = 1, CancellationToken token = default)
        {
            var metrics = new Dictionary<string, long>
            {
                [type.FullName()] = count
            };
            return await UpdateTaskStatusAsync(taskId, metrics, token);
        }

        /// <summary>
        /// 获取当前任务的状态
        /// </summary>
        /// <param name="taskId"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<JToken?> GetTaskStatusAsync(int taskId, CancellationToken token = default)
        {
            try
            {
                var host = await CommonHelper.GetHostAsync();
                var baseUrl = new Uri(_appSettings.TaskApiUrl).GetLeftPart(UriPartial.Authority);
                using var response = await client.GetAsync($"{baseUrl}/api{_apiVersion}/task-status.php?action=task_status&id={taskId}&host={System.Web.HttpUtility.UrlEncode(host)}&_t={System.DateTime.Now.Ticks}", token);
                response.EnsureSuccessStatusCode();
                return JObject.Parse(await response.Content.ReadAsStringAsync(token));
            }
            catch (OperationCanceledException)
            {
                // 请求被取消，安全退出
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetTaskStatusAsync TaskId={taskId} failed: {ex.Message}");
            }
            return null;
        }




        #endregion

        #region  主机状态统计&更新

        private static string GetProxyHostSafely(string? proxyIpUrl)
        {
            if (string.IsNullOrWhiteSpace(proxyIpUrl))
                return string.Empty;

            return Uri.TryCreate(proxyIpUrl, UriKind.Absolute, out var uri)
                ? uri.Host
                : string.Empty;
        }


        /// <summary>
        /// 更新主机状态
        /// </summary>
        /// <param name="metrics"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<JToken?> UpdateHostStatusAsync(Dictionary<string, long> metrics, CancellationToken token = default)
        {
            metrics ??= new Dictionary<string, long>();
            string wordName = "default";
            var host = await CommonHelper.GetHostAsync();
            try
            {
                var baseUrl = new Uri(_appSettings.TaskApiUrl).GetLeftPart(UriPartial.Authority);
                StringBuilder builder = new StringBuilder(baseUrl);
                builder.Append($"/api{_apiVersion}/task-status.php?action=update_host&_t={System.DateTime.Now.Ticks}");
                var bidRequest = new
                {
                    host = host,
                    task = _appSettings.TaskName,
                    version = _options.AppVersion,
                    proxy = GetProxyHostSafely(_appSettings.ProxyIpUrl),
                    fullproxy = _appSettings.ProxyIpUrl,
                    wordname = wordName,
                    metrics = metrics,
                };
                var postData = JsonConvert.SerializeObject(bidRequest);
                using var content = new StringContent(postData, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(builder.ToString(), content, token);
                response.EnsureSuccessStatusCode();
                var resp = await response.Content.ReadAsStringAsync(token);
                return JObject.Parse(resp);
            }
            catch (OperationCanceledException)
            {
                // 请求被取消，安全退出
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpdateHostStatusAsync Host={host} failed: {ex.Message}");
            }
            return null;
        }
        /// <summary>
        /// 更新主机状态
        /// </summary>
        /// <param name="type"></param>
        /// <param name="count"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<JToken?> UpdateHostStatusAsync(StateType type = StateType.Start, int count = 1, CancellationToken token = default)
        {
            var metrics = new Dictionary<string, long>
            {
                [type.FullName()] = count
            };
            return await UpdateHostStatusAsync(metrics, token);
        }

        /// <summary>
        /// 获取今日主机状态
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<JToken?> GetHostTodayStatusAsync(CancellationToken token = default)
        {
            var host = await CommonHelper.GetHostAsync();
            try
            {
                var baseUrl = new Uri(_appSettings.TaskApiUrl).GetLeftPart(UriPartial.Authority);
                using var response = await client.GetAsync($"{baseUrl}/api{_apiVersion}/task-status.php?action=host_today_status&host={System.Web.HttpUtility.UrlEncode(host)}&_t={System.DateTime.Now.Ticks}", token);
                response.EnsureSuccessStatusCode();
                return JObject.Parse(await response.Content.ReadAsStringAsync(token));
            }
            catch (OperationCanceledException)
            {
                // 请求被取消，安全退出
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetHostTodayStatusAsync Host={host} failed: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// 获取当前时段主机状态
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<JToken?> GetHostHourStatusAsync(CancellationToken token = default)
        {
            var host = await CommonHelper.GetHostAsync();
            try
            {
                var baseUrl = new Uri(_appSettings.TaskApiUrl).GetLeftPart(UriPartial.Authority);
                using var response = await client.GetAsync($"{baseUrl}/api{_apiVersion}/task-status.php?action=host_hour_status&host={System.Web.HttpUtility.UrlEncode(host)}&_t={System.DateTime.Now.Ticks}", token);
                response.EnsureSuccessStatusCode();
                return JObject.Parse(await response.Content.ReadAsStringAsync(token));
            }
            catch (OperationCanceledException)
            {
                // 请求被取消，安全退出
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetHostHourStatusAsync Host={host} failed: {ex.Message}");
            }
            return null;
        }

        #endregion

        #region 代理状态统计&更新
        public async Task<JToken?> UpdateProxyIpStatAsync(int taskId, Dictionary<string, long> metrics, IEnumerable<string> ips, CancellationToken token = default)
        {
            try
            {
                var host = await CommonHelper.GetHostAsync();
                var baseUrl = new Uri(_appSettings.TaskApiUrl).GetLeftPart(UriPartial.Authority);
                StringBuilder builder = new StringBuilder(baseUrl);
                builder.Append($"/api{_apiVersion}/ip-status.php?action=request&id={taskId}&_t={System.DateTime.Now.Ticks}");
                var body = new Dictionary<string, object>
                {
                    ["metrics"] = metrics,
                    ["ips"] = ips
                };
                body["host"] = host;
                body["agency"] = _appSettings.ProxyIpUrl;

                var postData = JsonConvert.SerializeObject(body);
                using var content = new StringContent(postData, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(builder.ToString(), content, token);
                response.EnsureSuccessStatusCode();
                return JObject.Parse(await response.Content.ReadAsStringAsync(token));
            }
            catch (OperationCanceledException)
            {
                // 请求被取消，安全退出
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpdateProxyIpStatAsync TaskId={taskId} failed: {ex.Message}");
            }
            return null;
        }

        //taskId, ip, 1, token
        public async Task<JToken?> UpdateProxyIpConsumedIpAsync(int taskId, string ip, int count = 1, CancellationToken token = default)
        {
            try
            {
                var host = await CommonHelper.GetHostAsync();
                var baseUrl = new Uri(_appSettings.TaskApiUrl).GetLeftPart(UriPartial.Authority);
                StringBuilder builder = new StringBuilder(baseUrl);
                builder.Append($"/api{_apiVersion}/ip-status.php?action=consumed&id={taskId}&_t={System.DateTime.Now.Ticks}");
                var metrics = new
                {
                    ip = ip,
                    count = count,
                    host = host,
                    version = _options.AppVersion,
                    agency = _appSettings.ProxyIpUrl,
                };
                var postData = JsonConvert.SerializeObject(metrics);
                using var content = new StringContent(postData, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(builder.ToString(), content, token);
                response.EnsureSuccessStatusCode();
                return JObject.Parse(await response.Content.ReadAsStringAsync(token));
            }
            catch (OperationCanceledException)
            {
                // 请求被取消，安全退出
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpdateProxyIpConsumedIpAsync TaskId={taskId} failed: {ex.Message}");
            }
            return null;
        }

        public async Task<JToken?> UpdateProxyIpConsumedIpAsync(int taskId, IEnumerable<string> ips, int count = 1, CancellationToken token = default)
        {
            try
            {
                if (ips == null || !ips.Any())
                    return null;
                var host = await CommonHelper.GetHostAsync();
                var baseUrl = new Uri(_appSettings.TaskApiUrl).GetLeftPart(UriPartial.Authority);
                StringBuilder builder = new StringBuilder(baseUrl);
                builder.Append($"/api/ip-status.php?action=consumed&id={taskId}&_t={DateTime.Now.Ticks}");
                var metrics = new
                {
                    ips = ips.ToArray(),
                    host = host,
                    version = _options.AppVersion,
                    count = count,
                    agency = _appSettings.ProxyIpUrl
                };
                var postData = JsonConvert.SerializeObject(metrics);
                using var content = new StringContent(postData, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(builder.ToString(), content, token);
                response.EnsureSuccessStatusCode();

                return JObject.Parse(await response.Content.ReadAsStringAsync(token));
            }
            catch (OperationCanceledException)
            {
                // 请求被取消，安全退出
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpdateProxyIpConsumedIpAsync TaskId={taskId} failed: {ex.Message}");
            }

            return null;
        }


        #endregion

        #region 电话号码
        private readonly SemaphoreSlim _phone_signal = new(1, 1);
        private static ConcurrentQueue<string> _phone_list = new ConcurrentQueue<string>();
        private async Task<string?> GetPhoneNumberInternal(int count)
        {
            try
            {
                string baseUrl = new Uri(_appSettings.TaskApiUrl).GetLeftPart(UriPartial.Authority);
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
                var url = $"{baseUrl}/api{_apiVersion}/get_phone.php?count={count}&t={System.DateTime.Now.Ticks}";
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetPhoneNumberInternal error: {ex.Message}");
            }
            return null;
        }
        public async Task<string?> GetPhoneNumberAsync(CancellationToken token = default)
        {
            if (_phone_list.TryDequeue(out var cached))
            {
                return cached;
            }
            await _phone_signal.WaitAsync();
            try
            {
                if (_phone_list.TryDequeue(out cached))
                {
                    return cached;
                }
                var text = await GetPhoneNumberInternal(20);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }
                var json = JObject.Parse(text);
                var data = json["data"] as JArray;
                if (data == null || data.Count == 0)
                {
                    return null;
                }
                string first = data[0].Value<string>();
                for (int i = 1; i < data.Count; i++)
                {
                    _phone_list.Enqueue(data[i].Value<string>());
                }

                return first;
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetPhoneNumber error: {ex.Message}");
                return null;
            }
            finally
            {
                _phone_signal.Release();
            }
        }
        #endregion

        #region 对话

        private sealed class TalkCacheBucket
        {
            public SemaphoreSlim Signal { get; } = new(1, 1);
            public ConcurrentQueue<string> Queue { get; } = new();
        }

        private readonly ConcurrentDictionary<string, TalkCacheBucket> _talk_cache_map = new();

        private async Task<string?> GetTalkInternal(string name, int count)
        {
            try
            {
                string baseUrl = new Uri(_appSettings.TaskApiUrl).GetLeftPart(UriPartial.Authority);

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");

                var url = $"{baseUrl}/api{_apiVersion}/get_talk.php?name={Uri.EscapeDataString(name)}&count={count}&t={DateTime.Now.Ticks}";

                using HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTalkInternal error, name={Name}", name);
            }

            return null;
        }

        public async Task<string?> GetTalkAsync(string name, CancellationToken token = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;
            var bucket = _talk_cache_map.GetOrAdd(name, _ => new TalkCacheBucket());
            // 先无锁快速取一次
            if (bucket.Queue.TryDequeue(out var cached))
            {
                return cached;
            }
            await bucket.Signal.WaitAsync(token);
            try
            {
                // 双检，避免并发重复拉取
                if (bucket.Queue.TryDequeue(out cached))
                {
                    return cached;
                }

                var text = await GetTalkInternal(name, 20);
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                var json = JObject.Parse(text);
                var data = json["data"] as JArray;
                if (data == null || data.Count == 0)
                {
                    return null;
                }

                string? first = null;

                for (int i = 0; i < data.Count; i++)
                {
                    var item = data[i]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(item))
                        continue;

                    if (first == null)
                    {
                        first = item;
                    }
                    else
                    {
                        bucket.Queue.Enqueue(item);
                    }
                }

                return first;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTalkAsync error, name={Name}", name);
                return null;
            }
            finally
            {
                bucket.Signal.Release();
            }
        }
        #endregion
    }


}
