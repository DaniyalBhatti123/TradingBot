using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TradingBot.Models
{
    public class CoinDetail
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public required string Symbol { get; set; }
        public required string Name { get; set; }
        public decimal FirstPrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal PriceChangePercentage { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool IsActive { get; set; }
    }
} 