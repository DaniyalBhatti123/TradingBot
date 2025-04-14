using Kucoin.Net.Clients;
using TradingBot.Models;

namespace TradingBot.Services
{
    public class KucoinService
    {
        private readonly KucoinRestClient _client;

        public KucoinService()
        {
            _client = new KucoinRestClient();
        }

        public async Task<List<CoinDetail>> GetAllCoins()
        {
            var tickers = await _client.SpotApi.ExchangeData.GetTickersAsync();
            var coins = new List<CoinDetail>();

            if (tickers.Data != null)
            {
                foreach (var ticker in tickers.Data.Data.ToList())
                {
                    if (ticker.Symbol.EndsWith("USDT"))
                    {
                        var coinDetail = new CoinDetail
                        {
                            Symbol = ticker.Symbol,
                            Name = ticker.Symbol.Replace("-USDT", ""),
                            CurrentPrice = ticker.LastPrice ?? 0m,
                            FirstPrice = ticker.LastPrice ?? 0m, // This will be updated if it's a new coin
                            PriceChangePercentage = ticker.ChangePercentage ?? 0m,
                            CreatedAt = DateTime.UtcNow,
                            LastUpdated = DateTime.UtcNow,
                            IsActive = true
                        };
                        coins.Add(coinDetail);
                    }
                }
            }

            return coins;
        }

        public async Task<decimal> GetCurrentPrice(string symbol)
        {
            var ticker = await _client.SpotApi.ExchangeData.GetTickerAsync(symbol);
            return ticker.Data?.LastPrice ?? 0m;
        }
    }
} 