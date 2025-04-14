using TradingBot.Configuration;
using TradingBot.Models;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace TradingBot.Services
{
    public class TradingService
    {
        private readonly MongoDBService _mongoDbService;
        private readonly KucoinService _kucoinService;
        private readonly TradingSettings _settings;

        public TradingService(MongoDBService mongoDbService, KucoinService kucoinService, TradingSettings settings)
        {
            _mongoDbService = mongoDbService;
            _kucoinService = kucoinService;
            _settings = settings;
            InitializeBalance().Wait();
        }

        private async Task InitializeBalance()
        {
            var currentBalance = await _mongoDbService.GetCurrentBalance();
            if (currentBalance == -1)
            {
                await _mongoDbService.UpdateBalance(_settings.InitialBalance);
            }
        }

        public async Task AnalysingOpenTradesToBeClosed()
        {
            var openTrades = await _mongoDbService.GetOpenTrades();

            // Check existing trades for take profit or stop loss
            foreach (var trade in openTrades)
            {
                if (trade.CloseForcefully)
                {
                    var currentPrice = await _kucoinService.GetCurrentPrice(trade.Symbol);
                    await CloseTrade(trade, currentPrice, TradeStatus.Closed);
                }
                else
                {
                    var currentPrice = await _kucoinService.GetCurrentPrice(trade.Symbol);
                    var profitPercentage = ((currentPrice - trade.EntryPrice) / trade.EntryPrice) * 100;

                    if (profitPercentage >= _settings.TakeProfitPercentage)
                    {
                        // Take profit
                        await CloseTrade(trade, currentPrice, TradeStatus.Closed);
                    }
                    else if (profitPercentage <= -_settings.StopLossPercentage)
                    {
                        // Stop loss
                        await CloseTrade(trade, currentPrice, TradeStatus.StopLoss);
                    }
                }
            }
        }

        public async Task OpenTrade(CoinDetail coin)
        {
            var currentBalance = await _mongoDbService.GetCurrentBalance();
            if (currentBalance < _settings.TradeAmount)
            {
                throw new InvalidOperationException($"Insufficient balance. Required: {_settings.TradeAmount}, Available: {currentBalance}");
            }

            var coinCurrentPrice = await _kucoinService.GetCurrentPrice(coin.Symbol);

            var quantity = _settings.TradeAmount / coinCurrentPrice;
            var trade = new Trade
            {
                Symbol = coin.Symbol,
                EntryPrice = coinCurrentPrice,
                Quantity = quantity,
                EntryTime = DateTime.UtcNow,
                Type = TradeType.Buy,
                Status = TradeStatus.Open,
                CloseForcefully = false
            };

            await _mongoDbService.InsertTrade(trade);
            await _mongoDbService.UpdateBalance(currentBalance - _settings.TradeAmount);
        }

        private async Task CloseTrade(Trade trade, decimal exitPrice, TradeStatus status)
        {
            trade.ExitPrice = exitPrice;
            trade.ExitTime = DateTime.UtcNow;
            trade.Status = status;
            trade.ProfitLoss = (exitPrice - trade.EntryPrice) * trade.Quantity;
            trade.ProfitLossPercentage = ((exitPrice - trade.EntryPrice) / trade.EntryPrice) * 100;

            await _mongoDbService.UpdateTrade(trade);

            var currentBalance = await _mongoDbService.GetCurrentBalance();
            await _mongoDbService.UpdateBalance(currentBalance + _settings.TradeAmount + trade.ProfitLoss);
        }
    }
} 