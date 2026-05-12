
using BDInsight.Models;


namespace BDInsight.LandingPolicy
{
    public interface ILandingPageStrategy
    {
        bool CanHandle(string url);
        Task<StepFlow> HandleAsync(WorkerRunContext ctx, CancellationToken token);
    }
}
