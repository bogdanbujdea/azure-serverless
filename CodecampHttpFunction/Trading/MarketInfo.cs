using System.Collections.Generic;

namespace CodecampHttpFunction.Trading
{
    public class MarketInfo
    {
        public MarketInfo(string market, string chartUrl, int quantity, int leverage, int decimalCount,
            decimal stopLossPercentage, decimal takeProfitPercentage, decimal triggerDistance)
        {
            Market = market;
            ChartUrl = chartUrl;
            Quantity = quantity;
            Leverage = leverage;
            DecimalCount = decimalCount;
            StopLossPercentage = stopLossPercentage;
            TakeProfitPercentage = takeProfitPercentage;
            TriggerDistanceUnits = triggerDistance;
        }


        public int DecimalCount { get; set; }

        public string Market { get; set; }

        public string ChartUrl { get; set; }

        public int Quantity { get; set; }

        public int Leverage { get; set; }

        public decimal StopLossPercentage { get; set; }

        public decimal TakeProfitPercentage { get; set; }

        public decimal TriggerDistanceUnits { get; set; }

        public SignalType SignalType { get; set; }
    }
}