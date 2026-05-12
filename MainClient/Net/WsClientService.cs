using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace MainClient.Net;

public sealed class WsClientService : IAsyncDisposable
{
    private readonly Uri _serverUri;
    private readonly string _clientId;
    private readonly string _token;
    private readonly string _machineName;
    private readonly string _version;
    private readonly string _group;
    private readonly string _localIp;
    private readonly TimeSpan _heartbeatInterval;
    private readonly TimeSpan _heartbeatTimeout;
    private readonly WsReconnectOptions _reconnectOptions;

    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _runTask;

    private readonly ConcurrentDictionary<string, Func<JsonElement, Task<object>>> _handlers = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly object _lifecycleLock = new();

    private Func<object>? _getConfigHandler;
    private Func<JsonElement, Task<object>>? _setConfigHandler;
    private Func<JsonElement, Task<object>>? _appStartHandler;
    private Func<JsonElement, Task<object>>? _appStopHandler;

    private volatile WsConnectionState _state = WsConnectionState.None;
    private long _lastHeartbeatAckUnixMs;
    private int _reconnectAttempt;
    private bool _manualStop;
    private IntPtr _mainWindowHandle = IntPtr.Zero;
    private long _lastAckSeq;

    public WsConnectionState State => _state;

    public event Action<string>? OnLog;
    public event EventHandler<WsStateChangedEventArgs>? OnStateChanged;
    public event Action? OnConnecting;
    public event Action? OnConnected;
    public event Action<string?>? OnDisconnected;
    public event EventHandler<WsReconnectEventArgs>? OnReconnecting;
    public event Action<int>? OnReconnectSucceeded;
    public event Action? OnStopped;
    public event Action<TimeSpan>? OnHeartbeatTimeout;

    public WsClientService(
        string serverUrl,
        string clientId,
        string token,
        string? machineName = null,
        string? version = null,
        string? group = null,
        string? localIp = null,
        TimeSpan? heartbeatInterval = null,
        TimeSpan? heartbeatTimeout = null,
        WsReconnectOptions? reconnectOptions = null)
    {
        _serverUri = new Uri(serverUrl ?? throw new ArgumentNullException(nameof(serverUrl)));
        _clientId = string.IsNullOrWhiteSpace(clientId)
            ? throw new ArgumentException("clientId 不能为空", nameof(clientId))
            : clientId;

        _token = token ?? string.Empty;
        _machineName = machineName ?? Environment.MachineName;
        _version = version ?? "1.0.0";
        _group = group ?? "default";
        _localIp = localIp ?? clientId;

        _heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(20);
        _heartbeatTimeout = heartbeatTimeout ?? TimeSpan.FromSeconds(60);
        _reconnectOptions = reconnectOptions ?? new WsReconnectOptions();

        RegisterBuiltInHandlers();
    }

    public void SetMainWindowHandle(IntPtr handle)
    {
        _mainWindowHandle = handle;
    }

    public void RegisterHandler(string action, Func<JsonElement, Task<object>> handler)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("action 不能为空", nameof(action));

