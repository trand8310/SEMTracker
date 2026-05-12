using BDInsight.Models;
using BDInsight.Swiper;
using SEM;
using SEM.Plugins;


namespace BDInsight.LandingPolicy
{
    public sealed class AiSiteLandingPageStrategy : ILandingPageStrategy
    {
        private readonly BDInsightTask _owner;

        public AiSiteLandingPageStrategy(BDInsightTask owner)
        {
            _owner = owner;
        }

        public bool CanHandle(string url) => url.StartsWith("https://aisite.wejianzhan.com", StringComparison.OrdinalIgnoreCase);

        public async Task<StepFlow> HandleAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(CommonHelper.RandomRange(3000, 5000), token);
            var no_result_title = ctx.Page!.GetByText("抱歉，未能匹配到合适的课程");
            if (await no_result_title.CountAsync() > 0)
            {
                var refreshBtn = ctx.Page!.Locator(".no-result-btn").GetByText("刷新");
                if (await refreshBtn.CountAsync() > 0)
                {
                    await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, refreshBtn.First);
                    await ctx.Page.WaitForTimeoutAsync(2000);
                }
                no_result_title = ctx.Page!.GetByText("抱歉，未能匹配到合适的课程");
                if (await no_result_title.CountAsync() > 0)
                {
                    return StepFlow.Continue;
                }
            }

            var openBtn = ctx.Page!.Locator(".animate-container svg image");
            if (await openBtn.CountAsync() > 0)
            {

                int imageCount = await openBtn.CountAsync();
                await _owner.ClickAndDetectNavigationAsync(ctx, openBtn.Nth(imageCount - 1), token);
                await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
            }

            openBtn = ctx.Page.Locator(".welcome-popup-open-button");
            if (await openBtn.CountAsync() > 0)
            {

                if (new[] { 1, 2, 3, 7, 8, 9 }.Contains(CommonHelper.RandomRange(1, 10)))
                {
                    var clicked = await _owner.ClickAndDetectNavigationAsync(ctx, openBtn.First, token);
                    if (clicked.Navigated)
                        return StepFlow.Continue;
                }
            }

            var closeBtn = ctx.Page.Locator(".close-btn,.close-area .close-icon,.layui-layer-close,.layui-layer-btn");
            if (await closeBtn.CountAsync() > 0)
            {
                await CDPHelper.MouseClickAsync(ctx.Page, ctx.CdpSession!, closeBtn.First);
                await Task.Delay(CommonHelper.RandomRange(3000, 5000), token);
            }



            await HumanScrollHelper.TouchPageLongScrollAsync(
            ctx.Page!,
            ctx.CdpSession!,
            scrollCount: CommonHelper.RandomRange(0, 3),
            direction: PageScrollDirection.Up,
            cancellationToken: token);
            await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
            var offerItems = ctx.Page.Locator(".ad-card-title,.ad-card-image,.ad-card-conv-btn");
            if (await offerItems.CountAsync() > 0)
            {
                int count = await offerItems.CountAsync();
                var offer = offerItems.Nth(CommonHelper.RandomRange(0, count));
                await  SwipeEmulator.SwipeToElementAsync(ctx.Page, ctx.CdpSession!, offer);
                await Task.Delay(CommonHelper.RandomRange(800, 1200), token);
                var click = await _owner.ClickAndDetectNavigationAsync(ctx, offer, token);
                if (click.Navigated)
                {
                    await Task.Delay(CommonHelper.RandomRange(2000, 3000), token);
                }

                return StepFlow.Continue;
            }

            var jsClick = await _owner.TryRandomViewportClickableClickAsync(ctx, token);
            if (!jsClick.Navigated)
                await _owner.TryRandomLinkClickAsync(ctx, "a,img", token);

            return StepFlow.Continue;
        }
    }
}
