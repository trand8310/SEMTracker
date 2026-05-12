using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SEM.Plugins;

namespace SEM
{
    public interface IQTPService
    {
        string Title { get; }
        Task<(bool, bool, int)> ExecuteWorkerAsync(string uniqueId, JObject taskArgs, CancellationToken token);
        Task ForceStopWorkerAsync(string uniqueId, string reason, CancellationToken token = default);
        public event EventHandler<PluginLogEventArgs>? OnLogEventHandler;
        public event EventHandler<TaskStateChangedEventArgs>? OnStateChangedEventHandler;
        public event EventHandler<TaskAdWordEventArgs>? OnTaskAdWordEventHandler;
        void LogWriteLine(string value, LogLevel level = LogLevel.Information);
        void LogError(string message);
        void QTPExecute(StateType type, int id, int count = 1, string? data = null);
        void QTPExecuteStart(int id, int count, string? data);
        void QTPExecuteDSP(int id, int count, string? data);
        void QTPExecuteClickthrough(int id, int count, string? data);
        void QTPExecuteSuccess(int id, int count, string? data);
        void QTPExecuteFailure(int id, int count, string? data);
        void QTPExecuteComplete(int id, int count, string? data);

    }
}