        _handlers[action] = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void RegisterHandler(string action, Func<JsonElement, object> handler)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("action 不能为空", nameof(action));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));

        _handlers[action] = payload => Task.FromResult(handler(payload));
    }

    public void RegisterGetConfigHandler(Func<object> handler)
    {
        _getConfigHandler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void RegisterSetConfigHandler(Func<JsonElement, Task<object>> handler)
    {
        _setConfigHandler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void RegisterSetConfigHandler(Func<JsonElement, object> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        _setConfigHandler = payload => Task.FromResult(handler(payload));
    }

    public void RegisterAppStartHandler(Func<JsonElement, Task<object>> handler)
    {
        _appStartHandler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void RegisterAppStartHandler(Func<JsonElement, object> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        _appStartHandler = args => Task.FromResult(handler(args));
    }

    public void RegisterAppStopHandler(Func<JsonElement, Task<object>> handler)
    {
        _appStopHandler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    public void RegisterAppStopHandler(Func<JsonElement, object> handler)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        _appStopHandler = args => Task.FromResult(handler(args));
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lifecycleLock)
        {
            if (_runTask != null)
                return Task.CompletedTask;

            _manualStop = false;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runTask = Task.Run(() => RunLoopAsync(_cts.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Task? runTask;
        ClientWebSocket? ws;

        lock (_lifecycleLock)
        {
            _manualStop = true;
            _cts?.Cancel();
            runTask = _runTask;
            ws = _ws;
        }

        try
        {
            if (ws != null &&
                (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived))
            {
                await ws.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "client stop",
                    CancellationToken.None);
            }
        }
        catch
        {
        }

        if (runTask != null)
        {
            try { await runTask; } catch { }
        }

        lock (_lifecycleLock)
        {
            _runTask = null;
        }

        SetState(WsConnectionState.Stopped, "manual stop");
        OnStopped?.Invoke();
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Exception? connectException = null;
            string? disconnectReason = null;

            try
            {
                SetState(_reconnectAttempt > 0 ? WsConnectionState.Reconnecting : WsConnectionState.Connecting,
                    _reconnectAttempt > 0 ? "reconnect start" : "connect start");

                if (_reconnectAttempt == 0)
                    OnConnecting?.Invoke();

                _ws?.Dispose();
                _ws = new ClientWebSocket();

                Log($"开始连接: {_serverUri}");
                await _ws.ConnectAsync(_serverUri, token);

                SetState(WsConnectionState.Connected, "connected");
                Log("WebSocket 已连接");

                if (_reconnectAttempt > 0)
                    OnReconnectSucceeded?.Invoke(_reconnectAttempt);
                else
                    OnConnected?.Invoke();

                _reconnectAttempt = 0;
                _lastHeartbeatAckUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                await SendJsonAsync(new
                {
                    type = "register",
                    clientId = _clientId,
                    token = _token,
                    machineName = _machineName,
                    version = _version,
                    group = _group,
                    localIp = _localIp,
                    lastAckSeq = Interlocked.Read(ref _lastAckSeq)
                }, token);

                var receiveTask = ReceiveLoopAsync(token);
                var heartbeatTask = HeartbeatLoopAsync(token);
                var heartbeatWatchTask = HeartbeatWatchLoopAsync(token);

                var completed = await Task.WhenAny(receiveTask, heartbeatTask, heartbeatWatchTask);

                if (completed == receiveTask)
                {
                    disconnectReason = "receive loop ended";
                    try { await receiveTask; } catch (Exception ex) { connectException = ex; }
                }
                else if (completed == heartbeatTask)
                {
                    disconnectReason = "heartbeat loop ended";
                    try { await heartbeatTask; } catch (Exception ex) { connectException = ex; }
                }
                else
                {
                    disconnectReason = "heartbeat timeout";
                    try { await heartbeatWatchTask; } catch (Exception ex) { connectException = ex; }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                connectException = ex;
                disconnectReason = ex.Message;
                Log("连接异常: " + ex.Message);
            }

            if (token.IsCancellationRequested || _manualStop)
                break;

            SetState(WsConnectionState.Disconnected, disconnectReason);
            OnDisconnected?.Invoke(disconnectReason);

            if (!_reconnectOptions.EnableAutoReconnect)
            {
                Log("自动重连已关闭，停止连接循环");
                break;
            }

            _reconnectAttempt++;

            if (_reconnectOptions.MaxRetryCount > 0 &&
                _reconnectAttempt > _reconnectOptions.MaxRetryCount)
            {
                Log($"已超过最大重试次数: {_reconnectOptions.MaxRetryCount}，停止重连");
                break;
            }

            var delay = GetReconnectDelay(_reconnectAttempt);

            OnReconnecting?.Invoke(this, new WsReconnectEventArgs
            {
                RetryCount = _reconnectAttempt,
                Delay = delay,
                Reason = disconnectReason,
                Exception = connectException
            });

            Log($"连接已中断，准备第 {_reconnectAttempt} 次重连，延迟 {delay.TotalSeconds:0.##} 秒");

            try
            {
                if (!(_reconnectOptions.RetryImmediatelyFirstTime && _reconnectAttempt == 1))
                    await Task.Delay(delay, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (!_manualStop)
        {
            SetState(WsConnectionState.Stopped, "run loop exited");
            OnStopped?.Invoke();
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(_heartbeatInterval, token);

            if (_ws == null || _ws.State != WebSocketState.Open)
                return;

            await SendJsonAsync(new
            {
                type = "heartbeat",
                clientId = _clientId,
                time = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }, token);
        }
    }

    private async Task HeartbeatWatchLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), token);

            if (_ws == null || _ws.State != WebSocketState.Open)
                return;

            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var diff = TimeSpan.FromMilliseconds(nowMs - Interlocked.Read(ref _lastHeartbeatAckUnixMs));

            if (diff > _heartbeatTimeout)
            {
                Log($"心跳超时，超过 {diff.TotalSeconds:0.##} 秒未收到 heartbeat_ack");
                OnHeartbeatTimeout?.Invoke(diff);

                try
                {
                    if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
                    {
                        await _ws.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "heartbeat timeout",
                            CancellationToken.None);
                    }
                }
                catch
                {
                }

                return;
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
                return;

            var json = await ReceiveTextAsync(_ws, token);
            if (string.IsNullOrWhiteSpace(json))
                return;

            Log("收到: " + json);
            await HandleMessageAsync(json, token);
        }
    }

    private async Task HandleMessageAsync(string json, CancellationToken token)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var type = root.TryGetProperty("type", out var typeProp)
            ? typeProp.GetString()
            : null;

        switch (type)
        {
            case "register_ack":
                {
                    var success = root.TryGetProperty("success", out var s) && s.GetBoolean();
                    if (root.TryGetProperty("ackSeq", out var ackSeqProp) &&
                        ackSeqProp.ValueKind is JsonValueKind.Number &&
                        ackSeqProp.TryGetInt64(out var ackSeq))
                    {
                        MaxExchange(ref _lastAckSeq, ackSeq);
                    }
                    Log(success ? $"注册成功，clientId={_clientId}" : "注册失败");
                    return;
                }

            case "heartbeat_ack":
                {
                    Interlocked.Exchange(ref _lastHeartbeatAckUnixMs, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    return;
                }

            case "request":
                {
                    await HandleRequestAsync(root, token);
                    return;
                }

            case "error":
                {
                    var message = root.TryGetProperty("message", out var msg)
                        ? msg.GetString()
                        : "unknown_error";
                    Log("服务端错误: " + message);
                    return;
                }

            default:
                Log("未知消息类型: " + type);
                return;
        }
    }

    private async Task HandleRequestAsync(JsonElement root, CancellationToken token)
    {
        var requestId = root.TryGetProperty("requestId", out var requestProp)
            ? requestProp.GetString() ?? ""
            : "";

        var action = root.TryGetProperty("action", out var actionProp)
            ? actionProp.GetString() ?? ""
            : "";

        var payload = root.TryGetProperty("payload", out var payloadProp)
            ? payloadProp
            : default;

        var seq = 0L;
        if (root.TryGetProperty("seq", out var seqProp) &&
            seqProp.ValueKind is JsonValueKind.Number)
        {
            seqProp.TryGetInt64(out seq);
        }

        var currentAck = Interlocked.Read(ref _lastAckSeq);
        if (seq > 0 && seq <= currentAck)
        {
            await SendAckAsync(seq, token);
            Log($"忽略重复请求: requestId={requestId}, seq={seq}, ackSeq={currentAck}");
            return;
        }

        bool success = true;
        string? error = null;
        object resultData;

        try
        {
            if (!_handlers.TryGetValue(action, out var handler))
                throw new InvalidOperationException($"未找到 action 处理器: {action}");

            resultData = await handler(payload);
        }
        catch (Exception ex)
        {
            success = false;
            error = ex.Message;
            resultData = new { };
            Log($"处理请求失败, action={action}, error={ex.Message}");
        }

        await SendJsonAsync(new
        {
            type = "response",
            requestId,
            clientId = _clientId,
            action,
            success,
            error,
            data = resultData
        }, token);

        if (seq > 0)
        {
            await SendAckAsync(seq, token);
        }
    }

    private async Task SendAckAsync(long seq, CancellationToken token)
    {
        await SendJsonAsync(new
        {
            type = "ack",
            clientId = _clientId,
            seq
        }, token);

        MaxExchange(ref _lastAckSeq, seq);
    }

    private static void MaxExchange(ref long target, long candidate)
    {
        while (true)
        {
            var current = Interlocked.Read(ref target);
            if (candidate <= current)
                return;

            var original = Interlocked.CompareExchange(ref target, candidate, current);
            if (original == current)
                return;
        }
    }

    public async Task SendJsonAsync(object obj, CancellationToken token = default)
    {
        if (_ws == null || _ws.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket 未连接");

        var json = JsonSerializer.Serialize(obj);
        var bytes = Encoding.UTF8.GetBytes(json);

        await _sendLock.WaitAsync(token);
        try
        {
            if (_ws == null || _ws.State != WebSocketState.Open)
                throw new InvalidOperationException("WebSocket 未连接");

            await _ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                token);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static async Task<string> ReceiveTextAsync(ClientWebSocket ws, CancellationToken token)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        while (true)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);

            if (result.MessageType == WebSocketMessageType.Close)
            {
                try
                {
                    if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
                    {
                        await ws.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "closed",
                            CancellationToken.None);
                    }
                }
                catch
                {
                }

                return string.Empty;
            }

            ms.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
                break;
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private TimeSpan GetReconnectDelay(int retryCount)
    {
        if (_reconnectOptions.RetryImmediatelyFirstTime && retryCount == 1)
            return TimeSpan.Zero;

        if (!_reconnectOptions.UseExponentialBackoff)
        {
            var ms = _reconnectOptions.InitialDelay.TotalMilliseconds * retryCount;
            ms = Math.Min(ms, _reconnectOptions.MaxDelay.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(Math.Max(ms, 0));
        }

        var delayMs = _reconnectOptions.InitialDelay.TotalMilliseconds *
                      Math.Pow(_reconnectOptions.BackoffFactor, Math.Max(0, retryCount - 1));

        delayMs = Math.Min(delayMs, _reconnectOptions.MaxDelay.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(Math.Max(delayMs, 0));
    }

    private void RegisterBuiltInHandlers()
    {
        _handlers[WsActions.GetConfig] = payload =>
        {
            if (_getConfigHandler == null)
                throw new InvalidOperationException("未注册获取配置处理器");

            var config = _getConfigHandler();

            return Task.FromResult<object>(new
            {
                message = "获取配置成功",
                config
            });
        };

        _handlers[WsActions.SetConfig] = async payload =>
        {
            if (_setConfigHandler == null)
                throw new InvalidOperationException("未注册设置配置处理器");

            var result = await _setConfigHandler(payload);

            return new
            {
                message = "设置配置成功",
                config = result
            };
        };

        _handlers[WsActions.Command] = async payload =>
        {
            if (payload.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("command payload 必须为对象");

            var command = payload.TryGetProperty("command", out var cmdProp)
                ? (cmdProp.GetString() ?? "").Trim()
                : "";

            var args = payload.TryGetProperty("args", out var argsProp)
                ? argsProp
                : default;

            if (string.IsNullOrWhiteSpace(command))
                throw new InvalidOperationException("command 不能为空");

            return await HandleBuiltInCommandAsync(command, args);
        };
    }

    private async Task<object> HandleBuiltInCommandAsync(string command, JsonElement args)
    {
        switch (command)
        {
            case WsCommandNames.ScreenScreenshot:
                {
                    var result = ScreenshotHelper.CaptureFullScreen();
                    return new
                    {
                        command,
                        success = result.Success,
                        status = result.Status,
                        message = result.Message,
                        contentType = result.ContentType,
                        fileName = result.FileName,
                        imageBase64 = result.ImageBase64,
                        width = result.Width,
                        height = result.Height,
                        captureMode = result.CaptureMode
                    };
                }

            case WsCommandNames.AppScreenshot:
                {
                    ScreenshotHelper.CaptureResult result;

                    if (_mainWindowHandle != IntPtr.Zero)
                    {
                        result = ScreenshotHelper.CaptureWindow(_mainWindowHandle);
                    }
                    else
                    {
                        result = ScreenshotHelper.CaptureCurrentProcessMainWindow();
                        if (!result.Success)
                        {
                            result = ScreenshotHelper.CaptureForegroundWindow();
                        }
                    }

                    return new
                    {
                        command,
                        success = result.Success,
                        status = result.Status,
                        message = result.Message,
                        contentType = result.ContentType,
                        fileName = result.FileName,
                        imageBase64 = result.ImageBase64,
                        width = result.Width,
                        height = result.Height,
                        captureMode = result.CaptureMode
                    };
                }

            case WsCommandNames.MachineRestart:
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(500);
                        MachineCommandHelper.Restart();
                    });

                    return new
                    {
                        command,
                        success = true,
                        status = "ok",
                        message = "机器重启命令已下发"
                    };
                }

            case WsCommandNames.MachineLogoff:
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(500);
                        MachineCommandHelper.Logoff();
                    });

                    return new
                    {
                        command,
                        success = true,
                        status = "ok",
                        message = "机器注销命令已下发"
                    };
                }

            case WsCommandNames.AppStart:
                {
                    if (_appStartHandler == null)
                        throw new InvalidOperationException("未注册应用启动处理器");

                    var result = await _appStartHandler(args);
                    return new
                    {
                        command,
                        success = true,
                        status = "ok",
                        result
                    };
                }

            case WsCommandNames.AppStop:
                {
                    if (_appStopHandler == null)
                        throw new InvalidOperationException("未注册应用停止处理器");

                    var result = await _appStopHandler(args);
                    return new
                    {
                        command,
                        success = true,
                        status = "ok",
                        result
                    };
                }

            default:
                throw new InvalidOperationException($"不支持的 command: {command}");
        }
    }

    private void SetState(WsConnectionState newState, string? reason = null)
    {
        var oldState = _state;
        if (oldState == newState)
            return;

        _state = newState;

        OnStateChanged?.Invoke(this, new WsStateChangedEventArgs
        {
            OldState = oldState,
            NewState = newState,
            Reason = reason
        });

        Log($"状态变化: {oldState} -> {newState}" +
            (string.IsNullOrWhiteSpace(reason) ? "" : $"，原因: {reason}"));
    }

    private void Log(string message)
    {
        OnLog?.Invoke($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
        _ws?.Dispose();
        _sendLock.Dispose();
    }
}
