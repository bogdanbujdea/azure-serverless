using System.Collections.Generic;

namespace CodecampHttpFunction.Trading
{
    public class TradedSymbols
    {
        public static List<MarketInfo> MarketCharts = InitializeMarketCharts();

        private static List<MarketInfo> InitializeMarketCharts()
        {
            var btcOrders = GetOrdersForBitcoin();
            var ethOrders = GetOrdersForEthereum();
            return new List<MarketInfo>
            {
                new MarketInfo("XBTUSD", "https://www.tradingview.com/chart/WiAaybp9/",
                    quantity: 800,
                    leverage: 50,
                    decimalCount: 0,
                    stopLossPercentage: 1M,
                    takeProfitPercentage: 1M,
                    triggerDistance: 2,
                    orders: btcOrders
                )
            };
        }
        private static List<OrderInfo> GetOrdersForEthereum()
        {
            var ordersForBitcoin = new List<OrderInfo>();
            ordersForBitcoin.Add(new OrderInfo
            {
                OrderStart = OrderStart.Market,
                ProfitPercentage = 0.5M,
                Quantity = 200
            });
            ordersForBitcoin.Add(new OrderInfo
            {
                OrderStart = OrderStart.Limit,
                ProfitPercentage = 1.5M,
                Quantity = 300,
                StartPricePercentage = 1M
            });
            return ordersForBitcoin;
        }

        private static List<OrderInfo> GetOrdersForBitcoin()
        {
            var ordersForBitcoin = new List<OrderInfo>();
            ordersForBitcoin.Add(new OrderInfo
            {
                OrderStart = OrderStart.Market,
                ProfitPercentage = 0.5M,
                Quantity = 1000
            });
            ordersForBitcoin.Add(new OrderInfo
            {
                OrderStart = OrderStart.Limit,
                ProfitPercentage = 1.5M,
                Quantity = 500,
                StartPricePercentage = 1M
            });
            return ordersForBitcoin;
        }
    }
}