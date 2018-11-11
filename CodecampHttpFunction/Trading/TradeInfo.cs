namespace CodecampHttpFunction.Trading
{
    public class TradeInfo
    {
        public string Symbol { get; set; }

        public OrderType EntryOrder { get; set; }

        public OrderType ExitOrder { get; set; }

        public decimal StopLossPercentage { get; set; }

        public decimal TriggerDistanceUnits { get; set; }

        public decimal TakeProfitPercentage { get; set; }
        public int Leverage { get; set; }
        public int Quantity { get; set; }
        public int DecimalCount { get; set; }

        public decimal StartPricePercentage { get; set; }

        public OrderStart OrderStart { get; set; }
    }
}
