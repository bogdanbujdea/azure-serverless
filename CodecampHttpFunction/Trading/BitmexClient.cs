using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AzureFunctionUtils;
using Bitmex.NET;
using Bitmex.NET.Dtos;
using Bitmex.NET.Models;

namespace CodecampHttpFunction.Trading
{
    public class BitmexClient
    {
        private readonly IBitmexApiService _bitmexApiService;

        public BitmexClient(string env)
        {
            _bitmexApiService = CreateBitmexClient(env);
        }

        public async Task<string> GoLong(MarketInfo marketInfo)
        {
            var tradeInfo = new TradeInfo
            {
                EntryOrder = OrderType.Buy,
                ExitOrder = OrderType.Sell,
                Symbol = marketInfo.Market,
                StopLossPercentage = -marketInfo.StopLossPercentage,
                Leverage = marketInfo.Leverage,
                DecimalCount = marketInfo.DecimalCount,
                Quantity = marketInfo.Quantity,
                TriggerDistanceUnits = marketInfo.TriggerDistanceUnits,
                TakeProfitPercentage = marketInfo.TakeProfitPercentage
            };
            return await Trade(tradeInfo);
        }

        public async Task<string> GoShort(MarketInfo marketInfo)
        {
            var tradeInfo = new TradeInfo
            {
                EntryOrder = OrderType.Sell,
                ExitOrder = OrderType.Buy,
                Symbol = marketInfo.Market,
                StopLossPercentage = marketInfo.StopLossPercentage,
                Quantity = marketInfo.Quantity,
                Leverage = marketInfo.Leverage,
                DecimalCount = marketInfo.DecimalCount,
                TriggerDistanceUnits = -marketInfo.TriggerDistanceUnits,
                TakeProfitPercentage = -marketInfo.TakeProfitPercentage
            };
            return await Trade(tradeInfo);
        }

        private async Task<string> Trade(TradeInfo tradeInfo)
        {
            await PrepareForMarketShift(tradeInfo);
            var orderDto = await ExecuteMarketOrder(tradeInfo);
            Logger.Info($"Market {tradeInfo.EntryOrder} {tradeInfo.Quantity} at {orderDto.Price}, status {orderDto.OrdStatus}");

            var openPositions = await _bitmexApiService.Execute(BitmexApiUrls.Position.GetPosition, new PositionGETRequestParams { Count = 10 });
            var currentPosition = openPositions.FirstOrDefault(o => o.Symbol == tradeInfo.Symbol && o.IsOpen);
            decimal stopLossPrice;

            if (currentPosition != null)
            {
                var diff = orderDto.Price.GetValueOrDefault() - currentPosition.LiquidationPrice.GetValueOrDefault();
                var lossLimit = diff * 0.8M * (-1);
                stopLossPrice = RoundPrice(orderDto.Price.GetValueOrDefault() + lossLimit, 0, tradeInfo.DecimalCount);
                var limitPrice = RoundPrice(orderDto.Price.GetValueOrDefault() + (lossLimit/4), 0, tradeInfo.DecimalCount);
                var limitDto = await ExecuteLimitOrder(tradeInfo, limitPrice);
                Logger.Info($"Limit {tradeInfo.EntryOrder} {tradeInfo.Quantity} at {limitDto.Price}, status {limitDto.OrdStatus}");
            }
            else
            {
                stopLossPrice = RoundPrice(orderDto.Price.GetValueOrDefault(), tradeInfo.StopLossPercentage, tradeInfo.DecimalCount);
            }

            int takeProfitQuantity = (int) (tradeInfo.Quantity * 0.5M);
            var (takeProfitPrice, takeProfitTrigger, takeProfitOrder) = await CreateTakeProfitOrders(orderDto.Price.GetValueOrDefault(), tradeInfo, (int) takeProfitQuantity, tradeInfo.TakeProfitPercentage);
            await CreateTakeProfitOrders(orderDto.Price.GetValueOrDefault(), tradeInfo, (int) (RoundPrice(takeProfitQuantity, -25, tradeInfo.DecimalCount)), tradeInfo.TakeProfitPercentage / 2);
            var stopLossTrigger = stopLossPrice + tradeInfo.TriggerDistanceUnits;

            var stopLossOrder = await ExecuteStopLossOrder(tradeInfo.Symbol, tradeInfo.Quantity, stopLossPrice, stopLossTrigger, tradeInfo.ExitOrder.ToBitmexOrder());
            Logger.Info($"Status for stop loss: {stopLossOrder.OrdStatus}");

            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"{tradeInfo.EntryOrder} {orderDto.OrderQty} {tradeInfo.Symbol}");
            stringBuilder.AppendLine($"Entry price: {orderDto.Price}, {orderDto.OrdStatus}");
            stringBuilder.AppendLine($"Take profit price: {takeProfitPrice}, {takeProfitOrder.OrdStatus}");
            stringBuilder.AppendLine($"Take profit trigger: {takeProfitTrigger}");
            stringBuilder.AppendLine($"Stop loss price: {stopLossPrice}, {stopLossOrder.OrdStatus}");
            stringBuilder.AppendLine($"Stop loss trigger: {stopLossTrigger}");

            return stringBuilder.ToString();
        }

