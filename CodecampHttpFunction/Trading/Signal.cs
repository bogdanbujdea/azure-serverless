using Microsoft.WindowsAzure.Storage.Table;

namespace CodecampHttpFunction.Trading
{
    public class Signal : TableEntity
    {
        private string _market;

        public Signal()
        {
            PartitionKey = "bitmex";
        }
        public int Id { get; set; }

        public string SignalType { get; set; }

        public string Market
        {
            get => _market;
            set
            {
                _market = value;
                RowKey = $"{_market}-{CryptoTrader.Timestamp}";
            }
        }
    }
}