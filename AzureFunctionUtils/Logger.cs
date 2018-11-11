using Microsoft.Azure.WebJobs.Host;

namespace AzureFunctionUtils
{
    public static class Logger
    {
        private static TraceWriter _logger;

        public static void Init(TraceWriter logger)
        {
            _logger = logger;
        }

        public static void Error(string message)
        {
            _logger.Error(message);
        }

        public static void Info(string message)
        {
            _logger.Info(message);
        }
    }
}
