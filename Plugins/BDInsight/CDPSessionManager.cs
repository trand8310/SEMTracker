
 
namespace SEM.Plugins
{
    using Microsoft.Playwright;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public sealed class CDPSessionManager : IAsyncDisposable
    {
        private sealed class SessionEntry
        {
            public required Lazy<Task<ICDPSession>> LazySession { get; init; }
            public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
            public EventHandler<IPage>? CloseHandler;
        }

        private readonly IBrowserContext _context;
        private readonly ConcurrentDictionary<IPage, SessionEntry> _sessionMap = new();
        private readonly EventHandler<IPage> _contextPageHandler;
        private int _disposeStarted;

        public CDPSessionManager(IBrowserContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            _contextPageHandler = (_, page) => AttachPageCloseHandler(page);
            _context.Page += _contextPageHandler;

            foreach (var existingPage in _context.Pages)
            {
                AttachPageCloseHandler(existingPage);
            }
        }

        public async Task<ICDPSession> GetOrCreateSessionAsync(IPage page)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));

            var entry = _sessionMap.GetOrAdd(page, CreateEntry);

            try
            {
                return await entry.LazySession.Value.ConfigureAwait(false);
            }
            catch
            {
                // 创建失败后把坏缓存清掉，避免后续一直拿到 faulted task
                _sessionMap.TryRemove(new KeyValuePair<IPage, SessionEntry>(page, entry));
                throw;
            }
        }

        public bool ContainsPage(IPage page)
        {
            if (page == null)
                return false;

            return _sessionMap.ContainsKey(page);
        }

        public bool RemoveSession(IPage page)
        {
            if (page == null)
                return false;

            if (!_sessionMap.TryRemove(page, out var entry))
                return false;

            if (entry.CloseHandler != null)
            {
                try { page.Close -= entry.CloseHandler; } catch { }
            }

            _ = DisposeSessionEntryAsync(entry);
            return true;
        }

        public int Count => _sessionMap.Count;

        public IReadOnlyCollection<IPage> GetTrackedPages()
        {
            return _sessionMap.Keys.ToArray();
        }

        public async Task<bool> TryWarmupSessionAsync(IPage page)
        {
            try
            {
                await GetOrCreateSessionAsync(page).ConfigureAwait(false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void Clear()
        {
            foreach (var pair in _sessionMap.ToArray())
            {
                RemoveSession(pair.Key);
            }
        }

        private SessionEntry CreateEntry(IPage page)
        {
            return new SessionEntry
            {
                LazySession = new Lazy<Task<ICDPSession>>(
                    () => CreateSessionCoreAsync(page),
                    LazyThreadSafetyMode.ExecutionAndPublication)
            };
        }

        private void AttachPageCloseHandler(IPage page)
        {
            if (page == null)
                return;

            var entry = _sessionMap.GetOrAdd(page, CreateEntry);
            if (entry.CloseHandler != null)
                return;

            EventHandler<IPage> closeHandler = (_, _) => RemoveSession(page);
            if (Interlocked.CompareExchange(ref entry.CloseHandler, closeHandler, null) == null)
            {
                page.Close += closeHandler;
            }
        }

        private async Task<ICDPSession> CreateSessionCoreAsync(IPage page)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));

            // 页面可能已经关了，提前拦一下
            if (page.IsClosed)
                throw new InvalidOperationException("Page is already closed.");

            return await _context.NewCDPSessionAsync(page).ConfigureAwait(false);
        }

        private static async Task DisposeSessionEntryAsync(SessionEntry entry)
        {
            if (!entry.LazySession.IsValueCreated)
                return;

            try
            {
                var session = await entry.LazySession.Value.ConfigureAwait(false);
                if (session is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                }
                else if (session is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) == 1)
                return;

            try
            {
                _context.Page -= _contextPageHandler;
            }
            catch
            {
            }

            var entries = _sessionMap.ToArray();
            _sessionMap.Clear();

            foreach (var pair in entries)
            {
                if (pair.Value.CloseHandler != null)
                {
                    try { pair.Key.Close -= pair.Value.CloseHandler; } catch { }
                }
            }

            foreach (var pair in entries)
            {
                await DisposeSessionEntryAsync(pair.Value).ConfigureAwait(false);
            }
        }
    }
}
