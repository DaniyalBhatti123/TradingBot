using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TradingBot.Models
{
    public class Trade
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public required string Symbol { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal ExitPrice { get; set; }
        public decimal Quantity { get; set; }
        public decimal ProfitLoss { get; set; }
        public decimal ProfitLossPercentage { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime? ExitTime { get; set; }
        public TradeType Type { get; set; }
        public TradeStatus Status { get; set; }
    }

    public enum TradeType
    {
        Buy,
        Sell
    }

    public enum TradeStatus
    {
        Open,
        Closed,
        StopLoss
    }
} 