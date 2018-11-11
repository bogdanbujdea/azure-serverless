using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Azure.Management.ContainerInstance.Fluent.Models;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace AzureFunctionUtils
{
    public class AzureContainerManager
    {
        private readonly string _functionDirectory;
        private const int Port = 3000;
        private const string AzureLoginPath = "AzureAuthPath";
        private static string ResourceGroupName = "codecamp-container";
        private static string ContainerGroupName = "chart-image-analyzer";
        private static string ContainerImageApp = "thewindev/chart-analyzer";

        public AzureContainerManager(string functionDirectory)
        {
            _functionDirectory = functionDirectory;
        }

        public void StartImageAnalyzer()
        {
            IAzure azure = GetAzureContext(Environment.GetEnvironmentVariable(AzureLoginPath));
            CreateResourceGroup(azure, ResourceGroupName, Region.EuropeWest);            
            RunTaskBasedContainer(azure, ResourceGroupName, ContainerGroupName, ContainerImageApp, null);
        }

        public void StopImageAnalyzer()
        {
            IAzure azure = GetAzureContext(Environment.GetEnvironmentVariable(AzureLoginPath));
            DeleteResourceGroup(azure, ResourceGroupName);
        }

        public ContainerStatus GetStatus()
        {
            try
            {
                IAzure azure = GetAzureContext(Environment.GetEnvironmentVariable(AzureLoginPath));
                var containerGroup = azure.ContainerGroups.GetByResourceGroup(ResourceGroupName, ContainerGroupName);
                if (containerGroup == null)
                {
                    return ContainerStatus.Missing;
                }

                if (containerGroup.State != "Running")
                {
                    return ContainerStatus.Initializing;
                }

                var startTime = containerGroup.Containers.FirstOrDefault().Value.InstanceView.CurrentState.StartTime.GetValueOrDefault();
                Logger.Info($"Container started at {startTime}");
                if (DateTime.UtcNow.Subtract(startTime).TotalMinutes > 10)
                {
                    Logger.Error($"Container started at {startTime} and exceeded duration");
                    return ContainerStatus.ExceededDuration;
                }
                return ContainerStatus.Running;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return ContainerStatus.Unknown;
            }
        }

        private void DeleteResourceGroup(IAzure azure, string resourceGroupName)
        {
            Logger.Info($"\nDeleting resource group '{resourceGroupName}'...");

            azure.ResourceGroups.DeleteByNameAsync(resourceGroupName);
        }

        private void RunTaskBasedContainer(IAzure azure,
                                                 string resourceGroupName,
                                                 string containerGroupName,
                                                 string containerImage,
                                                 string startCommandLine)
        {
            Logger.Info($"\nCreating container group '{containerGroupName}' with start command '{startCommandLine}'");

            IResourceGroup resGroup = azure.ResourceGroups.GetByName(resourceGroupName);
            Region azureRegion = resGroup.Region;

            var tradedSymbols = new List<object>
            {
                new
                {
                    Market = "XBTUSD",
                    ChartUrl = "https://www.tradingview.com/chart/WiAaybp9/"
                }
            };
            var chartsJson = JsonConvert.SerializeObject(tradedSymbols, Formatting.None, new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() });
            azure.ContainerGroups.Define(containerGroupName)
                .WithRegion(azureRegion)
                .WithExistingResourceGroup(resourceGroupName)
                .WithLinux()
                .WithPublicImageRegistryOnly()
                .WithoutVolume()
                .DefineContainerInstance(containerGroupName + "-1")
                .WithImage(containerImage)
                .WithExternalTcpPort(Port)
                .WithCpuCoreCount(1.0)
                .WithMemorySizeInGB(1)
                .WithEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING", Environment.GetEnvironmentVariable("AzureWebJobsStorage"))
                .WithEnvironmentVariable("charts", chartsJson)
                .WithEnvironmentVariable("SCREENSHOTS_CONTAINER", Environment.GetEnvironmentVariable("ScreenshotsContainer"))
                .WithEnvironmentVariable("TIMEOUT", Environment.GetEnvironmentVariable("Timeout"))
                .WithEnvironmentVariable("FUNCTION_URL", Environment.GetEnvironmentVariable("FunctionUrl"))
                .Attach()
                .WithDnsPrefix(containerGroupName)
                .WithRestartPolicy(ContainerGroupRestartPolicy.Never)
                .CreateAsync();
        }

        private void CreateResourceGroup(IAzure azure, string resourceGroupName, Region azureRegion)
        {
            Logger.Info($"\nCreating resource group '{resourceGroupName}'...");

            azure.ResourceGroups.Define(resourceGroupName)
                .WithRegion(azureRegion)
                .Create();
        }

        private IAzure GetAzureContext(string authFilePath)
        {
            IAzure azure;

            try
            {
                var currentDirectory = Directory.GetCurrentDirectory();
                Logger.Info($"Current directory is: {currentDirectory}");
                Logger.Info($"Current function directory is: {_functionDirectory}");
                var azureFilePath = Path.Combine(_functionDirectory, authFilePath);
                Logger.Info($"Authenticating with Azure using credentials in file at {azureFilePath}");

                azure = Azure.Authenticate(azureFilePath).WithDefaultSubscription();
                var currentSubscription = azure.GetCurrentSubscription();

                Logger.Info($"Authenticated with subscription '{currentSubscription.DisplayName}' (ID: {currentSubscription.SubscriptionId})");
            }
            catch (Exception ex)
            {
                Logger.Error($"\nFailed to authenticate:\n{ex.Message}");

                if (string.IsNullOrEmpty(authFilePath))
                {
                    Logger.Error("Have you set the AZURE_AUTH_LOCATION environment variable?");
                }

                throw;
            }

            return azure;
        }

        public static void StopIfRunning(string contextFunctionAppDirectory)
        {
            var azureContainerManager = new AzureContainerManager(contextFunctionAppDirectory);
            var containerStatus = azureContainerManager.GetStatus();
            if (containerStatus == ContainerStatus.Running)
            {
                azureContainerManager.StopImageAnalyzer();
                Logger.Info($"Container stopped at {DateTime.UtcNow}");
            }
        }
    }
}
