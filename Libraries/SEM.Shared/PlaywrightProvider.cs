

namespace SEM
{
    using Microsoft.Playwright;
    using System.Threading;
    using System;
    using System.Threading.Tasks;


    public sealed class PlaywrightProvider : IPlaywrightProvider, IAsyncDisposable
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private Task<IPlaywright>? _instanceTask;
        private bool _disposed;

        public async Task<IPlaywright> GetAsync()
        {
            ThrowIfDisposed();

            var task = Volatile.Read(ref _instanceTask);
            if (task != null)
                return await AwaitAndResetOnFailureAsync(task).ConfigureAwait(false);

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                ThrowIfDisposed();

                task = _instanceTask;
                if (task == null)
                {
                    task = CreatePlaywrightAsync();
                    Volatile.Write(ref _instanceTask, task);
                }
            }
            finally
            {
                _lock.Release();
            }

            return await AwaitAndResetOnFailureAsync(task).ConfigureAwait(false);
        }

        private static Task<IPlaywright> CreatePlaywrightAsync()
        {
            return Playwright.CreateAsync();
        }

        private async Task<IPlaywright> AwaitAndResetOnFailureAsync(Task<IPlaywright> task)
        {
            try
            {
                return await task.ConfigureAwait(false);
            }
            catch
            {
                await _lock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (ReferenceEquals(_instanceTask, task))
                    {
                        _instanceTask = null;
                    }
                }
                finally
                {
                    _lock.Release();
                }

                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Task<IPlaywright>? taskToDispose = null;

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_disposed)
                    return;

                _disposed = true;
                taskToDispose = _instanceTask;
                _instanceTask = null;
            }
            finally
            {
                _lock.Release();
            }

            if (taskToDispose != null)
            {
                try
                {
                    var pw = await taskToDispose.ConfigureAwait(false);

                    if (pw is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                    }
                    else
                    {
                        pw.Dispose();
                    }
                }
                catch
                {
                    // 如果初始化本身失败，这里不再继续抛出
                }
            }

            _lock.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PlaywrightProvider));
        }
    }
}
