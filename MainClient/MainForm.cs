using MainClient.Common;
using MainClient.Logging;
using MainClient.LogViewer;
using MainClient.Models;
using MainClient.Net;
using MainClient.UiTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SEM;
using SEM.Extensions;
using SEM.Infrastructure;
using SEM.Models;
using SEM.Net;
using SEM.Plugins;
using Serilog.Events;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;



namespace MainClient
{
    public partial class MainForm : Form
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private readonly AppSettings _appSettings;
        private readonly IpHelper _ipHelper;
        private readonly AdeHelper _adeHelper;
        private readonly FileUpdater _fileUpdater;
        private readonly ProxyTester _ipTester;
        private readonly IRootDomainService _domainService;
        private readonly IPlaywrightProvider _playwrightProvider;
        private readonly TaskStatsAggregator _aggregator;
        private readonly ChineseNameGenerator _nameGenerator;
        private readonly FileCleanupQueue _fileCleanupQueue = new();
        private int _startupAutomationTriggered = 0;
        private WsClientService? _wsClient;
        #region 任务调度
        private PipelineRunner<JToken>? _pipeline;
        private UiTaskRunner? _uiRunner;
        private AppAutoRestart? _appAutoRestart;
        #endregion

        #region 任务计数属性
        /// <summary>
        /// 执行总量
        /// </summary>
        private int QTPTotalStartCount = 0;
        /// <summary>
        /// 曝光总量
        /// </summary>
        private int QTPTotalDspCount = 0;
        /// <summary>
        /// 点击总量
        /// </summary>
        private int QTPTotalClickthroughCount = 0;


        #endregion



        #region LogWrite

        private readonly ConcurrentQueue<UiLogItem> _uiLogBuffer = new();
        private readonly System.Windows.Forms.Timer _uiTimer = new();
        private CancellationTokenSource _uiLogCts = new();
        private int _flushing = 0;
        private const int MaxFlushCount = 500;
        // 新控件
        private LogViewerUltra logViewer;
        private void StartLogConsumer()
        {
            // 初始化新控件
            logViewer = new LogViewerUltra()
            {
                Dock = DockStyle.Fill
            };
            groupBox33.Controls.Add(logViewer);

            // 后台读取日志
            Task.Run(async () =>
            {
                var reader = UiLogChannel.Channel.Reader;

                try
                {
                    await foreach (var item in reader.ReadAllAsync(_uiLogCts.Token))
                    {
                        if (_uiLogCts.IsCancellationRequested)
                            break;

                        _uiLogBuffer.Enqueue(item);
                    }
                }
                catch (OperationCanceledException) { }

            }, _uiLogCts.Token);

            // UI Timer
            _uiTimer.Interval = 200;
            _uiTimer.Tick += (_, __) =>
            {
                if (Interlocked.Exchange(ref _flushing, 1) == 1)
                    return;

                try
                {
                    FlushLogsToUi();
                }
                finally
                {
                    Interlocked.Exchange(ref _flushing, 0);
                }
            };
            _uiTimer.Start();

            this.FormClosing += (s, e) =>
            {
                try
                {
                    _uiTimer.Stop();
                    _uiLogCts.Cancel();
                    UiLogChannel.Channel.Writer.TryComplete();
                }
                catch { }
            };
        }
        private void FlushLogsToUi()
        {
            if (IsDisposed || Disposing)
                return;

            if (!IsHandleCreated || logViewer.IsDisposed)
                return;

            if (_uiLogBuffer.IsEmpty)
                return;

            int count = 0;

            while (_uiLogBuffer.TryDequeue(out var item))
            {
                logViewer.WriteLog(item.Message, ConvertLevel(item.Level));

                if (++count >= MaxFlushCount)
                    break;
            }
        }
        // 日志级别映射
        private LogLevel ConvertLevel(LogEventLevel level) => level switch
        {
            LogEventLevel.Verbose => LogLevel.Trace,
            LogEventLevel.Debug => LogLevel.Debug,
            LogEventLevel.Information => LogLevel.Information,
            LogEventLevel.Warning => LogLevel.Warning,
            LogEventLevel.Error => LogLevel.Error,
            _ => LogLevel.Information
        };



        public void LogWriteLine(string message)
        {
            _logger.LogInformation(message);
        }
        public void LogWriteLine(PluginLogEventArgs e)
        {
            //if (IsPlaywrightTraceMessage(e.Message))
            //{
            //    // 额外写一份到 playwright-.log（由 Program.cs 中 [PWTRACE] 过滤规则分流）
            //    _logger.LogInformation("[PWTRACE] {PluginMessage}", e.Message);
            //}

            switch (e.Level)
            {
                case LogLevel.Trace: _logger.LogTrace(e.Message); break;
                case LogLevel.Debug: _logger.LogDebug(e.Message); break;
                case LogLevel.Information: _logger.LogInformation(e.Message); break;
                case LogLevel.Warning: _logger.LogWarning(e.Message); break;
                case LogLevel.Error: _logger.LogError(e.Message); break;
            }
        }

        private static bool IsPlaywrightTraceMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            return message.Contains("CDP", StringComparison.OrdinalIgnoreCase)
                || message.Contains("RequestFailed", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Crash", StringComparison.OrdinalIgnoreCase)
                || message.Contains("args=", StringComparison.OrdinalIgnoreCase)
                || message.Contains("ExecuteWorker:Start", StringComparison.OrdinalIgnoreCase)
                || message.Contains("ExecuteWorker:Canceled", StringComparison.OrdinalIgnoreCase)
                || message.Contains("ExecuteWorker:Complete", StringComparison.OrdinalIgnoreCase);
        }

        #endregion







        private Dictionary<string, SEMPlugin> allPlugins = new Dictionary<string, SEMPlugin>();
        private void LoadQTPPlugins()
        {
            DirectoryInfo d = new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins"));
            d.GetFiles().ToList().ForEach(x =>
             {
                 var assembly = System.Reflection.Assembly.LoadFile(x.FullName);
                 if (assembly != null)
                 {
                     var typeName = System.IO.Path.GetFileNameWithoutExtension(x.FullName);
                     Type type = assembly.GetType($"SEM.Plugins.{typeName}Task");
                     if (type != null)
                     {
                         System.Reflection.MethodInfo methodInfo = type.GetMethod("GetInfo");
                         object result = methodInfo.Invoke(null, null);
                         if (result != null && result is SEMPlugin)
                         {
                             var plugin = (SEMPlugin)result;
                             plugin.type = type;
                             allPlugins.Add(plugin.Name, plugin);
                         }
                     }
                 }
             });
        }

