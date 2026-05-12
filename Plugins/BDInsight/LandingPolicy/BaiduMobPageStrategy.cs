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
            var items = ctx.Page!.Locator(".ec_ad_results div[class^='fc-'].c-container");
            var count = await items.CountAsync();
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

                    var text = await target.InnerTextAsync();
                    var box = await target.BoundingBoxAsync();
                    if (box != null)
                        _owner.LogWriteLine($"触发广告位:{text}:({box.X},{box.Y},{box.Width},{box.Height})");
                    else
                        _owner.LogWriteLine($"触发广告位:{text}");


                    var click = await _owner.ClickAndDetectNavigationAsync(ctx, target, token);
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
