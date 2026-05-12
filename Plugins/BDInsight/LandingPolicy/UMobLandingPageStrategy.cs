

using BDInsight.Models;
using SEM;
using SEM.Plugins;

namespace BDInsight.LandingPolicy
{

    public sealed class UMobLandingPageStrategy : ILandingPageStrategy
    {
        private readonly BDInsightTask _owner;

        public UMobLandingPageStrategy(BDInsightTask owner)
        {
            _owner = owner;
        }

        public bool CanHandle(string url) => url.StartsWith("https://site.u-mob.cn/", StringComparison.OrdinalIgnoreCase);

        public async Task<StepFlow> HandleAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            //await _owner.TouchPageScrollAsync(ctx.Page!, ctx.CdpSession!, CommonHelper.RandomRange(0, 3), PageScrollDirection.Up);
            await Task.Delay(CommonHelper.RandomRange(800, 1200), token);

            var tagItems = ctx.Page!.Locator(".tag-panel .tag-item");
            var count = await tagItems.CountAsync();
            if (count > 0)
            {
                var clickCount = CommonHelper.RandomRange(1, count);
                var indices = Enumerable.Range(0, count)
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(clickCount)
                    .ToList();

                foreach (var i in indices)
                {
                    token.ThrowIfCancellationRequested();
                    await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, tagItems.Nth(i));
                    await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                }
            }

            var button = ctx.Page.Locator(":text('下载')");
            if (await button.CountAsync() > 0)
            {
                if (new[] { 1, 3, 5, 7, 9 }.Contains(CommonHelper.RandomRange(0, 10)))
                {
                    await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, button.First);
                    await Task.Delay(CommonHelper.RandomRange(1500, 2500), token);
                }
            }
            return StepFlow.Continue;
        }
    }
}
