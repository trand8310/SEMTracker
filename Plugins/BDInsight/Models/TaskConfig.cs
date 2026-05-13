using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BDInsight.Models
{
    public sealed class TaskConfig
    {
        public string UniqueId { get; set; } = "";
        public JObject TaskArgs { get; set; } = default!;
        public CancellationTokenSource LinkedCts { get; set; } = default!;
        public int TaskId { get; set; }
        public string TaskUrl { get; set; } = "";
        public int SleepMs { get; set; }
        public int PageLoadingTimeoutMs { get; set; }
        public int PageLoadedDelayMs { get; set; }
        public string UserAgent { get; set; } = "";
        public int Os { get; set; }
        public int? DevSw { get; set; }
        public float DeviceScale { get; set; }
        public int Sw { get; set; }
        public int Sh { get; set; }
        public bool PvsTriggerOne { get; set; }
        public int CurrentUV { get; set; }

        public string KernelVersion { get; set; } = "132";
        public int MaxTouchPoints { get; set; }
        public int ProcessIndex { get; set; }

        public bool IsTest { get; set; }
        public int TotalPV { get; set; }

        public string CacheDir { get; set; } = "";
        public string UserDataDir { get; set; } = "";

        public bool IsProxyMode { get; set; }

        public string ProxyServer { get; set; } = "";
    }
}
