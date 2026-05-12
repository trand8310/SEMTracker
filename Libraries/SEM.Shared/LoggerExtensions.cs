using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SEM
{
    public static class LoggerExtensions
    {
        public static void LogX5Sec(this ILogger logger, string message)
        {
            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["LogType"] = "X5Sec"
            }))
            {
                logger.LogWarning(message);
            }
        }
    }

}