        public async Task<List<FileVersionInfo>> GetLatestFileWithVersionAsync()
        {
            List<FileVersionInfo> result = new List<FileVersionInfo>();
            try
            {
                var versionList = await _fileUpdater.GetLatestFileWithVersionAsync(_appSettings.TaskApiUrl);
                if (versionList != null && versionList.Success)
                {
                    result.AddRange(versionList.Data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetLatestFileWithVersionAsync failed: {ex.Message}");
            }
            return result;
        }
        public async Task InitBrowserVersionListAsync()
        {
            try
            {
                var versionList = await _fileUpdater.GetBrowserVersionListAsync(_appSettings.TaskApiUrl);
                if (versionList != null && versionList.Success && versionList.Data != null)
                {
                    this.InvokeOnUiThreadIfRequired(() =>
                    {
                        var newVersions = versionList.Data
                            .Where(x => !string.IsNullOrWhiteSpace(x))
                            .Distinct()
                            .ToList();

                        var targetVersion = _appSettings.KernelVersion;

                        comboBox_KernelVersion.DataSource = null;
                        comboBox_KernelVersion.DataSource = newVersions;

                        if (newVersions.Count == 0)
                        {
                            comboBox_KernelVersion.SelectedIndex = -1;
                            return;
                        }

                        var index = !string.IsNullOrWhiteSpace(targetVersion)
                            ? newVersions.IndexOf(targetVersion)
                            : -1;

                        comboBox_KernelVersion.SelectedIndex = index >= 0 ? index : 0;

                        _appSettings.KernelVersion = comboBox_KernelVersion.SelectedItem?.ToString() ?? "";
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"InitBrowserVersionListAsync failed: {ex.Message}");
            }
        }


        public void InitKernelVersion()
        {
            try
            {

                string fileDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "File", "chrome-win");
                if (!System.IO.Directory.Exists(fileDir))
                    System.IO.Directory.CreateDirectory(fileDir);

                var versionList = Directory.GetDirectories(fileDir)
                                           .Select(Path.GetFileName)
                                           .OrderByDescending(v => v)
                                           .ToList();

                foreach (var version in versionList)
                {
                    comboBox_KernelVersion.Items.Add(version);
                }

                if (versionList.Count > 0)
                {
                    if (!string.IsNullOrWhiteSpace(_appSettings.KernelVersion) && comboBox_KernelVersion.SelectedIndex == -1)
                    {
                        comboBox_KernelVersion.SelectedIndex = comboBox_KernelVersion.Items.IndexOf(_appSettings.KernelVersion);
                    }

                    if (comboBox_KernelVersion.SelectedIndex == -1)
                        comboBox_KernelVersion.SelectedIndex = 0;
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"InitKernelVersion failed: {ex.Message}");
            }
        }


        private void InitWsClient()
        {
            var wnd_handel = this.Handle;
            _ = Task.Run(async () =>
            {
                var clientId = await CommonHelper.GetHostAsync();
                var serverUrl = "ws://117.21.200.221:9502";
                var token = "abc123";

                _wsClient = new WsClientService(
                serverUrl: serverUrl,
                clientId: clientId,
                token: token,
                machineName: clientId,
                version: Application.ProductVersion,
                group: "default",
                localIp: clientId,
                heartbeatInterval: TimeSpan.FromSeconds(15),
                heartbeatTimeout: TimeSpan.FromSeconds(45),
                reconnectOptions: new WsReconnectOptions
                {
                    EnableAutoReconnect = true,
                    MaxRetryCount = 0,
                    InitialDelay = TimeSpan.FromSeconds(2),
                    MaxDelay = TimeSpan.FromSeconds(30),
                    UseExponentialBackoff = true,
                    BackoffFactor = 2.0,
                    RetryImmediatelyFirstTime = false
                });

                _wsClient.SetMainWindowHandle(wnd_handel);

                _wsClient.OnLog += msg => LogWriteLine(msg);
                _wsClient.OnConnecting += () => LogWriteLine("正在连接服务器...");
                _wsClient.OnConnected += () => LogWriteLine("首次连接成功");
                _wsClient.OnDisconnected += reason => LogWriteLine("连接断开: " + reason);
                _wsClient.OnReconnecting += (_, e) =>
                    LogWriteLine($"第 {e.RetryCount} 次重连中，{e.Delay.TotalSeconds} 秒后重试，原因: {e.Reason}");
                _wsClient.OnReconnectSucceeded += count =>
                    LogWriteLine($"重连成功，之前累计重试次数: {count}");
                _wsClient.OnHeartbeatTimeout += timeout =>
                    LogWriteLine($"心跳超时: {timeout.TotalSeconds} 秒");
                _wsClient.OnStopped += () => LogWriteLine("客户端已停止");

                RegisterWsHandlers(_wsClient);

                await _wsClient.StartAsync();

            });

        }

        private void RegisterWsHandlers(WsClientService wsClient)
        {
            wsClient.RegisterGetConfigHandler(() =>
            {

                LogWriteLine("获取配置");
                return _appSettings;
            });

            wsClient.RegisterSetConfigHandler(payload =>
            {
                if (payload.ValueKind == JsonValueKind.Object)
                {
                    //if (payload.TryGetProperty("serverUrl", out var p1))
                    //    config.ServerUrl = p1.GetString() ?? config.ServerUrl;

                    //if (payload.TryGetProperty("clientId", out var p2))
                    //    config.ClientId = p2.GetString() ?? config.ClientId;

                    //if (payload.TryGetProperty("token", out var p3))
                    //    config.Token = p3.GetString() ?? config.Token;

                    //if (payload.TryGetProperty("deviceName", out var p4))
                    //    config.DeviceName = p4.GetString() ?? config.DeviceName;

                    //if (payload.TryGetProperty("timeout", out var p5) && p5.TryGetInt32(out var timeout))
                    //    config.Timeout = timeout;
                }

                //ClientConfigStore.Save(_configFile, config);
                LogWriteLine("配置已保存到本地");
                return (object)_appSettings;
            });

            wsClient.RegisterAppStartHandler(args =>
            {
                string name = "";
                string filePath = "";
                string arguments = "";

                if (args.ValueKind == JsonValueKind.Object)
                {
                    if (args.TryGetProperty("name", out var p1))
                        name = p1.GetString() ?? "";

                    if (args.TryGetProperty("filePath", out var p2))
                        filePath = p2.GetString() ?? "";

                    if (args.TryGetProperty("arguments", out var p3))
                        arguments = p3.GetString() ?? "";
                }

                if (string.IsNullOrWhiteSpace(filePath))
                    throw new InvalidOperationException("filePath 不能为空");

                var psi = new ProcessStartInfo
                {
                    FileName = filePath,
                    Arguments = arguments,
                    UseShellExecute = true
                };

                var p = Process.Start(psi);
                if (p == null)
                    throw new InvalidOperationException("应用启动失败");

                return (object)new
                {
                    name,
                    filePath,
                    processId = p.Id,
                    message = "应用已启动"
                };
            });

            wsClient.RegisterAppStopHandler(args =>
            {
                string name = "";
                int processId = 0;

                if (args.ValueKind == JsonValueKind.Object)
                {
                    if (args.TryGetProperty("name", out var p1))
                        name = p1.GetString() ?? "";

                    if (args.TryGetProperty("processId", out var p2) && p2.TryGetInt32(out var pid))
                        processId = pid;
                }

                int killed = 0;

                if (processId > 0)
                {
                    try
                    {
                        var p = Process.GetProcessById(processId);
                        p.Kill(true);
                        killed++;
                    }
                    catch
                    {
                    }
                }
                else if (!string.IsNullOrWhiteSpace(name))
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        try
                        {
                            p.Kill(true);
                            killed++;
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("name 或 processId 至少传一个");
                }

                return (object)new
                {
                    name,
                    processId,
                    killed,
                    message = killed > 0 ? "应用已停止" : "未找到可停止的进程"
                };
            });
        }



        #region 自动更新
        private async Task HandleStartupAutomationAsync(FileVersionInfo? latestVersion)
        {
            if (Interlocked.Exchange(ref _startupAutomationTriggered, 1) == 1)
            {
                return;
            }
            if (!_appSettings.AutoUpdate)
            {
                return;
            }

            try
            {

                await ExecuteUpdateAsync(isAutoUpdate: true, selectedFile: latestVersion);
            }
            catch (Exception)
            {
            }
        }

        private void TriggerStartTask()
        {
            this.InvokeOnUiThreadIfRequired(() =>
            {
                if (btnStartStop.Enabled)
                {
                    btnStartStop.PerformClick();
                }
            });
        }

        private async Task<bool> ExecuteUpdateAsync(bool isAutoUpdate, FileVersionInfo? selectedFile = null)
        {
            if (selectedFile == null)
            {
                _logger.LogInformation(isAutoUpdate ? "自动更新未找到可用版本。" : "请先选择要更新的版本！");
                return false;
            }

            this.InvokeOnUiThreadIfRequired(() =>
            {
                btnUpdate.Enabled = false;
                toolStripProgressBarDownload.AutoSize = false;
                toolStripProgressBarDownload.Width = 300;
                toolStripProgressBarDownload.Visible = true;
            });

            double lastReportedProgress = 0;
            const double minProgressStep = 1;
            DateTime lastProgressUpdate = DateTime.Now;
            var progressUpdateInterval = TimeSpan.FromMilliseconds(1000);
            EventHandler<ProgressEventArgs> handler = (s, e) =>
            {
                bool isProgressTooSmall = Math.Abs(e.Progress - lastReportedProgress) < minProgressStep;
                bool isTooSoon = DateTime.Now - lastProgressUpdate < progressUpdateInterval;
                bool notFinished = e.Progress < 100;

                if (isProgressTooSmall && isTooSoon && notFinished)
                {
                    return;
                }

                lastReportedProgress = e.Progress;
                lastProgressUpdate = DateTime.Now;
                _logger.LogInformation(e.Message);
                this.InvokeOnUiThreadIfRequired(() =>
                {
                    toolStripProgressBarDownload.Value = (int)Math.Min(Math.Max(e.Progress, 0), 100);
                });
            };

            _fileUpdater.ProgressChanged -= handler;
            _fileUpdater.ProgressChanged += handler;

            try
            {
                try
                {
                    var smaideZip = await _fileUpdater.DownloadBootstrapAsync(_appSettings.TaskApiUrl);
                    var smaideDir = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName!;
                    ZipFile.ExtractToDirectory(smaideZip, smaideDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "下载或解压引导更新程序失败，继续执行主程序更新。");
                }

                this.InvokeOnUiThreadIfRequired(() =>
                {
                    toolStripProgressBarDownload.Width = 60;
                    toolStripProgressBarDownload.Visible = false;
                });

                var zipFilePath = await _fileUpdater.DownloadFileAsync(_appSettings.TaskApiUrl, selectedFile);
                string updaterPath = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName!, "smaide.exe");
                Process.Start(new ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"--update-version \"{Process.GetCurrentProcess().MainModule?.FileName}\" \"{zipFilePath}\" \"v{AppConsts.AppVersion}\" \"{selectedFile.Text}\"",
                    WorkingDirectory = Path.GetDirectoryName(updaterPath),
                    UseShellExecute = false,
                });

                Application.Exit();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, isAutoUpdate ? "自动更新失败" : "手动更新失败");
                return false;
            }
            finally
            {
                _fileUpdater.ProgressChanged -= handler;
                this.InvokeOnUiThreadIfRequired(() =>
                {
                    if (!IsDisposed && !Disposing)
                    {
                        btnUpdate.Enabled = true;
                        toolStripProgressBarDownload.Width = 60;
                        toolStripProgressBarDownload.Visible = false;
                    }
                });
            }
        }

