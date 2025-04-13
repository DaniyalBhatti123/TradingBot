using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace TradingBot.Models
{
    public class PriceLog
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }
        
        public string Symbol { get; set; }
        public decimal Price { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal PriceChangePercentage { get; set; }
    }
} 