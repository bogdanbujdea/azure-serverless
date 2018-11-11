using Bitmex.NET.Models;
using OrderType = CodecampHttpFunction.Trading.OrderType;

namespace CodecampHttpFunction
{
    public static class Extensions
    {
        public static OrderSide ToBitmexOrder(this OrderType orderType)
        {
            return orderType == OrderType.Buy ? OrderSide.Buy : OrderSide.Sell;
        }
    }
}
