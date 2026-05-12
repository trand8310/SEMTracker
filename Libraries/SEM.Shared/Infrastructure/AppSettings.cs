namespace SEM.Infrastructure
{
    public class AppSettings
    {

        public string QTPName { get; set; }
        public string ProxyIpUrl { get; set; }
        public string TaskApiUrl { get; set; }
        public string DevApiUrl { get; set; }
        public int FetchTaskInterval { get; set; }
        public int UVInterval { get; set; }
        public int MaximumConcurrency { get; set; }
        public int PageLoadingTimeout { get; set; }
        public string TaskName { get; set; }
        public bool IsHiddenMode { get; set; }
        public bool IsProxyMode { get; set; }
        public bool IsRealIp { get; set; }
        /// <summary>
        /// 获取IP详情
        /// </summary>
        public bool GetIpInfo { get; set; }
        public int Multiple { get; set; }
        public int MainResetTimeout { get; set; }
        public int UsingDevIndex { get; set; }
        public bool IsDetailLog { get; set; }
        public string PageloadedDelay { get; set; }
        public bool IsIpDuplicate { get; set; }
        /// <summary>
        /// 本地广告词
        /// </summary>
        public bool UseLocalWord { get; set; }

        public string UVOverride { get; set; }
        public string PVOverride { get; set; }

        public int IpTtl { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int HompageTrigger { get; set; }

        public string WordName { get; set; }

        /// <summary>
        /// 非1688广告优先
        /// </summary>
        public bool PriorityNon1688 { get; set; }
        /// <summary>
        /// 多UV时,仅触发1个广告位
        /// </summary>
        public bool UVsTriggerOne { get; set; }

        /// <summary>
        /// 多PV时,仅触发1个广告位
        /// </summary>
        public bool PVsTriggerOne { get; set; }

        /// <summary>
        /// 内核版本
        /// </summary>
        public string KernelVersion { get; set; }
        /// <summary>
        /// 隐身模式
        /// </summary>
        public bool Incognito { get; set; }


        /// <summary>
        /// 1688手机询价
        /// </summary>
        public bool Rfq1688 { get; set; }
        /// <summary>
        /// 1688 手机询价比例
        /// </summary>
        public int Rfq1688Rate { get; set; }



        /// <summary>
        /// p4psearch
        /// </summary>
        public bool p4psearch { get; set; }
        /// <summary>
        /// p4psearch比例
        /// </summary>
        public int p4psearchRate { get; set; }




        /// <summary>
        /// 不触发1688广告
        /// </summary>
        public bool NoTrigger1688 { get; set; }
        /// <summary>
        /// 清洗广告词
        /// </summary>
        public bool CleaningWords { get; set; }

        /// <summary>
        /// 是否使用动态词库
        /// </summary>
        public bool UseDynamicWord { get; set; }
        /// <summary>
        /// 动态词库类型
        /// </summary>
        public string WordType { get; set; }
        /// <summary>
        /// 动态态库,取词周期
        /// </summary>
        public int FetchRecently { get; set; }
        /// <summary>
        /// 取词频率
        /// </summary>
        public int MinFrequency { get; set; }


        /// <summary>
        /// 详情页不触发下载
        /// </summary>
        public bool NotTriggerDownload { get; set; }
        //NotTriggerDownload


        /// <summary>
        /// 动态词库名称
        /// </summary>
        public string DynamicWordName { get; set; }

        /// <summary>
        /// 小时内去重
        /// </summary>
        public bool DistinctByHour { get; set; }

        public string ExcludeWords { get; set; }

        /// <summary>
        /// 是否测试
        /// </summary>
        public bool IsTest { get; set; }

        /// <summary>
        /// 启动时是否自动执行应用更新
        /// </summary>
        public bool AutoUpdate { get; set; }







    }
}
