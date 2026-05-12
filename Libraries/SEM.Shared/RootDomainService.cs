 
namespace SEM
{
    using Nager.PublicSuffix;
    using Nager.PublicSuffix.RuleProviders;
    using Nager.PublicSuffix.RuleProviders.CacheProviders;

    public sealed class RootDomainService : IRootDomainService, IDisposable
    {
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private DomainParser? _domainParser;
        private HttpClient? _httpClient;
        private bool _initialized;

        public async Task InitializeAsync()
        {
            if (_initialized && _domainParser != null)
                return;

            await _initLock.WaitAsync();
            try
            {
                if (_initialized && _domainParser != null)
                    return;

                _httpClient ??= new HttpClient();

                var cacheProvider = new LocalFileSystemCacheProvider();
                var ruleProvider = new CachedHttpRuleProvider(cacheProvider, _httpClient);

                await ruleProvider.BuildAsync();

                _domainParser = new DomainParser(ruleProvider);
                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public bool TryGetRootDomain(string hostOrUrl, out string rootDomain)
        {
            rootDomain = string.Empty;

            if (!_initialized || _domainParser == null)
                return false;

            try
            {
                string host = hostOrUrl;

                if (Uri.TryCreate(hostOrUrl, UriKind.Absolute, out var uri))
                    host = uri.Host;

                host = host.Trim().ToLowerInvariant();

                var info = _domainParser.Parse(host);
                rootDomain = info?.RegistrableDomain ?? string.Empty;

                return !string.IsNullOrWhiteSpace(rootDomain);
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
            _initLock.Dispose();
        }
    }
}
