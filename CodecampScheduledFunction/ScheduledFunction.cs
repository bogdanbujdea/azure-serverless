using System;
using AzureFunctionUtils;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace CodecampScheduledFunction
{
    public static class ScheduledFunction
    {
        [FunctionName("ScheduledFunction")]
        public static void Run([TimerTrigger("0 */50 * * * *", RunOnStartup = true)]TimerInfo myTimer, TraceWriter log, ExecutionContext context)
        {
            try
            {
                Logger.Init(log);
                log.Info($"Starting at {DateTime.Now} in {context.FunctionAppDirectory}");

                var azureContainerManager = new AzureContainerManager(context.FunctionAppDirectory);
                var containerStatus = azureContainerManager.GetStatus();
                Logger.Info($"Status is {containerStatus}");
                switch (containerStatus)
                {
                    case ContainerStatus.Missing:
                        Logger.Info("Starting container");
                        azureContainerManager.StartImageAnalyzer();
                        break;
                    case ContainerStatus.ExceededDuration:
                        azureContainerManager.StopImageAnalyzer();
                        break;
                }

                log.Info($"Finished at {DateTime.Now}");
            }
            catch (Exception e)
            {
                log.Info(e.ToString());
            }
        }
    }
}
