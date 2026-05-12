using System.Collections.Concurrent;
using System.Threading.Channels;


namespace SEM
{
    public enum CleanupItemType
    {
        File,
        Directory
    }

    public sealed class CleanupItem
    {
        public CleanupItemType Type { get; init; }
        public string Path { get; init; } = "";
    }


    public sealed class FileCleanupQueue : IAsyncDisposable
    {
        private readonly Channel<CleanupItem> _channel = Channel.CreateUnbounded<CleanupItem>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        private readonly CancellationTokenSource _cts = new();
        private readonly Task _worker;

        public FileCleanupQueue()
        {
            _worker = Task.Run(() => ProcessLoopAsync(_cts.Token));
        }

        public bool EnqueueFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return _channel.Writer.TryWrite(new CleanupItem
            {
                Type = CleanupItemType.File,
                Path = path
            });
        }

        public bool EnqueueDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            return _channel.Writer.TryWrite(new CleanupItem
            {
                Type = CleanupItemType.Directory,
                Path = path
            });
        }

        private async Task ProcessLoopAsync(CancellationToken token)
        {
            try
            {
                await foreach (var item in _channel.Reader.ReadAllAsync(token))
                {
                    try
                    {
                        switch (item.Type)
                        {
                            case CleanupItemType.File:
                                await DeleteFileWithRetryAsync(item.Path);
                                break;

                            case CleanupItemType.Directory:
                                await DeleteDirectoryWithRetryAsync(item.Path);
                                break;
                        }

                        // 每删一个稍微停一下，避免瞬时 IO 太猛
                        await Task.Delay(100, token);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        // 忽略单项失败，继续后面的
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常退出
            }
            catch
            {
                // 后台清理线程不要炸出
            }
        }

        private static async Task DeleteFileWithRetryAsync(string filePath)
        {
            var delays = new[]
            {
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10)
        };

            foreach (var delay in delays)
            {
                try
                {
                    if (!File.Exists(filePath))
                        return;

                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay);

                    File.Delete(filePath);
                    return;
                }
                catch
                {
                    // 继续重试
                }
            }
        }

        private static async Task DeleteDirectoryWithRetryAsync(string dirPath)
        {
            var delays = new[]
            {
            TimeSpan.Zero,
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(20)
        };

            foreach (var delay in delays)
            {
                try
                {
                    if (!Directory.Exists(dirPath))
                        return;

                    if (delay > TimeSpan.Zero)
                        await Task.Delay(delay);

                    Directory.Delete(dirPath, recursive: true);
                    return;
                }
                catch
                {
                    // 继续重试
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _channel.Writer.TryComplete();
                _cts.Cancel();
            }
            catch { }

            try
            {
                await _worker;
            }
            catch { }

            _cts.Dispose();
        }
    }

    public static class CleanupCollector
    {
        public static List<CleanupItem> CollectDownloadFiles(string downloadsPath, string[] extensions)
        {
            var result = new List<CleanupItem>();

            try
            {
                if (!Directory.Exists(downloadsPath))
                    return result;

                var extSet = new HashSet<string>(
                    extensions.Select(x => x.StartsWith(".") ? x.ToLowerInvariant() : "." + x.ToLowerInvariant()));

                foreach (var file in Directory.EnumerateFiles(downloadsPath, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        if (extSet.Contains(ext))
                        {
                            result.Add(new CleanupItem
                            {
                                Type = CleanupItemType.File,
                                Path = file
                            });
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        public static List<CleanupItem> CollectDirectories(string parentPath)
        {
            var result = new List<CleanupItem>();

            try
            {
                if (!Directory.Exists(parentPath))
                    return result;

                foreach (var dir in Directory.EnumerateDirectories(parentPath, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        result.Add(new CleanupItem
                        {
                            Type = CleanupItemType.Directory,
                            Path = dir
                        });

                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return result;
        }


        public static List<CleanupItem> CollectPrefixedDirectories(string parentPath, string prefix)
        {
            var result = new List<CleanupItem>();

            try
            {
                if (!Directory.Exists(parentPath))
                    return result;

                foreach (var dir in Directory.EnumerateDirectories(parentPath, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var name = Path.GetFileName(dir);
                        if (!string.IsNullOrWhiteSpace(name) &&
                            name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            result.Add(new CleanupItem
                            {
                                Type = CleanupItemType.Directory,
                                Path = dir
                            });
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }

            return result;
        }

        public static List<CleanupItem> CollectSingleDirectory(string dirPath)
        {
            var result = new List<CleanupItem>();

            try
            {
                if (Directory.Exists(dirPath))
                {
                    result.Add(new CleanupItem
                    {
                        Type = CleanupItemType.Directory,
                        Path = dirPath
                    });
                }
            }
            catch
            {
            }

            return result;
        }
    }
}
