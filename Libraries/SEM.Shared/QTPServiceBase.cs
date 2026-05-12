using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SEM.Infrastructure;
using SEM.Plugins;


namespace SEM
{
    public abstract class QTPServiceBase : IQTPService
    {
        public abstract string Title { get; }
        public abstract Task<(bool, bool, int)> ExecuteWorkerAsync(string uniqueId, JObject taskArgs, CancellationToken token);
        public virtual Task ForceStopWorkerAsync(string uniqueId, string reason, CancellationToken token = default) => Task.CompletedTask;
        public readonly AppSettings _appSettings;
        public event EventHandler<PluginLogEventArgs>? OnLogEventHandler;
        public event EventHandler<TaskStateChangedEventArgs>? OnStateChangedEventHandler;
        public event EventHandler<TaskAdWordEventArgs>? OnTaskAdWordEventHandler;

        public QTPServiceBase(AppSettings appSettings)
        {
            this._appSettings = appSettings;
        }


        public virtual void LogWriteLine(string message, LogLevel level = LogLevel.Information)
        {
            OnLogEventHandler?.Invoke(this, new PluginLogEventArgs(message, level));
        }
        public virtual void LogError(string message)
        {
            OnLogEventHandler?.Invoke(this, new PluginLogEventArgs(message, LogLevel.Error));
        }

        public virtual void QTPExecute(StateType type, int id, int count = 1, string? data = null)
        {
            OnStateChangedEventHandler?.Invoke(this, new TaskStateChangedEventArgs(type, id, count, data));
        }

        /// <summary>
        /// 执行
        /// </summary>
        /// <param name="value"></param>
        public virtual void QTPExecuteStart(int id, int count = 1, string? data = null)
        {
            OnStateChangedEventHandler?.Invoke(this, new TaskStateChangedEventArgs(StateType.Start, id, count, data));
        }

        public void QTPExecuteDSP(int id, int count = 1, string? data = null)
        {
            OnStateChangedEventHandler?.Invoke(this, new TaskStateChangedEventArgs(StateType.DSP, id, count, data));
        }
        /// <summary>
        /// 点击
        /// </summary>
        /// <param name="value"></param>
        public virtual void QTPExecuteClickthrough(int id, int count = 1, string? data = null)
        {
            OnStateChangedEventHandler?.Invoke(this, new TaskStateChangedEventArgs(StateType.Clickthrough, id, count, data));
        }
        /// <summary>
        /// 成功
        /// </summary>
        /// <param name="value"></param>
        public virtual void QTPExecuteSuccess(int id, int count = 1, string? data = null)
        {
            OnStateChangedEventHandler?.Invoke(this, new TaskStateChangedEventArgs(StateType.Success, id, count, data));
        }
        /// <summary>
        /// 失败
        /// </summary>
        /// <param name="value"></param>
        public virtual void QTPExecuteFailure(int id, int count = 1, string? data = null)
        {
            OnStateChangedEventHandler?.Invoke(this, new TaskStateChangedEventArgs(StateType.Failure, id, count, data));
        }
        /// <summary>
        /// 完成
        /// </summary>
        /// <param name="value"></param>
        public virtual void QTPExecuteComplete(int id, int count = 1, string? data = null)
        {
            OnStateChangedEventHandler?.Invoke(this, new TaskStateChangedEventArgs(StateType.Complete, id, count, data));
        }

        public virtual void X5Secdata(int id, int count = 0, string? data = null)
        {
            OnStateChangedEventHandler?.Invoke(this, new TaskStateChangedEventArgs(StateType.X5Sec, id, count, data));
        }

        public virtual void QTPUploadAdWord(string type, string word)
        {
            OnTaskAdWordEventHandler?.Invoke(this, new TaskAdWordEventArgs(type, word));
        }
    }
}
