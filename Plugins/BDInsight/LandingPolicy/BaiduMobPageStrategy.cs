using BDInsight.Models;
using BDInsight.Swiper;
using SEM;
using SEM.Plugins;

namespace BDInsight.LandingPolicy
{

    public sealed class BaiduMobPageStrategy : ILandingPageStrategy
    {
        private readonly BDInsightTask _owner;
        public BaiduMobPageStrategy(BDInsightTask owner)
        {
            _owner = owner;
        }

        public bool CanHandle(string url) => url.StartsWith("https://m.baidu.com/", StringComparison.OrdinalIgnoreCase);

        public async Task<StepFlow> HandleAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
            var sponsoreds = ctx.Page!.Locator(".ec_ad_results div[class^='fc-'].c-container");
            var count = await sponsoreds.CountAsync();
            if (count > 0)
            {
                var candidates = Enumerable.Range(0, count)
                .OrderBy(_ => Guid.NewGuid())
                .Select(i => sponsoreds.Nth(i))
                .ToList();

                foreach (var sponsored in candidates)
                {
                    token.ThrowIfCancellationRequested();
                    await SwipeEmulator.SwipeToElementAsync(ctx.Page!, ctx.CdpSession!, sponsored);
                    await sponsored.ScrollIntoViewIfNeededAsync();
                    await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                    var click = await _owner.ClickAndDetectNavigationAsync(ctx, sponsored, token);
                    if (click.Navigated)
                    {
                        return StepFlow.Continue;
                    }
                }

            }

            return StepFlow.NextPv;
        }
    }
}
