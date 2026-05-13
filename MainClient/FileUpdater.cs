using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace MainClient
{
    public class ProgressEventArgs : EventArgs
    {
        public double Progress { get; }
        public string Message { get; }
        public ProgressEventArgs(double progress, string message = "")
        {
            Progress = progress;
            Message = message;
        }
    }
    public class FileVersionInfo
    {
        public string File { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Text => Path.GetFileNameWithoutExtension(File).Replace("BDInsight_", "");
    }
    public class VersionResponse
    {
        public bool Success { get; set; }
        public string Runtime_Version { get; set; } = string.Empty;
        public List<FileVersionInfo> Data { get; set; } = new();
    }
    public class TResponse<T>
    {
        public bool Success { get; set; }
        public string Runtime_Version { get; set; } = string.Empty;
        public List<T> Data { get; set; } = new();
    }
    public class FileUpdater
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        public const string _apiVersion = "_v2";
        // 事件：进度或状态通知
        public event EventHandler<ProgressEventArgs>? ProgressChanged;

        private void OnProgressChanged(double progress, string message = "")
        {
            ProgressChanged?.Invoke(this, new ProgressEventArgs(progress, message));
        }

        public FileUpdater(HttpClient httpClient, ILogger<FileUpdater> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        // 获取版本列表
        public async Task<VersionResponse?> GetVersionListAsync(string taskApiUrl, CancellationToken token = default)
        {
            try
            {
                var baseUrl = new Uri(taskApiUrl).GetLeftPart(UriPartial.Authority);
                var url = $"{baseUrl}/api{_apiVersion}/update.php?action=get_version_list&runtime_version={AppConsts.AppVersion}&_t={DateTime.Now.Ticks}";
                using var response = await _httpClient.GetAsync(url, token);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(token);
                return JsonConvert.DeserializeObject<VersionResponse>(content);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("GetFileVersionListAsync: 请求被取消");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetFileVersionListAsync failed");
                return null;
            }
        }

        public async Task<VersionResponse?> GetLatestFileWithVersionAsync(string taskApiUrl, CancellationToken token = default)
        {
            try
            {
                //app=smad&runtime_version=1.2.3.4
                var baseUrl = new Uri(taskApiUrl).GetLeftPart(UriPartial.Authority);
                var url = $"{baseUrl}/api{_apiVersion}/update.php?action=get_latest&prefix=BDInsight_&runtime_version={AppConsts.AppVersion}&_t={DateTime.Now.Ticks}";
                using var response = await _httpClient.GetAsync(url, token);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(token);
                return JsonConvert.DeserializeObject<VersionResponse>(content);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("GetLatestFileWithVersionAsync: 请求被取消");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetLatestFileWithVersionAsync failed");
                return null;
            }
        }
 

        public List<string> GetHistoryVersions(string historyDir)
        {
            var result = new List<string>();

            if (!Directory.Exists(historyDir))
                return result;

            var regex = new Regex(@"^v\d+\.\d+\.\d+\.\d+$", RegexOptions.IgnoreCase);
            foreach (var dir in Directory.GetDirectories(historyDir))
            {
                var name = Path.GetFileName(dir);

                if (regex.IsMatch(name))
                {
                    result.Add(name);
                }
            }

            // 可选：按版本号排序（从新到旧 or 从旧到新）
            //result.Sort(CompareVersionDesc);

            return result;
        }


        public async Task<TResponse<string>?> GetBrowserVersionListAsync(string taskApiUrl, CancellationToken token = default)
        {
            try
            {
                var baseUrl = new Uri(taskApiUrl).GetLeftPart(UriPartial.Authority);
                var url = $"{baseUrl}/api{_apiVersion}/update.php?action=get_browser_version_list&runtime_version={AppConsts.AppVersion}&_t={DateTime.Now.Ticks}";
                using var response = await _httpClient.GetAsync(url, token);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync(token);
                return JsonConvert.DeserializeObject<TResponse<string>>(content);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("GetBrowserVersionListAsync: 请求被取消");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetBrowserVersionListAsync failed");
                return null;
            }
        }

        #region 下载方法
        private string ComputeFileHash(string filePath)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = sha256.ComputeHash(fs);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
        public async Task<string> DownloadFileAsync(string taskApiUrl, FileVersionInfo selectedVersion, CancellationToken token = default)
        {
            string baseDir = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar))?.FullName!;
            string packagesDir = Path.Combine(baseDir, "packages");
            if (!Directory.Exists(packagesDir))
            {
                Directory.CreateDirectory(packagesDir);
            }
            string destinationPath = Path.Combine(baseDir, "packages", selectedVersion.File);
            if (File.Exists(destinationPath))
            {
                string localHash = ComputeFileHash(destinationPath);
                if (string.Equals(localHash, selectedVersion.Hash, StringComparison.OrdinalIgnoreCase))
                {
                    OnProgressChanged(100, "文件已存在且校验通过，无需重新下载");
                    return destinationPath;
                }
                else
                {
                    OnProgressChanged(0, "本地文件存在但 hash 不匹配，将重新下载");
                }
            }

            var baseUrl = new Uri(taskApiUrl).GetLeftPart(UriPartial.Authority);
            //http://211.154.24.179:9000
            //var downloadUrl = $"{baseUrl}/upload/{selectedVersion.File}";
            var downloadUrl = $"http://211.154.24.179:9000/upload/{selectedVersion.File}";
            string fileName = Path.GetFileName(downloadUrl);

            OnProgressChanged(0, downloadUrl);
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;

            using var contentStream = await response.Content.ReadAsStreamAsync(token);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            OnProgressChanged(0, "开始下载...");

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                totalRead += bytesRead;
                if (canReportProgress)
                {
                    double percent = (totalRead * 1.0 / totalBytes) * 100;
                    OnProgressChanged(percent, $"下载中... {percent:F1}%");
                }
            }

            OnProgressChanged(100, "下载完成");
            return destinationPath;
        }

        public async Task<string> DownloadBrowserAsync(string taskApiUrl, string version, CancellationToken token = default)
        {
            string downloadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp", "download");
            if (!Directory.Exists(downloadDir))
            {
                Directory.CreateDirectory(downloadDir);
            }
            string destinationPath = Path.Combine(downloadDir, $"chrome_{version}.zip");
            if (File.Exists(destinationPath))
            {
                System.IO.File.Delete(destinationPath);
                //return destinationPath;

            }
            //http://211.154.24.179:9000/

            var baseUrl = new Uri(taskApiUrl).GetLeftPart(UriPartial.Authority);
            var downloadUrl = $"http://211.154.24.179:9000/upload/chrome/{version}.zip";
            string fileName = Path.GetFileName(downloadUrl);

            OnProgressChanged(0, downloadUrl);
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;

            using var contentStream = await response.Content.ReadAsStreamAsync(token);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;

            OnProgressChanged(0, "开始下载...");

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                totalRead += bytesRead;
                if (canReportProgress)
                {
                    double percent = (totalRead * 1.0 / totalBytes) * 100;
                    OnProgressChanged(percent, $"下载中... {percent:F1}%");
                }
            }

            OnProgressChanged(100, "下载完成");
            return destinationPath;
        }




        /// <summary>
        /// /http://211.154.24.179:9000/upload/smaide.zip
        /// </summary>
        /// <param name="sourceDir"></param>
        /// <param name="destDir"></param>
        /// <param name="excludeDirs"></param>
        public async Task<string> DownloadBootstrapAsync(string taskApiUrl, CancellationToken token = default)
        {
            string downloadDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp", "download");
            if (!Directory.Exists(downloadDir))
            {
                Directory.CreateDirectory(downloadDir);
            }
            string destinationPath = Path.Combine(downloadDir, "smaide.zip");
            if (File.Exists(destinationPath))
            {
                System.IO.File.Delete(destinationPath);
            }
            var baseUrl = new Uri(taskApiUrl).GetLeftPart(UriPartial.Authority);
            var downloadUrl = $"http://211.154.24.179:9000/upload/smaide.zip";
            string fileName = Path.GetFileName(downloadUrl);
            using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;
            using var contentStream = await response.Content.ReadAsStreamAsync(token);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, token)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, token);
                totalRead += bytesRead;
                if (canReportProgress)
                {
                    double percent = (totalRead * 1.0 / totalBytes) * 100;
                }
            }
            return destinationPath;
        }



        #endregion

        #region 更新方法
        private void CopyDirectory(string sourceDir, string destDir, string[]? excludeDirs = null)
        {
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(destDir, fileName), true);
            }
            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(dir);
                if (excludeDirs != null && excludeDirs.Any(d => d.Equals(dirName, StringComparison.OrdinalIgnoreCase)))
                    continue;

                CopyDirectory(dir, Path.Combine(destDir, dirName), excludeDirs);
            }
        }



        public void UpdateFromZip(string zipFilePath)
        {
            if (!File.Exists(zipFilePath))
            {
                OnProgressChanged(0, $"更新包不存在: {zipFilePath}");
                return;
            }

            try
            {
                OnProgressChanged(0, "开始备份当前文件...");

                //string versionBackupDir = Path.Combine(HistoryDirectory, DateTime.Now.ToString("yyyyMMdd_HHmmss"), $"BDInsight_v{AppSettings.AppVertion}");
                //Directory.CreateDirectory(versionBackupDir);
                //foreach (var file in Directory.GetFiles(BaseDirectory))
                //{
                //    string fileName = Path.GetFileName(file);
                //    File.Copy(file, Path.Combine(versionBackupDir, fileName), true);
                //}
                //foreach (var dir in Directory.GetDirectories(BaseDirectory))
                //{
                //    string dirName = Path.GetFileName(dir);

                //    // 排除指定顶级目录
                //    if (dirName.Equals("logs", StringComparison.OrdinalIgnoreCase) ||
                //        dirName.Equals("history", StringComparison.OrdinalIgnoreCase) ||
                //        dirName.Equals("temp", StringComparison.OrdinalIgnoreCase))
                //        continue;
                //    string destDir = Path.Combine(versionBackupDir, dirName);
                //    if (dirName.Equals("File", StringComparison.OrdinalIgnoreCase))
                //    {
                //        CopyDirectory(dir, destDir, excludeDirs: new[] { "Cache" });
                //    }
                //    else
                //    {
                //        CopyDirectory(dir, destDir);
                //    }
                //}
                //OnProgressChanged(20, "备份完成");
                //OnUpdateHelper(zipFilePath);
                //// 构造 UpdateHelper 路径
                //string helperPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "UpdateHelper.exe");
                //// 参数：当前程序路径 + 更新 zip 文件路径
                //string args = $"\"{System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName}\" \"{zipFilePath}\"";
                //try
                //{
                //    System.Diagnostics.Process.Start(helperPath, args);
                //    OnProgressChanged(25, "启动更新程序...");

                //    //System.Windows.Forms.Application.Exit(0);
                //}
                //catch (Exception ex)
                //{
                //    OnProgressChanged(0, $"启动更新程序失败: {ex.Message}");
                //}




                //// 清理当前目录
                //OnProgressChanged(30, "清理当前目录...");
                //foreach (var file in Directory.GetFiles(BaseDirectory))
                //{
                //    string fileName = Path.GetFileName(file);
                //    if (fileName.Equals("backup", StringComparison.OrdinalIgnoreCase) ||
                //        fileName.Equals("temp", StringComparison.OrdinalIgnoreCase))
                //        continue;

                //    File.SetAttributes(file, FileAttributes.Normal);
                //    File.Delete(file);
                //}

                //foreach (var dir in Directory.GetDirectories(BaseDirectory))
                //{
                //    string dirName = Path.GetFileName(dir);
                //    if (dirName.Equals("backup", StringComparison.OrdinalIgnoreCase) ||
                //        dirName.Equals("temp", StringComparison.OrdinalIgnoreCase))
                //        continue;

                //    Directory.Delete(dir, true);
                //}

                //OnProgressChanged(50, "清理完成");

                //// 解压 ZIP 文件
                //OnProgressChanged(60, "解压更新包...");
                //ZipFile.ExtractToDirectory(zipFilePath, BaseDirectory);
                //OnProgressChanged(100, "更新完成");

            }
            catch (Exception ex)
            {
                OnProgressChanged(0, $"更新失败: {ex.Message}");
            }
        }
        #endregion

        #region 辅助方法
        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }
        #endregion









    }
}