        private async Task<(decimal takeProfitPrice, decimal takeProfitTrigger, OrderDto takeProfitOrder)> CreateTakeProfitOrders(decimal price, TradeInfo tradeInfo, int quantity, decimal profitPercentage)
        {
            var takeProfitPrice = RoundPrice(price, profitPercentage, tradeInfo.DecimalCount);
            var takeProfitTrigger = takeProfitPrice + tradeInfo.TriggerDistanceUnits;
            Logger.Info($"{tradeInfo.Symbol}: Take profit at {takeProfitPrice}");
            var takeProfitOrder = await ExecuteTakeProfitOrder(tradeInfo.Symbol, quantity, takeProfitPrice, takeProfitTrigger, tradeInfo.ExitOrder.ToBitmexOrder());
            Logger.Info($"Status for take profit: {takeProfitOrder.OrdStatus}");
            return (takeProfitPrice, takeProfitTrigger, takeProfitOrder);
        }

        private decimal RoundPrice(decimal stopPrice, decimal percentage, int round)
        {
            return Math.Round(stopPrice * (1 + percentage / 100), round);
        }

        private IBitmexApiService CreateBitmexClient(string env)
        {
            var key = env == "Test"
                ? Environment.GetEnvironmentVariable("bitmexTestnetKey")
                : Environment.GetEnvironmentVariable("bitmexProdKey");
            var secret = env == "Test"
                ? Environment.GetEnvironmentVariable("bitmexTestnetSecret")
                : Environment.GetEnvironmentVariable("bitmexProdSecret");
            var bitmexAuthorization = new BitmexAuthorization
            {
                BitmexEnvironment = env == "Test"
                    ? BitmexEnvironment.Test
                    : BitmexEnvironment.Prod,
                Key = key,
                Secret = secret
            };
            var bitmexApiService = BitmexApiService.CreateDefaultApi(bitmexAuthorization);
            return bitmexApiService;
        }

        private async Task<OrderDto> ExecuteMarketOrder(TradeInfo tradeInfo)
        {
            var orderParams = OrderPOSTRequestParams.CreateSimpleMarket(tradeInfo.Symbol, tradeInfo.Quantity / 4, tradeInfo.EntryOrder.ToBitmexOrder());
            return await _bitmexApiService.Execute(BitmexApiUrls.Order.PostOrder, orderParams);
        }

        private async Task<OrderDto> ExecuteLimitOrder(TradeInfo tradeInfo, decimal limit)
        {
            var orderParams = OrderPOSTRequestParams.CreateSimpleHidenLimit(tradeInfo.Symbol, (int) (tradeInfo.Quantity * 0.75), limit, tradeInfo.EntryOrder.ToBitmexOrder());
            return await _bitmexApiService.Execute(BitmexApiUrls.Order.PostOrder, orderParams);
        }

        private async Task<OrderDto> ExecuteStopLossOrder(string market, int quantity, decimal price, decimal trigger, OrderSide orderSide)
        {
            var apiActionAttributes = new ApiActionAttributes<OrderPOSTRequestParams, OrderDto>("order", HttpMethods.POST);
            Logger.Info($"Stop loss trigger at {trigger}, {orderSide} at {price}");
            return await _bitmexApiService.Execute(apiActionAttributes, new OrderPOSTRequestParams
            {
                Symbol = market,
                Side = Enum.GetName(typeof(OrderSide), orderSide),
                OrderQty = quantity,
                OrdType = "StopLimit",
                StopPx = trigger,
                Price = price,
                ExecInst = "Close,LastPrice",
            });
        }

        private async Task<OrderDto> ExecuteTakeProfitOrder(string market, int quantity, decimal price, decimal trigger, OrderSide orderSide)
        {
            var apiActionAttributes = new ApiActionAttributes<OrderPOSTRequestParams, OrderDto>("order", HttpMethods.POST);
            Logger.Info($"Take profit trigger at {trigger}, {orderSide} at {price}");
            return await _bitmexApiService.Execute(apiActionAttributes, new OrderPOSTRequestParams
            {
                Symbol = market,
                Side = Enum.GetName(typeof(OrderSide), orderSide),
                OrderQty = quantity,
                OrdType = "LimitIfTouched",
                StopPx = trigger,
                Price = price,
                ExecInst = "Close,LastPrice",
                TimeInForce = "GoodTillCancel"
            });
        }

        private async Task PrepareForMarketShift(TradeInfo tradeInfo)
        {
            try
            {
                await _bitmexApiService.Execute(BitmexApiUrls.Order.DeleteOrderAll, new OrderAllDELETERequestParams
                {
                    Symbol = tradeInfo.Symbol
                });
                await _bitmexApiService.Execute(BitmexApiUrls.Order.PostOrder, OrderPOSTRequestParams.ClosePositionByMarket(tradeInfo.Symbol));

                await SetLeverage(tradeInfo.Symbol, tradeInfo.Leverage);
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
            }
        }

        private async Task SetLeverage(string market, int leverage)
        {
            var positionLeveragePostRequestParams = new PositionLeveragePOSTRequestParams();
            positionLeveragePostRequestParams.Leverage = leverage;
            positionLeveragePostRequestParams.Symbol = market;
            await _bitmexApiService.Execute(BitmexApiUrls.Position.PostPositionLeverage, positionLeveragePostRequestParams);
        }
    }
}
