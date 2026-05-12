using BDInsight.Models;
namespace BDInsight.LandingPolicy
{
    public sealed class LandingPageStrategyDispatcher
    {
        private readonly List<ILandingPageStrategy> _strategies;

        public LandingPageStrategyDispatcher(IEnumerable<ILandingPageStrategy> strategies)
        {
            _strategies = strategies.ToList();
        }

        public async Task<StepFlow> DispatchAsync(WorkerRunContext ctx, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            var url = ctx.Page?.Url ?? string.Empty;
            foreach (var strategy in _strategies)
            {
                if (strategy.CanHandle(url))
                    return await strategy.HandleAsync(ctx, token);
            }

            return StepFlow.Continue;
        }
    }
}
