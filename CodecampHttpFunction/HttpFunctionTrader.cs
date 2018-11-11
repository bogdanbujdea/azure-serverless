using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AzureFunctionUtils;
using CodecampHttpFunction.Trading;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace CodecampHttpFunction
{
    public static class HttpFunctionTrader
    {
        [FunctionName("HttpFunctionTrader")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log, ExecutionContext context)
        {
            try
            {
                CryptoTrader.Timestamp = DateTime.Now.Ticks;
                Logger.Init(log);
                Logger.Info($"Started function at {DateTime.UtcNow}");
                AzureContainerManager.StopIfRunning(context.FunctionAppDirectory);

                var timestamp = GetParam(req, "timestamp");
                var buyNow = GetParam(req, "symbol");
                var isBullish = GetParam(req, "bullish");
                var env = GetParam(req, "env");
                if (string.IsNullOrEmpty(buyNow) == false)
                {
                    string message;
                    if (string.IsNullOrWhiteSpace(isBullish) == false)
                    {
                        message = await CryptoTrader.CreateTrades(SignalType.Bullish,
                            TradedSymbols.MarketCharts.FirstOrDefault(m => m.Market == buyNow), env);
                    }
                    else
                    {
                        message = await CryptoTrader.CreateTrades(SignalType.Bearish,
                            TradedSymbols.MarketCharts.FirstOrDefault(m => m.Market == buyNow), env);
                    }
                    return req.CreateResponse(HttpStatusCode.OK, message);
                }
                var signalManager = new SignalManager();
                return await signalManager.ProcessImages(timestamp);
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
                return req.CreateResponse(HttpStatusCode.InternalServerError, e.ToString());
            }
            finally
            {
                Logger.Info($"Finished function at {DateTime.UtcNow}");
            }
        }

        private static string GetParam(HttpRequestMessage req, string name)
        {
            return req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, name, StringComparison.OrdinalIgnoreCase) == 0)
                .Value;
        }
    }
}
