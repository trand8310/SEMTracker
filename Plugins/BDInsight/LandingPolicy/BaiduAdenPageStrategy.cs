using BDInsight.Models;
using BDInsight.Swiper;
using SEM;
using SEM.Plugins;

namespace BDInsight.LandingPolicy
{

    public sealed class BaiduAdenPageStrategy : ILandingPageStrategy
    {
        private readonly BDInsightTask _owner;

        public BaiduAdenPageStrategy(BDInsightTask owner)
        {
            _owner = owner;
        }

        public bool CanHandle(string url) => url.StartsWith("https://aden.baidu.com/", StringComparison.OrdinalIgnoreCase);

        public async Task<StepFlow> HandleAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(CommonHelper.RandomRange(800, 1200), token);

            var items = ctx.Page!.Locator("#rh-page-container .rh-page-item.finger");
            var count = await items.CountAsync();
            if (count == 0)
            {
                items = ctx.Page!.Locator("#rh-page-container .rh-page-item");
                count = await items.CountAsync();
            }
            if (count == 0)
            {
                items = ctx.Page!.Locator("#rh-video-container .rh-page-item");
                count = await items.CountAsync();
            }
            if (count > 0)
            {
                var candidates = Enumerable.Range(0, count)
                .OrderBy(_ => Guid.NewGuid())
                .Select(i => items.Nth(i))
                .ToList();

                foreach (var target in candidates)
                {
                    token.ThrowIfCancellationRequested();
                    await SwipeEmulator.SwipeToElementAsync(ctx.Page!, ctx.CdpSession!, target);
                    await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                    var click = await _owner.ClickAndDetectNavigationAsync(ctx, target, token);
                    if (click.Navigated)
                    {
                        return StepFlow.Continue;
                    }
                }
            }
            return StepFlow.Continue;
        }
    }
}