        #endregion


        public MainForm(
            IRootDomainService domainService,
            IPlaywrightProvider playwrightProvider,
            TaskStatsAggregator aggregator,
            AdeHelper adeHelper,
            ChineseNameGenerator nameGenerator,
            FileUpdater fileUpdater,
            IpHelper ipHelper,
            ProxyTester ipTester,
            AppSettings appSettings,
            IHttpClientFactory httpClientFactory,
            ILogger<MainForm> logger)
        {
            InitializeComponent();
            this._domainService = domainService;
            this._playwrightProvider = playwrightProvider;
            this._aggregator = aggregator;
            this._adeHelper = adeHelper;
            this._nameGenerator = nameGenerator;
            this._fileUpdater = fileUpdater;
            this._ipHelper = ipHelper;
            this._ipTester = ipTester;
            this._appSettings = appSettings;
            this._logger = logger;
            this._httpClientFactory = httpClientFactory;
            this.Text += $"{AppConsts.AppVersion}";
            LoadQTPPlugins();
            foreach (var p in allPlugins)
            {
                comboBox_QTPName.Items.Add(p.Key);
            }
            InitKernelVersion();
            LoadAppSetting();
            #region 数据初始化
            foreach (var item in new ManagementObjectSearcher("Select * from Win32_ComputerSystem").Get())
            {
                toolStripStatusLabel1.Text = $"CPU:{item["NumberOfLogicalProcessors"]}";
            }
            #endregion

            //InitWsClient();
        }
        public async Task InitGlobalStatus()
        {
            try
            {
                var resp = await _adeHelper.GetHostTodayStatusAsync();
                if (resp != null && resp.SelectToken("data") != null)
                {
                    this.QTPTotalStartCount = resp.SelectToken("data.start")?.Value<int>() ?? 0;
                    this.QTPTotalDspCount = resp.SelectToken("data.dsp")?.Value<int>() ?? 0;
                    this.QTPTotalClickthroughCount = resp.SelectToken("data.click")?.Value<int>() ?? 0;
                    this.InvokeOnUiThreadIfRequired(() =>
                    {
                        toolStripStatusLabel4.Text = $"执行总量：{this.QTPTotalStartCount}";
                        toolStripStatusLabel5.Text = $"曝光总量：{this.QTPTotalDspCount}";
                        toolStripStatusLabel6.Text = $"点击总量：{this.QTPTotalClickthroughCount}";
                    });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "InitGlobalStatus failed");

            }


        }



        private void ClearLocalCacheFile()
        {
            string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string tempPath = Path.GetTempPath();
            string chromeUserDataPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Temp",
                "Chrome",
                _appSettings.KernelVersion,
                "User_Data");

            // 1. 先快速收集
            var items = new List<CleanupItem>();

            items.AddRange(CleanupCollector.CollectDownloadFiles(
                downloadsPath,
                new[] { ".apk", ".crdownload", ".pdf" }));

            items.AddRange(CleanupCollector.CollectPrefixedDirectories(
                tempPath,
                "playwright-"));

            items.AddRange(CleanupCollector.CollectSingleDirectory(
                chromeUserDataPath));


