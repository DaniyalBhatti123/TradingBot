using MongoDB.Driver;
using TradingBot.Configuration;
using TradingBot.Models;

namespace TradingBot.Services
{
    public class MongoDBService
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<CoinDetail> _coinDetails;
        private readonly IMongoCollection<PriceLog> _priceLogs;
        private readonly IMongoCollection<Trade> _trades;
        private readonly IMongoCollection<Balance> _balanceCollection;
        private const string BALANCE_ID = "67fafbb20c75e77fdf7cfed9";

        public MongoDBService(MongoDBSettings settings)
        {
            var client = new MongoClient(settings.ConnectionString);
            _database = client.GetDatabase(settings.DatabaseName);
            _coinDetails = _database.GetCollection<CoinDetail>("CoinDetails");
            _priceLogs = _database.GetCollection<PriceLog>("PriceLogs");
            _trades = _database.GetCollection<Trade>("Trades");
            _balanceCollection = _database.GetCollection<Balance>("Balance");
        }

        public async Task<CoinDetail> GetCoinDetail(CoinDetail coinDetail)
        {
            var filter = Builders<CoinDetail>.Filter.Eq(x => x.Symbol, coinDetail.Symbol);
            var result = await _coinDetails.Find(filter).ToListAsync();
            return result.FirstOrDefault() ?? null;
        }

        public async Task UpsertCoinDetail(CoinDetail coinDetail)
        {
            if (string.IsNullOrEmpty(coinDetail.Id))
            {
                await InsertCoinDetail(coinDetail);
            }
            else
            {
                var filter = Builders<CoinDetail>.Filter.Eq(x => x.Symbol, coinDetail.Symbol);
                var options = new ReplaceOptions { IsUpsert = true };
                await _coinDetails.ReplaceOneAsync(filter, coinDetail);
            }
        }

        public async Task InsertCoinDetail(CoinDetail coinDetail)
        {
            await _coinDetails.InsertOneAsync(coinDetail);
        }

        public async Task BulkInsertCoinDetail(List<CoinDetail> coinDetails)
        {
            var writeModels = coinDetails.Select(coinDetail => new InsertOneModel<CoinDetail>(coinDetail)).ToList();
            await _coinDetails.BulkWriteAsync(writeModels);
        }

        public async Task<List<CoinDetail>> GetAllCoinDetails()
        {
            return await _coinDetails.Find(_ => true).ToListAsync();
        }

        public async Task<List<PriceLog>> GetAllPriceLogCollections(DateTime lookbackTime)
        {
            var priceLogs = await _priceLogs
                    .Find(log => log.Timestamp >= lookbackTime)
                    .ToListAsync();

            return priceLogs;
        }

        public async Task InsertPriceLog(PriceLog priceLog)
        {
            await _priceLogs.InsertOneAsync(priceLog);
        }

        public async Task BulkInsertPriceLogs(List<PriceLog> priceLogs)
        {
            var writeModels = priceLogs.Select(priceLog => new InsertOneModel<PriceLog>(priceLog)).ToList();
            await _priceLogs.BulkWriteAsync(writeModels);
        }

        public async Task CleanupOldPriceLogs()
        {
            var threeDaysAgo = DateTime.UtcNow.AddDays(-1);
            var filter = Builders<PriceLog>.Filter.Lt(x => x.Timestamp, threeDaysAgo);
            await _priceLogs.DeleteManyAsync(filter);
        }

        public async Task InsertTrade(Trade trade)
        {
            await _trades.InsertOneAsync(trade);
        }

        public async Task UpdateTrade(Trade trade)
        {
            var filter = Builders<Trade>.Filter.Eq(x => x.Id, trade.Id);
            await _trades.ReplaceOneAsync(filter, trade);
        }

        public async Task<List<Trade>> GetOpenTrades()
        {
            var filter = Builders<Trade>.Filter.Eq(x => x.Status, TradeStatus.Open);
            return await _trades.Find(filter).ToListAsync();
        }

        public async Task<decimal> GetCurrentBalance()
        {
            var balance = await _balanceCollection.Find(b => b.Id == BALANCE_ID).FirstOrDefaultAsync();
            return balance?.Amount ?? -1;
        }

        public async Task UpdateBalance(decimal newBalance)
        {
            var balance = new Balance
            {
                Id = BALANCE_ID,
                Amount = newBalance,
                LastUpdated = DateTime.UtcNow
            };

            var options = new ReplaceOptions { IsUpsert = true };
            await _balanceCollection.ReplaceOneAsync(b => b.Id == BALANCE_ID, balance, options);
        }
    }
} 