using System;
using System.Linq;
using System.Threading.Tasks;
using AzureFunctionUtils;
using CodecampHttpFunction.ImageAnalysis;
using CodecampHttpFunction.Notifications;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace CodecampHttpFunction.Trading
{
    public class CryptoTrader
    {
        public static long Timestamp = 0;

        public async Task RetrieveAndProcessSignal(MarketInfo marketInfo)
        {
            try
            {
                var bitmapAnalyzer = new BitmapAnalyzer();
                var signalType = await bitmapAnalyzer.GetLastSignal(marketInfo);
                var (lastResult, signalsCount) = await GetLastSignal(marketInfo);
                if (signalType == SignalType.None)
                    return;
                marketInfo.SignalType = signalType;
                await CheckSignalWithLast(signalType, lastResult, marketInfo);
                var table = await GetSignalsTable();
                var insertOperation = TableOperation.Insert(new Signal { Id = signalsCount, SignalType = signalType.ToString().ToLower(), Market = marketInfo.Market });
                await table.ExecuteAsync(insertOperation);
                Logger.Info($"Inserted signal {signalType} for {marketInfo.Market}");
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
            }
        }

        private async Task CheckSignalWithLast(SignalType currentSignal, Signal lastSignal, MarketInfo marketInfo)
        {
            if (lastSignal == null)
            {
                Logger.Info($"First signal for {marketInfo.Market}");
                return;
            }
            if (lastSignal?.SignalType != currentSignal.ToString().ToLower() && currentSignal != SignalType.None)
            {
                if (bool.Parse(Environment.GetEnvironmentVariable("prodIsEnabled")))
                    await CreateTrades(currentSignal, marketInfo, "Prod");
                await CreateTrades(currentSignal, marketInfo, "Test");
            }
        }

        public static async Task<string> CreateTrades(SignalType currentSignal, MarketInfo marketInfo, string env)
        {
            Logger.Info($"Creating trades for env {env}");
            string message;
            var bitmexTestnetClient = new BitmexClient(env);
            if (currentSignal == SignalType.Bullish)
            {
                message = await bitmexTestnetClient.GoLong(marketInfo);
            }
            else if (currentSignal == SignalType.Bearish)
            {
                message = await bitmexTestnetClient.GoShort(marketInfo);
            }
            else
            {
                return "";
            }

            Logger.Info(message);

            await new Mailman().SendMailAsync($"[{env}]-{marketInfo.Market}: {currentSignal} signal", message);
            return message;
        }

        private async Task<(Signal, int)> GetLastSignal(MarketInfo marketInfo)
        {
            var table = await GetSignalsTable();
            var tableOperation = TableOperation.Retrieve<Signal>(Environment.GetEnvironmentVariable("TableName"), "");
            await table.ExecuteAsync(tableOperation);
            var query = new TableQuery<Signal>()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, "bitmex"))
                .Where(TableQuery.GenerateFilterCondition("Market", QueryComparisons.Equal, marketInfo.Market));
            var results = await table.ExecuteQuerySegmentedAsync(query, new TableContinuationToken());
            var signals = results.Results.OrderBy(o => o.Timestamp.DateTime).ToList();
            var lastResult = signals.LastOrDefault();
            return (lastResult, signals.Count);
        }

        private async Task<CloudTable> GetSignalsTable()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference(Environment.GetEnvironmentVariable("TableName"));

            await table.CreateIfNotExistsAsync();
            return table;
        }
    }
}