            string historyDir = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName!, "history");
            items.AddRange(CleanupCollector.CollectDirectories(historyDir));

            // 2. 再统一入后台队列
            foreach (var item in items)
            {
                if (item.Type == CleanupItemType.File)
                    _fileCleanupQueue.EnqueueFile(item.Path);
                else
                    _fileCleanupQueue.EnqueueDirectory(item.Path);
            }
        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            StartLogConsumer();
            _logger.LogInformation("应用已启动");

            Task.Run(async () =>
            {
                CommonHelper.ClearLocalChromeProcesses();
                var latestFileList = await GetLatestFileWithVersionAsync();
                if (latestFileList.Count > 0)
                {
                    this.InvokeOnUiThreadIfRequired(() =>
                    {
                        comboBox_VersionList.DataSource = null;
                        comboBox_VersionList.DisplayMember = "Text";
                        comboBox_VersionList.ValueMember = "File";
                        comboBox_VersionList.DataSource = latestFileList;
                        comboBox_VersionList.SelectedIndex = 0;
                    });
                }
                try
                {
                    await InitBrowserVersionListAsync();
                    await InitGlobalStatus();
                    ClearLocalCacheFile();
                }
                catch (Exception)
                {

                }
                var isRestart = System.Environment.GetCommandLineArgs().Any(p => p.StartsWith("restart"));
                if (isRestart)
                {
                    TriggerStartTask();
                }

                if (latestFileList.Count > 0)
                {
                    await HandleStartupAutomationAsync(latestFileList.FirstOrDefault());
                }

                this.InvokeOnUiThreadIfRequired(() =>
                {

                    #region 控件初始化
                    var controls = new List<Control>() { tabPage1, groupBox6 };
                    foreach (var control in controls)
                    {
                        foreach (var c in control.Controls)
                        {
                            if (c is NumericUpDown)
                            {
                                (c as NumericUpDown).ValueChanged += (s, e) =>
                                {
                                    UpdateAppSetting();
                                };
                            }
                            else if (c is TextBox)
                            {
                                (c as TextBox).TextChanged += (s, e) =>
                                {
                                    UpdateAppSetting();
                                };
                            }
                            else if (c is CheckBox)
                            {
                                (c as CheckBox).Click += (s, e) =>
                                {
                                    UpdateAppSetting();
                                };
                            }
                            else if (c is RadioButton)
                            {
                                (c as RadioButton).Click += (s, e) =>
                                {
                                    UpdateAppSetting();
                                };
                            }
                            else if (c is ComboBox)
                            {
                                (c as ComboBox).SelectedIndexChanged += (s, e) =>
                                {
                                    //if ((c as ComboBox).Name.Equals("comboBox_WordType"))
                                    //{
                                    //    var _value = (c as ComboBox).Text;
                                    //    Task.Run(async () =>
                                    //    {
                                    //        await ReloadWordNames(_value);
                                    //        this.BeginInvoke(() =>
                                    //        {
                                    //            this.comboBox_DynamicWordName.Text = _appSettings.DynamicWordName;
                                    //        });
                                    //    });

                                    //}
                                    UpdateAppSetting();
                                };
                            }
                        }
                    }
                    #endregion

                });
            });

        }

        #region 应用设置

        private void ApplyOneTimeLocalPatch()
        {

            string patchDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "patches");
            if (!Directory.Exists(patchDir))
                Directory.CreateDirectory(patchDir);
            string patchFile = Path.Combine(patchDir, "patch_page_loading_202604252047.done");
            if (File.Exists(patchFile))
                return;

            UserConfigService.Save("AppSettings", _appSettings);
            // 创建标记文件
            File.WriteAllText(
                patchFile,
                $"done at {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                Encoding.UTF8
            );
        }
        private void LoadAppSetting()
        {

            ApplyOneTimeLocalPatch();
            comboBox_QTPName.Text = _appSettings.QTPName;
            textBox_ProxyIpUrl.Text = _appSettings.ProxyIpUrl;
            textBox_TaskApiUrl.Text = _appSettings.TaskApiUrl;
            textBox_DevApiUrl.Text = _appSettings.DevApiUrl;
            numericUpDown_FetchTaskInterval.Value = _appSettings.FetchTaskInterval;
            numericUpDown_MaximumConcurrency.Value = _appSettings.MaximumConcurrency;
            numericUpDown_PageLoadingTimeout.Value = _appSettings.PageLoadingTimeout;
            textBox_TaskName.Text = _appSettings.TaskName;
            numericUpDown_Multiple.Value = _appSettings.Multiple;
            numericUpDown_MainResetTimeout.Value = _appSettings.MainResetTimeout;
            checkBox_IsHiddenMode.Checked = _appSettings.IsHiddenMode;
            checkBox_IsProxyMode.Checked = _appSettings.IsProxyMode;
            checkBox_IsRealIp.Checked = _appSettings.IsRealIp;
            checkBox_GetIpInfo.Checked = _appSettings.GetIpInfo;
            textBox_PageloadedDelay.Text = _appSettings.PageloadedDelay;
            var usingDevIndex = _appSettings.UsingDevIndex;
            if (usingDevIndex == 2)
                radioButton_UseLocalDev.Checked = true;
            else
                radioButton_UseSystemDev.Checked = true;
            checkBox_IsDetailLog.Checked = _appSettings.IsDetailLog;
            textBox_UVOverride.Text = _appSettings.UVOverride;
            textBox_PVOverride.Text = _appSettings.PVOverride;
            numericUpDown_IpTtl.Value = _appSettings.IpTtl;
            checkBox_UVsTriggerOne.Checked = _appSettings.UVsTriggerOne;
            checkBox_PVsTriggerOne.Checked = _appSettings.PVsTriggerOne;
            comboBox_KernelVersion.Text = _appSettings.KernelVersion;
            checkBox_IsTest.Checked = _appSettings.IsTest;
            checkBox_AutoUpdate.Checked = _appSettings.AutoUpdate;

            checkBox_BlockImage.Checked = _appSettings.BlockImage;
            checkBox_BlockMedia.Checked = _appSettings.BlockMedia;


        }
        private static object lock_config = new object();
        private void UpdateAppSetting()
        {
            lock (lock_config)
            {
                _appSettings.QTPName = comboBox_QTPName.Text;
                _appSettings.ProxyIpUrl = textBox_ProxyIpUrl.Text;
                _appSettings.TaskApiUrl = textBox_TaskApiUrl.Text;
                _appSettings.DevApiUrl = textBox_DevApiUrl.Text;
                _appSettings.FetchTaskInterval = (int)numericUpDown_FetchTaskInterval.Value;
                _appSettings.MaximumConcurrency = (int)numericUpDown_MaximumConcurrency.Value;
                _appSettings.PageLoadingTimeout = (int)numericUpDown_PageLoadingTimeout.Value;
                _appSettings.TaskName = textBox_TaskName.Text;
                _appSettings.Multiple = (int)numericUpDown_Multiple.Value;
                _appSettings.MainResetTimeout = (int)numericUpDown_MainResetTimeout.Value;
                _appSettings.IsHiddenMode = checkBox_IsHiddenMode.Checked;
                _appSettings.IsProxyMode = checkBox_IsProxyMode.Checked;
                _appSettings.IsRealIp = checkBox_IsRealIp.Checked;
                _appSettings.GetIpInfo = checkBox_GetIpInfo.Checked;
                _appSettings.PageloadedDelay = textBox_PageloadedDelay.Text;
                if (radioButton_UseLocalDev.Checked)
                    _appSettings.UsingDevIndex = 2;
                else
                    _appSettings.UsingDevIndex = 1;
                _appSettings.IsDetailLog = checkBox_IsDetailLog.Checked;
                _appSettings.UVOverride = textBox_UVOverride.Text;
                _appSettings.PVOverride = textBox_PVOverride.Text;
                _appSettings.IpTtl = (int)numericUpDown_IpTtl.Value;
                _appSettings.UVsTriggerOne = checkBox_UVsTriggerOne.Checked;
                _appSettings.PVsTriggerOne = checkBox_PVsTriggerOne.Checked;
                _appSettings.KernelVersion = comboBox_KernelVersion.Text;
                _appSettings.IsTest = checkBox_IsTest.Checked;
                _appSettings.AutoUpdate = checkBox_AutoUpdate.Checked;
                _appSettings.BlockImage = checkBox_BlockImage.Checked;
                _appSettings.BlockMedia = checkBox_BlockMedia.Checked;
                UserConfigService.Save("AppSettings", _appSettings);
            }

        }
        #endregion

        private void buttonClear_Click(object sender, EventArgs e)
        {
            buttonClear.Enabled = false;
            btnStartStop.Enabled = false;
            Task.Run(() =>
            {
                CommonHelper.ClearLocalChromeProcesses();
                SystemCleaner.RestartExplorerAndRdpclip();
                string downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                CommonHelper.DeleteDownloadDir(downloadsPath, new string[] { ".apk", ".crdownload" });
                string tempPath = Path.GetTempPath();
                CommonHelper.DeletePlaywrightDirs(tempPath, "playwright-");
                CommonHelper.ClearDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Temp", "Chrome"));

                string historyDir = Path.Combine(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName!, "history");
                CommonHelper.ClearDirectory(historyDir);

                CommonHelper.EmptyStandbyList();

                this.InvokeOnUiThreadIfRequired(() =>
                {
                    btnStartStop.Enabled = true;
                    buttonClear.Enabled = true;
                });
            });
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.IO.DirectoryInfo dir = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.Startup));
            foreach (System.IO.FileInfo file in dir.GetFiles())
                file.Delete();
            Process.Start(new ProcessStartInfo { FileName = Environment.GetFolderPath(Environment.SpecialFolder.Startup), UseShellExecute = true });
            AppHelper.CreateShortcut("神马广告");
        }




        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string currentDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            Process.Start(new ProcessStartInfo { FileName = currentDirectory, UseShellExecute = true });

        }


        private async void btnUpdate_Click(object sender, EventArgs e)
        {
            if (comboBox_VersionList.Items.Count == 0)
            {
                _logger.LogInformation("无可用的更新版本！");
                return;
            }
            if (comboBox_VersionList.SelectedItem == null)
            {
                _logger.LogInformation("请先选择要更新的版本！");
                return;
            }
            var selectedFile = comboBox_VersionList.SelectedItem as FileVersionInfo;
            if (selectedFile == null)
            {
                _logger.LogInformation("请先选择要更新的版本！");
                return;
            }
            await ExecuteUpdateAsync(isAutoUpdate: false, selectedFile: selectedFile);
        }


        /// <summary>
        /// 获取任务
        /// </summary>
        /// <param name="writer"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task ProducerAsync(ChannelWriter<JToken> writer, CancellationToken token)
        {
            Exception? completionError = null;

            try
            {
                var host = await CommonHelper.GetHostAsync();
                while (!token.IsCancellationRequested)
                {
                    var url = $"{_appSettings.TaskApiUrl}?type=1&action=getTask&name={_appSettings.TaskName}&host={System.Web.HttpUtility.UrlEncode(host)}&ver={AppConsts.AppVersion}&_t={DateTime.Now.Ticks}";
                    var res = await _adeHelper.GetTaskAsync(url, token);
                    if (string.IsNullOrWhiteSpace(res))
                    {
                        LogWriteLine("读取任务异常");
                        await Task.Delay(_appSettings.FetchTaskInterval, token);
                        continue;
                    }

                    JArray? data;
                    try
                    {
                        var json = JObject.Parse(res);
                        data = json["data"] as JArray;
                    }
                    catch (JsonReaderException)
                    {
                        _logger.LogError("ProducerAsync json parse failed: {Response}", res);
                        await Task.Delay(_appSettings.FetchTaskInterval, token);
                        continue;
                    }

                    if (data == null || data.Count == 0)
                    {
                        LogWriteLine("暂无任务");
                        await Task.Delay(_appSettings.FetchTaskInterval, token);
                        continue;
                    }

                    int multiple = Math.Max(1, _appSettings.Multiple);
                    int totalEnqueued = 0;
                    for (int i = 0; i < multiple; i++)
                    {
                        foreach (var item in data)
                        {
                            if (!await writer.WaitToWriteAsync(token))
                                return;

                            await writer.WriteAsync(item, token);
                            totalEnqueued++;
                        }
                    }

                    LogWriteLine($"新增{totalEnqueued}条任务");
                    await Task.Delay(_appSettings.FetchTaskInterval, token);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {

            }
            catch (Exception ex)
            {
                completionError = ex;
                throw;
            }
            finally
            {
                writer.TryComplete(completionError);
            }
        }

        private async Task ConsumerAsync(int consumerId, JToken task, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();

                var parseResult = ParseTask(task);
                if (!parseResult.Success)
                {
                    _logger.LogWarning("ConsumerAsync skip malformed task: {Task}", task?.ToString(Newtonsoft.Json.Formatting.None));
                    return;
                }

                var ctx = parseResult.Context!;
                ApplyUvPvOverrides(ctx);

                ///检测设备接口是否可用
                var check_dev = await GetDeviceForTaskAsync(ctx.OS, ctx.TaskId, 0, token);
                if (check_dev == null)
                {
                    _logger.LogWarning("ConsumerAsync get device failed after retries. taskId={TaskId}, uv={Uv}", ctx.TaskId, 1);
                    return;
                }

                await PrepareProxyContextAsync(ctx, task, token);

                var ipTtlSeconds = _appSettings.IpTtl;
                if (ipTtlSeconds <= 0)
                {
                    _logger.LogWarning("ConsumerAsync invalid IpTtl={IpTtl}, taskId={TaskId}", ipTtlSeconds, ctx.TaskId);
                    return;
                }

                using var ipTtlCts = new CancellationTokenSource(TimeSpan.FromSeconds(ipTtlSeconds));
                using var consumerLinkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, ipTtlCts.Token);
                var consumerToken = consumerLinkedCts.Token;

                for (int uvIndex = 0; uvIndex < ctx.TotalUV; uvIndex++)
                {
                    if (token.IsCancellationRequested)
                        return;

                    try
                    {
                        _aggregator.Enqueue(new TaskEvent(ctx.TaskId, StateType.Request, 1));

                        var dev = uvIndex == 0 ? check_dev : await GetDeviceForTaskAsync(ctx.OS, ctx.TaskId, uvIndex, consumerToken);
                        if (dev == null)
                            continue;


                        NormalizeDevice(dev, ctx.OS);

                        var pluginArgs = BuildPluginArgs(ctx, task, dev, consumerId, uvIndex);

                        bool stopRemainingUv = await ExecutePluginOnceAsync(
                            ctx,
                            pluginArgs,
                            consumerId,
                            uvIndex,
                            consumerToken);

                        if (stopRemainingUv)
                            break;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (OperationCanceledException) when (ipTtlCts.IsCancellationRequested)
                    {
                        LogWriteLine($"任务 {ctx.TaskTitle}[{ctx.TaskId}] 的 IP 总有效时长已到，停止后续 UV。");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "ConsumerAsync uv failed. taskId={TaskId}, uv={Uv}, consumer={ConsumerId}",
                            ctx.TaskId, uvIndex + 1, consumerId);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"ConsumerAsync failed:{ex.Message}");
            }
        }



        /// <summary>
        /// 解析任务
        /// </summary>
        /// <param name="task"></param>
        /// <returns></returns>
        private ParseTaskResult ParseTask(JToken task)
        {
            if (task is not JObject taskObj)
                return new ParseTaskResult { Success = false };

            var taskIdToken = taskObj["id"];
            var url = taskObj["url"]?.Value<string>();
            var totalUvToken = taskObj["uv"];
            var totalPvToken = taskObj["pv"];

            if (taskIdToken == null || totalUvToken == null || totalPvToken == null || string.IsNullOrWhiteSpace(url))
                return new ParseTaskResult { Success = false };

            var devClientId = taskObj["client"]?.Value<string>()?
                .Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? "0";

            var ctx = new ConsumerTaskContext
            {
                TaskId = taskIdToken.Value<int>(),
                Url = url,
                TotalUV = Math.Max(1, totalUvToken.Value<int>()),
                TotalPV = Math.Max(1, totalPvToken.Value<int>()),
                DevClientId = devClientId,
                OS = _adeHelper.GetOS(devClientId),
                TaskTitle = taskObj["title"]?.Value<string>() ?? string.Empty,
                StartTime = DateTime.Now
            };

            return new ParseTaskResult
            {
                Success = true,
                Context = ctx
            };
        }
        /// <summary>
        /// 应用 UV / PV 覆盖配置
        /// </summary>
        /// <param name="ctx"></param>
        private void ApplyUvPvOverrides(ConsumerTaskContext ctx)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(_appSettings.UVOverride))
                {
                    var uvValues = _appSettings.UVOverride.Split(
                        '-',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    if (uvValues.Length > 1 &&
                        int.TryParse(uvValues[0], out var minUv) &&
                        int.TryParse(uvValues[1], out var maxUv) &&
                        maxUv >= minUv)
                    {
                        ctx.TotalUV = CommonHelper.RandomRange(minUv, maxUv + 1);
                    }
                    else if (uvValues.Length == 1 && int.TryParse(uvValues[0], out var uvExact))
                    {
                        ctx.TotalUV = uvExact;
                    }
                }

                if (!string.IsNullOrWhiteSpace(_appSettings.PVOverride))
                {
                    var pvValues = _appSettings.PVOverride.Split(
                        '-',
                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    if (pvValues.Length > 1 &&
                        int.TryParse(pvValues[0], out var minPv) &&
                        int.TryParse(pvValues[1], out var maxPv) &&
                        maxPv >= minPv)
                    {
                        ctx.TotalPV = CommonHelper.RandomRange(minPv, maxPv + 1);
                        if (ctx.TotalUV == 2)
                            ctx.TotalPV = minPv;
                    }
                    else if (pvValues.Length == 1 && int.TryParse(pvValues[0], out var pvExact))
                    {
                        ctx.TotalPV = pvExact;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "ConsumerAsync override parse failed, fallback to task values. taskId={TaskId}",
                    ctx.TaskId);
            }

            ctx.TotalUV = Math.Max(1, ctx.TotalUV);
            ctx.TotalPV = Math.Max(1, ctx.TotalPV);
        }

        #region 代理 / IP 信息
        /// <summary>
        /// 准备代理 / IP 信息
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="task"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task PrepareProxyContextAsync(ConsumerTaskContext ctx, JToken task, CancellationToken token)
        {
            ctx.ProxyServer = null;
            ctx.RealIp = string.Empty;
            ctx.IpInfo = null;

            if (_appSettings.IsProxyMode)
            {
                if (!string.IsNullOrWhiteSpace(_appSettings.ProxyIpUrl))
                {
                    await PrepareRemoteProxyAsync(ctx, task, token);
                }
                else
                {
                    await PrepareLocalProxyAsync(ctx, token);
                }
            }
            else
            {
                await PrepareDirectNetworkIpInfoAsync(ctx, token);
            }
        }
        /// <summary>
        /// 远程代理模式
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="task"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task PrepareRemoteProxyAsync(ConsumerTaskContext ctx, JToken task, CancellationToken token)
        {
            const int maxRetry = 10;

            for (int retry = 1; retry <= maxRetry; retry++)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    _aggregator.EnqueueProxyIpFetched(ctx.TaskId, 1);

                    var ipEntity = await _ipHelper.GetProxyIpAsync(task);
                    if (ipEntity == null)
                    {
                        LogWriteLine("获取IP错误");
                        await Task.Delay(Random.Shared.Next(100, 200), token);
                        continue;
                    }

                    FillProxyServerFromEntity(ctx, ipEntity);

                    if (string.IsNullOrWhiteSpace(ctx.ProxyServer) || !IsValidProxyServer(ctx.ProxyServer))
                    {
                        LogWriteLine($"IP异常,{ctx.ProxyServer}");
                        await Task.Delay(Random.Shared.Next(100, 200), token);
                        continue;
                    }

                    if (_appSettings.GetIpInfo || _appSettings.IsRealIp || _appSettings.IsIpDuplicate)
                    {
                        var ok = await TryFillIpInfoAsync(ctx, token);
                        if (!ok)
                        {
                            LogWriteLine($"无法获取IP信息,{ctx.ProxyServer}");
                            await Task.Delay(Random.Shared.Next(100, 200), token);
                            continue;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(ctx.RealIp))
                    {
                        _aggregator.EnqueueProxyIpConsumed(ctx.TaskId, ctx.RealIp, 1);
                    }

                    return;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    LogWriteLine($"IP异常,{ex.Message}");

                    if (ex.Message.Contains("没有满足您选择的条件IP"))
                        await Task.Delay(Random.Shared.Next(2000, 3000), token);

                    await Task.Delay(Random.Shared.Next(300, 500), token);
                }
            }

            throw new InvalidOperationException($"获取代理 IP 失败，taskId={ctx.TaskId}");
        }
        /// <summary>
        /// 本地代理模式
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task PrepareLocalProxyAsync(ConsumerTaskContext ctx, CancellationToken token)
        {
            ctx.ProxyServer = "127.0.0.1:7890";

            var result = await _ipTester.TestAsync(ctx.ProxyServer);
            if (!result.IsValid)
            {
                LogWriteLine($"无法获取IP信息,{ctx.ProxyServer}");
                throw new InvalidOperationException($"无法获取IP信息,{ctx.ProxyServer}");
            }

            ApplyIpTestResult(ctx, result);
        }
        /// <summary>
        /// 非代理模式
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private async Task PrepareDirectNetworkIpInfoAsync(ConsumerTaskContext ctx, CancellationToken token)
        {
            if (!_appSettings.GetIpInfo && !_appSettings.IsRealIp)
                return;

            var result = await _ipTester.TestAsync(ctx.ProxyServer);
            if (!result.IsValid)
            {
                LogWriteLine($"无法获取IP信息,{ctx.ProxyServer}");
                throw new InvalidOperationException($"无法获取IP信息,{ctx.ProxyServer}");
            }

            ApplyIpTestResult(ctx, result);
        }
        #endregion

        #region 辅助方法：填代理 / 验证代理 / 填 IP 结果
        /// <summary>
        /// 辅助方法：填代理 / 验证代理 / 填 IP 结果
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="ipEntity"></param>
        private void FillProxyServerFromEntity(ConsumerTaskContext ctx, dynamic ipEntity)
        {
            if (ipEntity.format == IPFormat.JSON)
            {
                ctx.ProxyServer = $"{ipEntity.json["ip"]}:{ipEntity.json["port"]}";

                if (_appSettings.IsRealIp)
                {
                    ctx.RealIp =
                        ipEntity.json["rip"]?.ToString() ??
                        ipEntity.json["real_ip"]?.ToString() ??
                        ipEntity.json["realIp"]?.ToString() ??
                        string.Empty;
                }
            }
            else
            {
                ctx.ProxyServer = ipEntity.value;
                if (_appSettings.IsRealIp)
                    ctx.RealIp = ctx.ProxyServer ?? string.Empty;
            }
        }

        /// <summary>
        /// 验证代理1
        /// </summary>
        /// <param name="proxyServer"></param>
        /// <returns></returns>
        private bool IsValidProxyServer(string proxyServer)
        {
            const string pattern = @"(?:(?:[0,1]?\d?\d|2[0-4]\d|25[0-5])\.){3}(?:[0,1]?\d?\d|2[0-4]\d|25[0-5]):\d{1,5}";
            return Regex.IsMatch(proxyServer, pattern);
        }

        /// <summary>
        /// 验证代理2
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<bool> TryFillIpInfoAsync(ConsumerTaskContext ctx, CancellationToken token)
        {
            var result = await _ipTester.TestAsync(ctx.ProxyServer);
            if (!result.IsValid)
                return false;

            ApplyIpTestResult(ctx, result);
            return true;
        }
        /// <summary>
        /// 验证代理3
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="result"></param>

        private void ApplyIpTestResult(ConsumerTaskContext ctx, dynamic result)
        {
            if (result.SuccessUrl.Equals("http://ip-api.com/json") ||
                result.SuccessUrl.Equals("http://117.21.200.221/api/dash/ipinfo.php") ||
                result.SuccessUrl.Equals("http://211.154.24.179:9000/api/dash/ipinfo.php"))
            {
                ctx.IpInfo = JObject.Parse(result.Data);
                ctx.RealIp = ctx.IpInfo["query"]?.Value<string>() ?? string.Empty;
            }
            else
            {
                var ipJson = JObject.Parse(result.Data);

                if (ipJson.ContainsKey("query"))
                    ctx.RealIp = ipJson["query"]?.Value<string>() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(ctx.RealIp) && ipJson.ContainsKey("ip"))
                    ctx.RealIp = ipJson["ip"]?.Value<string>() ?? string.Empty;

                ctx.IpInfo = new JObject
                {
                    ["query"] = ctx.RealIp
                };
            }
        }
        #endregion

        /// <summary>
        /// 获取设备
        /// </summary>
        /// <param name="os"></param>
        /// <param name="taskId"></param>
        /// <param name="uvIndex"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<JToken?> GetDeviceForTaskAsync(OSType os, int taskId, int uvIndex, CancellationToken token)
        {
            for (int retry = 0; retry < 5; retry++)
            {
                token.ThrowIfCancellationRequested();

                var dev = await _adeHelper.GetDeviceAsync(os, 1);
                if (dev != null)
                    return dev;
            }

            _logger.LogWarning(
                "ConsumerAsync get device failed after retries. taskId={TaskId}, uv={Uv}",
                taskId, uvIndex + 1);

            return null;
        }

        /// <summary>
        /// 标准化设备信息
        /// </summary>
        /// <param name="dev"></param>
        /// <param name="os"></param>
        private void NormalizeDevice(JToken dev, OSType os)
        {
            var ua = dev["ua"]?.Value<string>() ?? string.Empty;

            if (os == OSType.ANDROID)
            {

                var m1 = Regex.Match(ua, @"(?<=Android\s+)([\d.]+);([\S\s]+)(?=Build)");
                if (m1.Success && m1.Groups.Count == 3)
                {
                    var model = m1.Groups[2].Value.Trim();
                    model = Regex.Replace(model, "^.*?;\\s*", "");
                    dev["osv"] = m1.Groups[1].Value.Trim();
                    dev["model"] = model.Trim();
                }
                else
                {
                    if (dev.SelectToken("osv") != null && dev.SelectToken("model") != null)
                    {
                        dev["osv"] = dev["osv"]!.Value<string>();
                        dev["model"] = dev["model"]!.Value<string>();
                    }
                    else
                    {
                        dev["osv"] = "13";
                    }

                }

                var m2 = Regex.Match(ua, @"Chrome/([\d.]+)");
                dev["full_version"] = m2.Success && m2.Groups.Count == 2
                    ? m2.Groups[1].Value.Trim()
                    : _appSettings.KernelVersion;
            }
            else if (os == OSType.IOS)
            {
                dev["full_version"] = dev["osv"];
            }
            else if (os == OSType.PC)
            {
                dev["gpu"] = dev["renderer"];
                dev["vendor"] = dev["vender"];

            }
        }

        /// <summary>
        /// 构造插件参数
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="task"></param>
        /// <param name="dev"></param>
        /// <param name="consumerId"></param>
        /// <param name="uvIndex"></param>
        /// <returns></returns>
        private JObject BuildPluginArgs(ConsumerTaskContext ctx, JToken task, JToken dev, int consumerId, int uvIndex)
        {
            var cacheName = $"s{consumerId}_{uvIndex + 1}";

            var args = new JObject
            {
                ["task"] = task,
                ["dev"] = dev,
                ["ipInfo"] = ctx.IpInfo,
                ["isProxyMode"] = _appSettings.IsProxyMode,
                ["proxy_server"] = ctx.ProxyServer,
                ["realIp"] = ctx.RealIp,
                ["isHiddenMode"] = _appSettings.IsHiddenMode,
                ["cacheName"] = cacheName,
                ["processIndex"] = consumerId,
                ["totalPV"] = ctx.TotalPV,
                ["currentUV"] = uvIndex + 1,
                ["pageLoadingTimeout"] = _appSettings.PageLoadingTimeout,
                ["pageloadedDelay"] = _appSettings.PageloadedDelay,
                ["os"] = (int)ctx.OS,
                ["pvsTriggerOne"] = _appSettings.PVsTriggerOne,
                ["isTest"] = _appSettings.IsTest,
                ["kernelVersion"] = _appSettings.KernelVersion,
            };

            return args;
        }

        /// <summary>
        /// 执行插件
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="args"></param>
        /// <param name="consumerId"></param>
        /// <param name="uvIndex"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<bool> ExecutePluginOnceAsync(
        ConsumerTaskContext ctx,
        JObject args,
        int consumerId,
        int uvIndex,
        CancellationToken token)
        {
            if (!allPlugins.TryGetValue(_appSettings.QTPName, out var plugin) || plugin.type == null)
            {
                _logger.LogError("ConsumerAsync plugin not found: {PluginName}", _appSettings.QTPName);
                return false;
            }

            var pluginInstance = Activator.CreateInstance(
                plugin.type,
                new object[] { _domainService, _playwrightProvider, _aggregator, _adeHelper, _nameGenerator, _appSettings });

            if (pluginInstance is not IQTPService pluginService)
            {
                _logger.LogWarning("ConsumerAsync plugin instance invalid. plugin={PluginName}", _appSettings.QTPName);
                return false;
            }

            var uniqueId = Guid.NewGuid().ToString("D");

            EventHandler<PluginLogEventArgs>? logHandler = null;
            EventHandler<TaskStateChangedEventArgs>? stateChangedHandler = null;

            try
            {
                logHandler = (s, e) => LogWriteLine(e);
                stateChangedHandler = (s, e) =>
                {
                    _aggregator.Enqueue(new TaskEvent(e.Id, e.Type, e.Count, e.Data));
                };

                pluginService.OnLogEventHandler += logHandler;
                pluginService.OnStateChangedEventHandler += stateChangedHandler;

                LogWriteLine(
                    $"提交任务:{ctx.TaskTitle}[{ctx.TaskId}_{consumerId}_s{consumerId}_{uvIndex + 1}],os={ctx.OS},proxy={ctx.ProxyServer ?? "False"},realIp={ctx.RealIp},uv={ctx.TotalUV}/{uvIndex + 1}");


                try
                {
                    var (_, isPageTriggerClick, _) =
                        await ExecuteWorkerWithForceStopAsync(pluginService, uniqueId, args, token);

                    token.ThrowIfCancellationRequested();

                    if (ctx.TotalUV > 1 && isPageTriggerClick && _appSettings.UVsTriggerOne)
                        return true;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "ConsumerAsync plugin execute failed. taskId={TaskId}, consumer={ConsumerId}",
                        ctx.TaskId, consumerId);
                }

                return false;
            }
            finally
            {
                try
                {
                    if (pluginService is IAsyncDisposable asyncDisposable)
                    {
                        try
                        {
                            await asyncDisposable.DisposeAsync();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Async dispose plugin failed. uniqueId={UniqueId}", uniqueId);
                        }
                    }
                    else if (pluginService is IDisposable disposable)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Dispose plugin failed. uniqueId={UniqueId}", uniqueId);
                        }
                    }
                }
                finally
                {
                    if (logHandler != null) pluginService.OnLogEventHandler -= logHandler;
                    if (stateChangedHandler != null) pluginService.OnStateChangedEventHandler -= stateChangedHandler;
                }
            }
        }



        private async Task<(bool IsSuccess, bool IsPageTriggerClick, int PageAdsCount)> ExecuteWorkerWithForceStopAsync(
            IQTPService pluginService,
            string uniqueId,
            JObject args,
            CancellationToken token)
        {
            var workerTask = pluginService.ExecuteWorkerAsync(uniqueId, args, token);

            try
            {
                return await workerTask.WaitAsync(token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                await TryForceStopWorkerAsync(pluginService, uniqueId, "Cancellation token was signaled.");

                var completedTask = await Task.WhenAny(workerTask, Task.Delay(TimeSpan.FromSeconds(5)));
                if (completedTask == workerTask)
                {
                    try
                    {
                        await workerTask;
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                    }
                }
                else
                {
                    _logger.LogWarning("Worker did not exit within force-stop grace period. uniqueId={UniqueId}", uniqueId);
                }

                throw;
            }
        }

        private async Task TryForceStopWorkerAsync(IQTPService pluginService, string uniqueId, string reason)
        {
            try
            {
                await pluginService.ForceStopWorkerAsync(uniqueId, reason);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Force stop worker failed. uniqueId={UniqueId}", uniqueId);
            }
        }


        private void InitPipelineRunner()
        {
            int capacity = Math.Max(1, _appSettings.Multiple * _appSettings.MaximumConcurrency);
            int consumerCount = Math.Max(1, _appSettings.MaximumConcurrency);
            _pipeline = new PipelineRunner<JToken>(
                capacity,
                consumerCount,
                ProducerAsync,
                ConsumerAsync
            );
            _pipeline.ProgressChanged += _ =>
            {
                if (IsDisposed || Disposing)
                    return;
            };
            _pipeline.Started += () =>
            {
                this.InvokeOnUiThreadIfRequired(() =>
                {
                    lblStatus.Text = "任务状态：Running";
                });
            };
            _pipeline.Completed += () =>
            {
                this.InvokeOnUiThreadIfRequired(() =>
                {
                    lblStatus.Text = "任务状态：Completed";
                });
            };
            _pipeline.Canceled += () =>
            {
                this.InvokeOnUiThreadIfRequired(() =>
                {
                    lblStatus.Text = "任务状态：Canceled";
                });
            };
            _pipeline.Faulted += ex => _logger.LogError(ex, "Pipeline faulted");
        }

        private async Task StartRunnerAsync()
        {
            string version = comboBox_KernelVersion.Text;
            var chromeDir = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "File", "chrome-win", version, version);

            if (!Directory.Exists(chromeDir))
            {
                await DownloadBrowserAsync(version);
                if (!Directory.Exists(chromeDir))
                {
                    _logger.LogWarning("Chrome kernel missing after download: {ChromeDir}", chromeDir);
                    MessageBox.Show("浏览器内核缺失，请检查下载配置后重试。");
                    return;
                }
            }



            await _aggregator.StartAsync();


            InitPipelineRunner();

            var runner = new UiTaskRunner(token => _pipeline!.RunAsync(token));

            ConfigureRunner(runner);

            _uiRunner = runner;
            _uiRunner.Start();


            _appAutoRestart?.Dispose();
            _appAutoRestart = null;
            var restartInterval = CommonHelper.GetRandomizedInterval(_appSettings.MainResetTimeout, 180);
            _appAutoRestart = new AppAutoRestart(
                restartInterval,
                () =>
                {
                    return _uiRunner != null && _uiRunner.State == RunnerState.Running;
                });

            _appAutoRestart.Start();
        }
        private async Task StopRunnerAsync()
        {
            try
            {
                _appAutoRestart?.Stop();

                if (_uiRunner != null)
                {
                    await _uiRunner.StopAsync();
                }
                await _aggregator.StopAsync();
            }
            finally
            {
                _appAutoRestart = null;
            }
        }
        private void ConfigureRunner(UiTaskRunner runner)
        {
            int clearTick = 0;

            runner.StateChanged += state =>
            {
                this.InvokeOnUiThreadIfRequired(() =>
                {
                    lblStatus.Text = $"任务状态：{state}";
                    btnStartStop.Text = state == RunnerState.Running ? "停止" : "开始";
                });
            };

            runner.Faulted += ex =>
            {
                _logger.LogError(ex, "UiTaskRunner faulted");
            };

            runner.LogEmitted += log =>
            {
                if (_appSettings.IsDetailLog)
                {
                    if (log.Exception == null)
                        _logger.LogInformation("[{Source}] {Message}", log.Source, log.Message);
                    else
                        _logger.LogWarning(log.Exception, "[{Source}] {Message}", log.Source, log.Message);
                }
            };

            // 1秒一次：UI统计刷新
            runner.SetPeriodicAction(
                interval: TimeSpan.FromSeconds(1),
                onTick: async token =>
                {
                    var elapsed = runner.RunElapsed;
                    var totalStats = _aggregator.GetTotalStats();

                    this.InvokeOnUiThreadIfRequired(() =>
                    {
                        label5.Text = $"提交数量:{totalStats.Request}";
                        label6.Text = $"执行数量:{totalStats.Start}";
                        label7.Text = $"曝光数量:{totalStats.DSP}";
                        label8.Text = $"点击数量:{totalStats.Clickthrough}";
                        label9.Text = $"成功数量:{totalStats.Success}";
                        toolStripStatusLabel4.Text = $"执行总量：{QTPTotalStartCount + totalStats.Start}";
                        toolStripStatusLabel5.Text = $"曝光总量：{QTPTotalDspCount + totalStats.DSP}";
                        toolStripStatusLabel6.Text = $"点击总量：{QTPTotalClickthroughCount + totalStats.Clickthrough}";
                        label12.Text = $"运行时长:{elapsed:hh\\:mm\\:ss}";
                    });

                    await Task.CompletedTask;
                },
                name: "RefreshStatsUi",
                skipIfRunning: true,
                timeout: TimeSpan.FromSeconds(2),
                circuitBreakThreshold: 10,
                circuitBreakCooldown: TimeSpan.FromSeconds(30)
            );

            //// 10秒一次：错误弹窗清理
            //runner.SetPeriodicAction(
            //    interval: TimeSpan.FromSeconds(10),
            //    onTick: async token =>
            //    {
            //        await Task.Run(() =>
            //        {
            //            CommonHelper.ClearErrorMsgDialog("node.exe - 应用程序错误");
            //            CommonHelper.ClearErrorMsgDialog("chrome.exe - 应用程序错误");
            //            CommonHelper.ClearErrorMsgDialog("WerFault.exe - 应用程序错误");
            //            clearTick++;
            //            if (clearTick % 6 == 0)
            //            {
            //                CommonHelper.ClearProcesses(new[] { "WerFault" });
            //            }
            //        }, token);
            //    },
            //    name: "ClearCrashDialogs",
            //    skipIfRunning: true,
            //    timeout: TimeSpan.FromSeconds(3),
            //    circuitBreakThreshold: 10,
            //    circuitBreakCooldown: TimeSpan.FromMinutes(1)
            //);
        }


        private async void btnStartStop_Click(object sender, EventArgs e)
        {
            if (!btnStartStop.Enabled)
                return;

            btnStartStop.Enabled = false;

            try
            {
                if (_uiRunner != null && _uiRunner.State is RunnerState.Running or RunnerState.Stopping)
                {
                    await StopRunnerAsync();
                }
                else
                {
                    await StartRunnerAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "btnStartStop_Click failed");
                MessageBox.Show($"启动/停止任务失败: {ex.Message}");
            }
            finally
            {
                this.InvokeOnUiThreadIfRequired(() =>
                {
                    btnStartStop.Enabled = true;
                });

            }
        }

        private Task DownloadBrowserAsync(string version)
        {
            return Task.Run(async () =>
            {
                this.InvokeOnUiThreadIfRequired(() =>
                {
                    toolStripProgressBarDownload.AutoSize = false;
                    toolStripProgressBarDownload.Width = 300;
                    toolStripProgressBarDownload.Visible = true;
                });
                double _lastReportedProgress = 0;
                double MinProgressStep = 1;
                DateTime _lastProgressUpdate = System.DateTime.Now;
                double ProgressUpdateIntervalMs = 1000;
                EventHandler<ProgressEventArgs> handler = (s, e) =>
                {
                    bool isProgressTooSmall = Math.Abs(e.Progress - _lastReportedProgress) < MinProgressStep;
                    bool isTooSoon = (DateTime.Now - _lastProgressUpdate).TotalMilliseconds < ProgressUpdateIntervalMs;
                    bool notFinished = e.Progress < 100;

                    if (isProgressTooSmall && isTooSoon && notFinished)
                    {
                        return;
                    }
                    _lastReportedProgress = e.Progress;
                    _lastProgressUpdate = DateTime.Now;
                    _logger.LogInformation(e.Message);
                    this.InvokeOnUiThreadIfRequired(() =>
                    {
                        toolStripProgressBarDownload.Value = (int)Math.Min(Math.Max(e.Progress, 0), 100);
                    });
                };
                _fileUpdater.ProgressChanged -= handler;
                _fileUpdater.ProgressChanged += handler;
                var zipFilePath = await _fileUpdater.DownloadBrowserAsync(_appSettings.TaskApiUrl, version);
                var chromeDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "File", "chrome-win", version);
                if (!System.IO.Directory.Exists(chromeDir))
                    System.IO.Directory.CreateDirectory(chromeDir);
                ZipFile.ExtractToDirectory(zipFilePath, chromeDir);

                this.InvokeOnUiThreadIfRequired(() =>
                {
                    toolStripProgressBarDownload.Width = 60;
                    toolStripProgressBarDownload.Visible = false;
                });
            });
        }



        private async void button6_Click(object sender, EventArgs e)
        {
            if (comboBox_KernelVersion.Items.Count == 0 || string.IsNullOrWhiteSpace(comboBox_KernelVersion.Text))
            {
                _logger.LogInformation("请先选择要更新的版本！");
                return;
            }
            string version = comboBox_KernelVersion.Text;
            var chromeDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "File", "chrome-win", version, version);
            if (System.IO.Directory.Exists(chromeDir))
            {
                _logger.LogInformation($"浏览器版本{version},已存在！");
                return;
            }
            button6.Enabled = false;
            await DownloadBrowserAsync(version).ContinueWith(t =>
            {
                this.InvokeOnUiThreadIfRequired(() =>
                {
                    button6.Enabled = true;
                });
            });
        }

        private void button2_Click(object sender, EventArgs e)
        {
            SystemCleaner.LogoutComputer();
        }

        private void button3_Click_1(object sender, EventArgs e)
        {
            SystemCleaner.RestartComputer();
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            if (_wsClient != null)
            {
                try
                {
                    await _wsClient.StopAsync();
                    await _wsClient.DisposeAsync();
                }
                catch
                {
                }
            }

            base.OnFormClosing(e);
        }
    }
}
