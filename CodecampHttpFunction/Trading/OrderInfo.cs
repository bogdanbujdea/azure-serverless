namespace CodecampHttpFunction.Trading
{
    public class OrderInfo
    {
        public int Quantity { get; set; }

        public decimal ProfitPercentage { get; set; }

        public decimal StartPricePercentage { get; set; }

        public OrderStart OrderStart { get; set; }
    }
}