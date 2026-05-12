using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace MainClient.Net;

public static class WsActions
{
    public const string GetConfig = "get_config";
    public const string SetConfig = "set_config";
    public const string Command = "command";
}

public static class WsCommandNames
{
    public const string AppStart = "app_start";
    public const string AppStop = "app_stop";
    public const string MachineRestart = "machine_restart";
    public const string MachineLogoff = "machine_logoff";
    public const string ScreenScreenshot = "screen_screenshot";
    public const string AppScreenshot = "app_screenshot";
}

public enum WsConnectionState
{
    None = 0,
    Connecting = 1,
    Connected = 2,
    Reconnecting = 3,
    Disconnected = 4,
    Stopped = 5
}

public sealed class WsReconnectOptions
{
    /// <summary>
    /// 是否自动重连
    /// </summary>
    public bool EnableAutoReconnect { get; set; } = true;

    /// <summary>
    /// 最大重试次数，<=0 表示无限重试
    /// </summary>
    public int MaxRetryCount { get; set; } = 0;

    /// <summary>
    /// 初始延迟
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// 最大延迟
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 是否使用指数退避
    /// true: 2,4,8,16...
    /// false: 2,4,6,8...
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// 指数退避倍数
    /// </summary>
    public double BackoffFactor { get; set; } = 2.0;

    /// <summary>
    /// 第一次重连是否立即执行
    /// </summary>
    public bool RetryImmediatelyFirstTime { get; set; } = false;
}

public sealed class WsReconnectEventArgs : EventArgs
{
    public int RetryCount { get; init; }
    public TimeSpan Delay { get; init; }
    public string? Reason { get; init; }
    public Exception? Exception { get; init; }
}

public sealed class WsStateChangedEventArgs : EventArgs
{
    public WsConnectionState OldState { get; init; }
    public WsConnectionState NewState { get; init; }
    public string? Reason { get; init; }
}

