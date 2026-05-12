using Microsoft.Extensions.Logging;
 

namespace SEM.Plugins
{
    public class PluginLogEventArgs : EventArgs
    {
        public string Message { get; }
        public LogLevel Level { get; }

        public PluginLogEventArgs(string message, LogLevel level = LogLevel.Information)
        {
            Message = message;
            Level = level;
        }
    }
}
