using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TradingBot.Models
{
    public class Balance
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        
        public decimal Amount { get; set; }
        public DateTime LastUpdated { get; set; }
    }
} 