using Microsoft.Extensions.Configuration;
using TradingBot.Models;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TradingBot.Configuration;
using System.Globalization;

namespace TradingBot.Services
{
    public class CandleAnalysisService
    {
        private readonly IMongoCollection<PriceLog> _priceLogsCollection;
        private readonly TradingSettings _tradingSettings;

        public CandleAnalysisService(IConfiguration configuration)
        {
            var mongoSettings = configuration.GetSection("MongoDB").Get<MongoDBSettings>();
            var client = new MongoClient(mongoSettings.ConnectionString);
            var database = client.GetDatabase(mongoSettings.DatabaseName);
            _priceLogsCollection = database.GetCollection<PriceLog>("PriceLogs");
            _tradingSettings = configuration.GetSection("Trading").Get<TradingSettings>();
        }

        public class Candle
        {
            public DateTime CandleStartTimestamp { get; set; }
            public DateTime CandleEndTimestamp { get; set; }
            public decimal OpenPrice { get; set; }
            public decimal ClosePrice { get; set; }
            public bool IsGreen => ClosePrice >= OpenPrice;
        }

        public async Task<bool> ShouldOpenTrade(string symbol, List<PriceLog> priceLogs)
        {
            if(priceLogs.Count == 0) return false;
            var candles = await GetLastCandles(symbol, priceLogs);
            if (candles.Count < _tradingSettings.CandleAnalysis.NumberOfCandles)
                return false;

            return IsValidTradingPattern(candles);
        }

        private async Task<List<Candle>> GetLastCandles(string symbol, List<PriceLog> priceLogs)
        {
            var intervalMinutes = _tradingSettings.CandleAnalysis.CandleIntervalMinutes;
            var currentInterval = DateTime.MinValue;
            Candle currentCandle = null;

            var candles = new List<Candle>();
            var sortedPriceLogs = priceLogs.OrderBy(x => x.Timestamp);
            var candleStartTime = sortedPriceLogs.First().Timestamp;
            var candleEndTime = candleStartTime.AddMinutes(intervalMinutes);
            var tempPriceLogs = new List<PriceLog>();

            foreach (var priceLog in sortedPriceLogs)
            {
                if (priceLog.Timestamp < candleEndTime)
                {
                    tempPriceLogs.Add(priceLog);
                }
                else
                {
                    if (tempPriceLogs.Count > 0)
                    {
                        candles.Add(new Candle
                        {
                            CandleStartTimestamp = tempPriceLogs.First().Timestamp,
                            OpenPrice = tempPriceLogs.First().Price,
                            CandleEndTimestamp = tempPriceLogs.Last().Timestamp,
                            ClosePrice = tempPriceLogs.Last().Price
                        });  // First price in window
                    }

                    // Move window forward until entry fits
                    while (priceLog.Timestamp >= candleEndTime)
                    {
                        candleStartTime = candleEndTime;
                        candleEndTime = candleStartTime.AddMinutes(intervalMinutes);
                    }

                    tempPriceLogs = new List<PriceLog> { priceLog };
                }
            }

            if (tempPriceLogs.Count > 0)
            {
                candles.Add(new Candle
                {
                    CandleStartTimestamp = tempPriceLogs.First().Timestamp,
                    OpenPrice = tempPriceLogs.First().Price,
                    CandleEndTimestamp = tempPriceLogs.Last().Timestamp,
                    ClosePrice = tempPriceLogs.Last().Price
                });
            }

            //foreach (var log in priceLogs)
            //{
            //    // Round down to the nearest interval
            //    var logInterval = log.Timestamp.AddMinutes(-(log.Timestamp.Minute % intervalMinutes))
            //                                  .AddSeconds(-log.Timestamp.Second);

            //    if (currentInterval.Ticks != logInterval.Ticks)
            //    {
            //        if (currentCandle != null)
            //        {
            //            currentCandle.ClosePrice = log.Price;
            //            candles.Add(currentCandle);
            //        }

            //        currentCandle = new Candle
            //        {
            //            Timestamp = logInterval,
            //            OpenPrice = log.Price
            //        };
            //        currentInterval = logInterval;
            //    }
            //}

            // Add the last candle if exists
            //if (currentCandle != null)
            //{
            //    var lastLog = priceLogs.Last();
            //    currentCandle.ClosePrice = lastLog.Price;
            //    candles.Add(currentCandle);
            //}

            return candles.TakeLast(_tradingSettings.CandleAnalysis.NumberOfCandles + 1).ToList();
        }

        private bool IsValidTradingPattern(List<Candle> candles)
        {
            // Check last 3 candles pattern
            var lastThreeCandles = candles.TakeLast(3).ToList();
            if (lastThreeCandles.Count == 3)
            {
                var greenCandlesCount = lastThreeCandles.Count(c => c.IsGreen);
                if (lastThreeCandles.All(c => c.IsGreen) && 
                    greenCandlesCount >= _tradingSettings.CandleAnalysis.GreenCandleThreshold.MinimumGreenCandles)
                {
                    return true;
                }
            }

            // Check last 5 candles pattern
            var lastFiveCandles = candles.TakeLast(5).ToList();
            if (lastFiveCandles.Count == 5 && lastFiveCandles.All(c => c.IsGreen))
            {
                return true;
            }

            return false;
        }
    }
} 