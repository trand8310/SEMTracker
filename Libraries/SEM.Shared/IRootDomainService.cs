
namespace SEM
{
    public interface IRootDomainService
    {
        Task InitializeAsync();
        bool TryGetRootDomain(string hostOrUrl, out string rootDomain);
    }
}
