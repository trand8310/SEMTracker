using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SEM;
using SEM.Infrastructure;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;

namespace MainClient.Common
{
    public enum IPFormat
    {
        TXT = 1,
        JSON = 2,
    }
    public class IpEntity
    {
        public string value { get; set; } = string.Empty;
        public JToken json { get; set; }
        public IPFormat format { get; set; } = IPFormat.TXT;
    }



    public class IpHelper
    {
        private static JArray region_1;
        private static JArray region_2;
        private static JArray region_3;
        private static JArray region_4_1;
        private static JArray region_4_2;
        private static JArray region_ipzan;
        private static JArray region_51dail;
        private static JArray region_shenlong;

        static string[] delimiters = { "\r", "\n", System.Environment.NewLine };
        static SemaphoreSlim _mutex = new SemaphoreSlim(1);
        static IpHelper()
        {
            region_1 = (JArray)JsonConvert.DeserializeObject(Properties.Resources.region_1);
            region_2 = (JArray)JsonConvert.DeserializeObject(Properties.Resources.region_2);
            region_3 = (JArray)JsonConvert.DeserializeObject(Properties.Resources.region_3);
            region_4_1 = (JArray)JsonConvert.DeserializeObject(Properties.Resources.region_4_1);
            region_4_2 = (JArray)JsonConvert.DeserializeObject(Properties.Resources.region_4_2);
            region_ipzan = (JArray)JsonConvert.DeserializeObject(Properties.Resources.region_ipzan);
            region_51dail = (JArray)JsonConvert.DeserializeObject(Properties.Resources.region_51daili);
            region_shenlong = (JArray)JsonConvert.DeserializeObject(Properties.Resources.region_shenlong);
        }
        private readonly ILogger _logger;
        private readonly AppSettings _appSettings;
        private readonly IHttpClientFactory _httpClientFactory;
        public IpHelper(AppSettings appSettings, IHttpClientFactory httpClientFactory, ILogger<IpHelper> logger)
        {
            _appSettings = appSettings;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task ProxyIpStatAsync(int taskid, string proxyIpUrl)
        {
            try
            {
                string baseUrl = new Uri(_appSettings.TaskApiUrl).GetLeftPart(UriPartial.Authority);
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
                var bidRequest = new JObject();
                bidRequest["id"] = taskid;
                bidRequest["host"] = await CommonHelper.GetHostAsync();
                bidRequest["agency"] = proxyIpUrl;// new Uri(proxyIpUrl).GetLeftPart(UriPartial.Authority);
                //bidRequest["agency"] = new Uri(proxyIpUrl).GetLeftPart(UriPartial.Authority);
                bidRequest["id"] = taskid;
                var postData = JsonConvert.SerializeObject(bidRequest);
                HttpContent content = new StringContent(postData);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                await client.PostAsync($"{baseUrl}/api/ip_stat.php?action=invoke&t={System.DateTime.Now.Ticks}", content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        public async Task UpdateProxyIpStatAsync(int taskid, string proxyIpUrl, string realIp)
        {
            try
            {
                string baseUrl = new Uri(_appSettings.TaskApiUrl).GetLeftPart(UriPartial.Authority);
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json; charset=utf-8");
                var bidRequest = new JObject();
                bidRequest["id"] = taskid;
                bidRequest["host"] = await CommonHelper.GetHostAsync();
                bidRequest["agency"] = proxyIpUrl;// new Uri(proxyIpUrl).GetLeftPart(UriPartial.Authority);
                bidRequest["ip"] = realIp;
                var postData = JsonConvert.SerializeObject(bidRequest);
                HttpContent content = new StringContent(postData);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                await client.PostAsync($"{baseUrl}/api/ip_stat.php?action=reward&t={System.DateTime.Now.Ticks}", content);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }


        private static ConcurrentQueue<IpEntity> ipQueues = new ConcurrentQueue<IpEntity>();
        public async Task<IpEntity> GetProxyIpAsync(JToken task, int count = 0)
        {
            if (ipQueues.TryDequeue(out var value))
            {
                return value;
            }
            IPFormat iPFormat = IPFormat.TXT;
            var url = GetIpUrl(task, out iPFormat, count);
            var client = _httpClientFactory.CreateClient("IP_DATA");
            try
            {
                await _mutex.WaitAsync();
                HttpResponseMessage response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(content) && !(content.Contains("白名单") || content.Contains("暂无") || content.Contains("没有") || content.Contains("过多") || content.Contains("请重试")))
                    {
                        if (iPFormat == IPFormat.TXT)
                        {
                            var values = content.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var text in values)
                            {
                                ipQueues.Enqueue(new IpEntity() { format = iPFormat, value = text });
                            }
                        }
                        else if (iPFormat == IPFormat.JSON)
                        {
                            var json = JObject.Parse(content);
                            if (url.Contains("service.ipzan.com"))
                            {
                                foreach (var data in json.SelectToken("data.list").Children())
                                {
                                    ipQueues.Enqueue(new IpEntity() { format = iPFormat, json = data });
                                }
                            }
                            else if (url.Contains("api.xingyuip.com"))
                            {
                                foreach (var data in json.SelectToken("list").Children())
                                {
                                    var item = (JObject)data.DeepClone();
                                    item["rip"] = data["exit_ip"]?.Value<string>();
                                    item.Remove("exit_ip");



                                    ipQueues.Enqueue(new IpEntity() { format = iPFormat, json = (JToken)item });
                                }
                            }
                            else
                            {
                                foreach (var data in json.SelectToken("data").Children())
                                {
                                    ipQueues.Enqueue(new IpEntity() { format = iPFormat, json = data });
                                }
                            }

                        }

                        if (ipQueues.TryDequeue(out var entity))
                        {
                            return entity;
                        }
                    }
                    throw new Exception(content);

                }
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
                //_logger.LogError($"GetIpUrl => {url},{ex.InnerException?.Message}");
            }
            finally
            {
                _mutex.Release();
            }
            return null;
        }





        private string GetIpUrl(JToken task, out IPFormat format, int count = 0)
        {
            format = IPFormat.TXT;
            var url = _appSettings.ProxyIpUrl.Trim();
            try
            {
                //四川[18]:成都[188],
                var query = System.Web.HttpUtility.ParseQueryString(url);

                if (url.Contains("shenlongip.com"))
                {
                    #region shenlongip.com
                    //http://api.shenlongip.com/ip?key=pjr1xjh4&area=310100,320100,320200,320300,320400,320500,320600,320700,320800,320900,321000,321100,321200,321300&protocol=1&mr=1&pattern=txt&count=1&sign=e207c36f5687a57e9802c8190f428ea4
                    //if (query["format"] != null && query["format"].ToString().Equals("json"))
                    //    format = IPFormat.JSON;
                    //else
                    //    format = IPFormat.TXT;

                    //http://api.shenlongip.com/ip?key=k902nyta&protocol=1&mr=2&pattern=json&count=1&sign=e207c36f5687a57e9802c8190f428ea4&rip=1


                    if (query["pattern"] != null && query["pattern"].ToString().Equals("json"))
                        format = IPFormat.JSON;
                    else
                        format = IPFormat.TXT;


                    if (_appSettings.IsRealIp)
                    {
                        format = IPFormat.JSON;

                        if (Regex.IsMatch(url, @"pattern=\w+"))
                            url = Regex.Replace(url, @"pattern=\w+", $"pattern=json");
                        else
                            url = url += $"&pattern=json";


                        if (Regex.IsMatch(url, @"rip=\d+"))
                            url = Regex.Replace(url, @"rip=\d+", $"rip=1");
                        else
                            url = url += $"&rip=1";
                    }

                    if (count > 1)
                    {
                        if (Regex.IsMatch(url, @"count=[\d]*"))
                            url = Regex.Replace(url, @"count=[\d]*", $"count={count}");
                        else
                            url = url += $"&count={count}";
                    }

                    if (task["address"] != null && !string.IsNullOrEmpty(task["address"].ToString()) && !task["address"].ToString().Equals("全部"))
                    {
                        var address_list = task["address"].ToString().Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                        List<string> area_codes = new List<string>();
                        foreach (var address in address_list)
                        {
                            if (address.Contains(":"))
                            {
                                var address_values = address.Split(':');
                                var m1 = Regex.Match(address_values[0], @"\w+");
                                if (m1.Success)
                                {
                                    var m2 = Regex.Match(address_values[1], @"\w+");
                                    if (m2.Success)
                                    {
                                        var areas = region_shenlong.Where(w => w["name"].ToString().Contains(m2.Value)).ToList();
                                        if (areas != null && areas.Count() > 0)
                                        {
                                            var area_code = string.Join(",", areas.Select(s => s["code"].ToString()));
                                            area_codes.Add(area_code);
                                        }
                                    }
                                    else
                                    {
                                        var areas = region_shenlong.Where(w => w["name"].ToString().Contains(m1.Value)).ToList();
                                        if (areas != null && areas.Count() > 0)
                                        {
                                            var area_code = string.Join(",", areas.Select(s => s["code"].ToString()));
                                            area_codes.Add(area_code);
                                        }
                                    }
                                }
                            }
                            else
                            {
                                var m1 = Regex.Match(address, @"\w+");
                                if (m1.Success)
                                {
                                    var areas = region_shenlong.Where(w => w["name"].ToString().Contains(m1.Value));
                                    if (areas != null && areas.Count() > 0)
                                    {
                                        var area_code = string.Join(",", areas.Select(s => s["code"].ToString()));
                                        area_codes.Add(area_code);
                                    }
                                }
                            }
                        }


                        if (area_codes.Count() > 0)
                        {
                            var all_area_code = string.Join(",", area_codes);
                            if (Regex.IsMatch(url, @"area=[\d,]+"))
                                url = Regex.Replace(url, @"area=[\d,]+", $"area={all_area_code}");
                            else
                                url = url += $"&area={all_area_code}";
                        }

                    }
                    #endregion
                }
    
                else if (url.Contains("service.ipzan.com"))
                {
                    #region service.ipzan.com
                    //http://service.ipzan.com/core-extract?num=1&no=20211030082718667537&minute=3&format=json&repeat=1&protocol=1&pool=quality&mode=whitelist&secret=u5ta45tj

                    if (_appSettings.IsRealIp)
                    {
                        format = IPFormat.JSON;
                        //realIp=1
                        if (Regex.IsMatch(url, @"format=\w+"))
                            url = Regex.Replace(url, @"format=\w+", $"format=json");
                        else
                            url = url += $"&format=json";


                        if (Regex.IsMatch(url, @"realIp=\d+"))
                            url = Regex.Replace(url, @"realIp=\d+", $"realIp=1");
                        else
                            url = url += $"&realIp=1";
                    }
                    else
                    {
                        if (Regex.IsMatch(url, @"format=\w+"))
                            url = Regex.Replace(url, @"format=\w+", $"format=txt");
                    }


                    if (count > 1)
                    {
                        if (Regex.IsMatch(url, @"num=[\d]*"))
                            url = Regex.Replace(url, @"num=[\d]*", $"num={count}");
                        else
                            url = url += $"&num={count}";
                    }

                    if (task["address"] != null && !string.IsNullOrEmpty(task["address"].ToString()) && !task["address"].ToString().Equals("全部"))
                    {
                        var addrs = task["address"].ToString().Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                        var address = addrs[Math.Abs(Guid.NewGuid().GetHashCode()) % addrs.Length].Split(':');
                        var area_addr = string.Empty;
                        string area = string.Empty;
                        if (address.Length > 1)
                        {
                            var m1 = Regex.Match(address[1], @"\w+");
                            if (m1.Success)
                            {
                                var area_res = region_ipzan.Where(w => w["name"].ToString().Contains(m1.Value)).OrderByDescending(o => Convert.ToInt64(o["code"].ToString())).FirstOrDefault();
                                if (area_res == null)
                                {
                                    m1 = Regex.Match(address[0], @"\w+");
                                    if (m1.Success)
                                    {
                                        area_res = region_ipzan.Where(w => w["name"].ToString().Contains(m1.Value)).OrderByDescending(o => Convert.ToInt64(o["code"].ToString())).FirstOrDefault();
                                    }
                                }
                                if (area_res != null)
                                {
                                    if (Regex.IsMatch(url, @"area=[\w]*[^&]?"))
                                        url = Regex.Replace(url, @"area=[\w]*[^&]?", $"area={area_res["code"]}");
                                    else
                                        url = url += $"&area={area_res["code"]}";
                                }
                            }
                        }
                        else
                        {
                            var m1 = Regex.Match(address[0], @"\w+");
                            if (m1.Success)
                            {
                                var area_res = region_ipzan.Where(w => w["name"].ToString().Contains(m1.Value)).OrderByDescending(o => Convert.ToInt64(o["code"].ToString())).FirstOrDefault();
                                if (area_res != null)
                                {
                                    if (Regex.IsMatch(url, @"area=[\w]*[^&]?"))
                                        url = Regex.Replace(url, @"area=[\w]*[^&]?", $"area={area_res["code"]}");
                                    else
                                        url = url += $"&area={area_res["code"]}";
                                }
                            }
                        }
                    }
                    #endregion

                }
                else if(url.Contains("api.xingyuip.com"))
                {
                    #region service.ipzan.com
                    //http://api.xingyuip.com:13000/extract?channel_id=228&auth_mode=whitelist&channel_secret=w044cm&quantity=1&data_type=2&line_separator=0&dedup_mode=1

                    if (_appSettings.IsRealIp)
                    {
                        format = IPFormat.JSON;
                        //realIp=1
                        if (Regex.IsMatch(url, @"data_type=\d+"))
                            url = Regex.Replace(url, @"data_type=\d+", $"data_type=2");
                        else
                            url = url += $"&data_type=2";
 
                    }
                    else
                    {
                        if (Regex.IsMatch(url, @"data_type=\d+"))
                            url = Regex.Replace(url, @"data_type=\d+", $"data_type=1");
                    }


                    if (count > 1)
                    {
                        if (Regex.IsMatch(url, @"quantity=[\d]*"))
                            url = Regex.Replace(url, @"quantity=[\d]*", $"quantity={count}");
                        else
                            url = url += $"&quantity={count}";
                    }
                    #endregion
                }
                else if (url.Contains("51daili.com"))
                {
                    #region 51daili.com
                    //http://bapi.51daili.com/traffic/getip?linePoolIndex=1&packid=12&time=2&qty=12&port=1&format=txt&usertype=17&uid=39905


                    if (count > 1)
                    {
                        if (Regex.IsMatch(url, @"qty=[\d]*"))
                            url = Regex.Replace(url, @"qty=[\d]*", $"qty={count}");
                        else
                            url = url += $"&qty={count}";
                    }


                    if (task["address"] != null && !string.IsNullOrEmpty(task["address"].ToString()) && !task["address"].ToString().Equals("全部"))
                    {
                        var address_list = task["address"].ToString().Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                        var address = address_list[Math.Abs(Guid.NewGuid().GetHashCode()) % address_list.Length].Split(':');
                        if (address.Length > 1)
                        {
                            var m1 = Regex.Match(address[0], @"\w+");
                            if (m1.Success)
                            {
                                var area_prov = region_51dail.Where(w => w["provinceName"].ToString().Contains(m1.Value)).OrderByDescending(o => Convert.ToInt64(o["provinceCode"].ToString())).FirstOrDefault();
                                if (area_prov != null)
                                {
                                    var m2 = Regex.Match(address[1], @"\w+");
                                    if (m2.Success)
                                    {
                                        var area_city = area_prov["mallCityList"].FirstOrDefault(w => w["cityName"].ToString().Contains(m2.Value));
                                        if (area_city != null)
                                        {
                                            if (Regex.IsMatch(url, @"regionCode=[\w]*[^&]?"))
                                                url = Regex.Replace(url, @"regionCode=[\w]*[^&]?", $"regionCode={area_city["cityCode"]}");
                                            else
                                                url = url += $"&regionCode={area_city["cityCode"]}";
                                        }
                                        else
                                        {
                                            if (Regex.IsMatch(url, @"regionCode=[\w]*[^&]?"))
                                                url = Regex.Replace(url, @"area=[\w]*[^&]?", $"regionCode={area_prov["provinceCode"]}");
                                            else
                                                url = url += $"&regionCode={area_prov["provinceCode"]}";
                                        }
                                    }
                                    else
                                    {
                                        if (Regex.IsMatch(url, @"regionCode=[\w]*[^&]?"))
                                            url = Regex.Replace(url, @"area=[\w]*[^&]?", $"regionCode={area_prov["provinceCode"]}");
                                        else
                                            url = url += $"&regionCode={area_prov["provinceCode"]}";
                                    }
                                }
                            }
                        }
                        else
                        {
                            var m1 = Regex.Match(address[0], @"\w+");
                            if (m1.Success)
                            {
                                var area_prov = region_51dail.Where(w => w["provinceName"].ToString().Contains(m1.Value)).OrderByDescending(o => Convert.ToInt64(o["provinceCode"].ToString())).FirstOrDefault();
                                if (area_prov != null)
                                {
                                    if (Regex.IsMatch(url, @"regionCode=[\w]*[^&]?"))
                                        url = Regex.Replace(url, @"regionCode=[\w]*[^&]?", $"regionCode={area_prov["provinceCode"]}");
                                    else
                                        url = url += $"&regionCode={area_prov["provinceCode"]}";
                                }
                            }
                        }
                    }
                    #endregion

                }

                else if (url.Contains("api.test.myipproxy.com") || url.Contains("api.hailiangip.com") || url.Contains("111.73.45.100") || url.Contains("47.97.20.179"))
                {
                    //http://api.test.myipproxy.com:8422/api/getIp?type=1&num=1&orderId=O21081016192288073951&time=1628583680&sign=95d2880db7a7effe459df80ee80ba249&unbindTime=180&dataType=1&pid=&cid=
                    #region myipproxy & hailiangip & ...

                    if (query["dataType"] != null && Int32.TryParse(query["dataType"].ToString(), out int dataType) && dataType > 0)
                        format = IPFormat.TXT;
                    else
                        format = IPFormat.JSON;

                    if (count > 0)
                    {
                        if (Regex.IsMatch(url, @"num=[\d]*"))
                            url = Regex.Replace(url, @"num=[\d]*", $"num={count}");
                        else
                            url = url += $"&num={count}";
                    }

                    if (_appSettings.IsRealIp)
                    {
                        format = IPFormat.JSON;
                        if (Regex.IsMatch(url, @"dataType=[\d]*"))
                            url = Regex.Replace(url, @"dataType=[\d]*", $"dataType=0");
                        else
                            url = url += $"&dataType=0";
                    }
                    else
                    {
                        if (Regex.IsMatch(url, @"dataType=[\d]*"))
                            url = Regex.Replace(url, @"dataType=[\d]*", $"dataType=1");
                        else
                            url = url += $"&dataType=1";
                    }

                    if (task["address"] != null && !string.IsNullOrEmpty(task["address"].ToString()) && !task["address"].ToString().Equals("全部"))
                    {
                        var addrs = task["address"].ToString().Split(new string[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                        var address = addrs[Math.Abs(Guid.NewGuid().GetHashCode()) % addrs.Length].Split(':');
                        var m1 = Regex.Match(address[0], @"\d+");
                        string pid = string.Empty, cid;
                        if (m1.Success)
                        {
                            pid = m1.Value;
                            if (Regex.IsMatch(url, @"pid=[\d]*"))
                                url = Regex.Replace(url, @"pid=[\d]*", $"pid={pid}");
                            else
                                url = url += $"&pid={pid}";
                        }
                        if (address.Count() > 1)
                        {
                            var m2 = Regex.Match(address[1], @"\d+");
                            if (m2.Success)
                            {
                                cid = m2.Value;
                                if (!string.IsNullOrWhiteSpace(pid) && !pid.Equals(cid))
                                {
                                    if (Regex.IsMatch(url, @"cid=[\d]*"))
                                        url = Regex.Replace(url, @"cid=[\d]*", $"cid={cid}");
                                    else
                                        url = url += $"&cid={cid}";
                                }
                            }
                        }
                    }

                    #endregion
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
            return url;
        }





        public async Task<bool> PingIP(string proxy_server)
        {
            Ping pingSender = new Ping();
            PingOptions options = new PingOptions();
            options.DontFragment = true;
            string data = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            byte[] buffer = Encoding.ASCII.GetBytes(data);
            int timeout = 1000;
            PingReply reply = await pingSender.SendPingAsync(proxy_server, timeout, buffer, options);
            if (reply.Status == IPStatus.Success)
            {
                return true;
            }
            return false;
        }

        public async Task<string> GetIpInfo(string proxy)
        {
            HttpClientHandler httpClientHandler = new HttpClientHandler() { Proxy = new WebProxy(proxy, BypassOnLocal: false), UseProxy = true };
            using (var client = new HttpClient(httpClientHandler))
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    HttpResponseMessage response = await client.GetAsync("http://ip-api.com/json/?lang=zh-CN");

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                }
                catch (WebException ex)
                {
                    Debug.WriteLine(ex.Message);

                }
                return null;
            }
            ;
        }

        public async Task<string> GetIpInfo()
        {

            using (var client = new HttpClient())
            {
                try
                {
                    client.Timeout = TimeSpan.FromSeconds(15);
                    HttpResponseMessage response = await client.GetAsync("http://ip-api.com/json/?lang=zh-CN");

                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        return await response.Content.ReadAsStringAsync();
                    }
                }
                catch (WebException ex)
                {
                    Debug.WriteLine(ex.Message);

                }
                return null;
            }
            ;
        }
        //http://ip-api.com/json/?lang=zh-CN


    }
}
