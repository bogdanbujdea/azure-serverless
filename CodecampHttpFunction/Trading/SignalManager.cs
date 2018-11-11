using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AzureFunctionUtils;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace CodecampHttpFunction.Trading
{
    public class SignalManager
    {
        public async Task<HttpResponseMessage> ProcessImages(string timestamp)
        {
            CryptoTrader.Timestamp = long.Parse(timestamp);
            var linksForImages = GetLinksForImages();
            Logger.Info($"Found {linksForImages.Count} links for timestamp {timestamp}");
            if (linksForImages.Count > 0)
            {
                foreach (var marketChart in TradedSymbols.MarketCharts)
                {
                    marketChart.ChartUrl = linksForImages.FirstOrDefault(l => l.Contains(marketChart.Market));
                    if (string.IsNullOrWhiteSpace(marketChart.ChartUrl))
                        continue;
                    Logger.Info($"Retrieving signal for {marketChart.Market} at {DateTime.UtcNow:f}");
                    var cryptoTrader = new CryptoTrader();
                    await cryptoTrader.RetrieveAndProcessSignal(marketChart);
                }

                Logger.Info($"Signals retrieved at {DateTime.UtcNow}");
                var response = TradedSymbols.MarketCharts.Select(m => new { m.Market, m.SignalType }).ToList();
                var json = JsonConvert.SerializeObject(response);
                var httpResponseMessage = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, Encoding.UTF8,
                        "application/json"),
                    StatusCode = HttpStatusCode.OK
                };
                httpResponseMessage.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                return httpResponseMessage;
            }
            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        }

        private static List<string> GetLinksForImages()
        {
            var storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            var blobClient = storageAccount.CreateCloudBlobClient();
            var containerName = Environment.GetEnvironmentVariable("ScreenshotsContainer");
            Logger.Info($"Searching for images inside {containerName}");
            var tradingImages = blobClient.ListContainers(containerName).FirstOrDefault();
            if (tradingImages != null)
            {
                var blobItems = tradingImages.ListBlobs();
                var matchingBlobs = blobItems.Where(b => b.Uri.AbsoluteUri.Contains(CryptoTrader.Timestamp.ToString())).ToList();
                return matchingBlobs.Select(b => b.StorageUri.PrimaryUri.ToString()).ToList();
            }
            return new List<string>();
        }
    }
}
