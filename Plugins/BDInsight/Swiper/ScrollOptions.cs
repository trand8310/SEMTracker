
namespace BDInsight.Swiper
{
    /// <summary>
    /// 如果你项目里已经有 ScrollOptions，就不要重复定义这个类。
    /// 如果没有，可以直接保留。
    /// </summary>
    public sealed class ScrollOptions
    {
        public HumanScrollMode Mode { get; set; } = HumanScrollMode.Auto;

        /// <summary>
        /// 固定滑动距离，优先级高于 HeightRatio。
        /// </summary>
        public int? DistancePx { get; set; }

        /// <summary>
        /// 按视口高度比例滑动，例如 0.5 表示半屏。
        /// </summary>
        public double? HeightRatio { get; set; }

        /// <summary>
        /// 是否自动混合长滑、短滑、探测滑、微调滑。
        /// </summary>
        public bool EnableAutoMix { get; set; } = true;

        /// <summary>
        /// 是否开启顶部保护。
        /// 手指下滑时，如果页面已经接近顶部，则不再继续滑，避免下拉刷新。
        /// </summary>
        public bool EnableTopProtection { get; set; } = true;

        public int NearTopThresholdPx { get; set; } = 10;

        /// <summary>
        /// 是否由 Helper 指定点数。
        /// false 时交给 SwipeEmulator 自动计算，通常更自然。
        /// </summary>
        public bool EnableCustomPointCount { get; set; } = false;

        public IntRange? PointCountRange { get; set; }

        /// <summary>
        /// 保留字段。
        /// 当前新版底层由 SwipeEmulator 控制 move delay。
        /// </summary>
        public IntRange? DelayRangeMs { get; set; }

        /// <summary>
        /// 保留字段。
        /// 当前新版底层由 SwipeEmulator 控制轨迹抖动。
        /// </summary>
        public FloatRange? JitterRange { get; set; }

        public IntRange? PauseRangeMs { get; set; }

        /// <summary>
        /// SwipeEmulator 内部已经会验证内部滚动容器是否真正滚动。
        /// </summary>
        public bool VerifyScrollChanged { get; set; } = true;

        public int MaxConsecutiveNoMove { get; set; } = 3;
    }
}
