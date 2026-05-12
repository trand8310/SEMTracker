
namespace SEM
{
    using Microsoft.Playwright;

    public interface IPlaywrightProvider
    {
        Task<IPlaywright> GetAsync();
    }
}
