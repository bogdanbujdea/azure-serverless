using System.Collections.Generic;

namespace CodecampHttpFunction.Trading
{
    public class TradedSymbols
    {
        public static List<MarketInfo> MarketCharts = InitializeMarketCharts();

        private static List<MarketInfo> InitializeMarketCharts()
        {
            return new List<MarketInfo>
            {
                new MarketInfo("XBTUSD", "https://www.tradingview.com/chart/WiAaybp9/",
                    quantity: 2000,
                    leverage: 50,
                    decimalCount: 0,
                    stopLossPercentage: 1M,
                    takeProfitPercentage: 1M,
                    triggerDistance: 2
                )
            };
        }
    }
}