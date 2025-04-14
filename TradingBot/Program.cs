using Microsoft.Extensions.Configuration;
using Quartz;
using Quartz.Impl;
using System.Configuration;
using TradingBot.Configuration;
using TradingBot.Models;
using TradingBot.Services;

namespace TradingBot
{
    public class Program
    {
        private static async Task Main(string[] args)
        {
            Console.WriteLine("Starting Trading Bot...");

            // Load configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var appSettings = configuration.Get<AppSettings>() ?? throw new InvalidOperationException("Failed to load application settings.");

            // Initialize services
            var mongoDbService = new MongoDBService(appSettings.MongoDB);
            var kucoinService = new KucoinService();
            var tradingService = new TradingService(mongoDbService, kucoinService, appSettings.Trading);

            // Create scheduler
            var schedulerFactory = new StdSchedulerFactory();
            var scheduler = await schedulerFactory.GetScheduler();
            await scheduler.Start();

            //Schedule price update job(every minute)
            var priceUpdateJob = JobBuilder.Create<PriceUpdateJob>()
                .WithIdentity("priceUpdateJob")
                .Build();

            var priceUpdateTrigger = TriggerBuilder.Create()
                .WithIdentity("priceUpdateTrigger")
                .WithSimpleSchedule()
                .Build();

            await scheduler.ScheduleJob(priceUpdateJob, priceUpdateTrigger);

            var tradingAnalysisJob = JobBuilder.Create<TradingAnalysisJob>()
                .WithIdentity("tradingAnalysisJob")
                .Build();

            var tradingAnalysisTrigger = TriggerBuilder.Create()
                .WithIdentity("tradingAnalysisTrigger")
                .WithSimpleSchedule()
                .Build();

            await scheduler.ScheduleJob(tradingAnalysisJob, tradingAnalysisTrigger);

            var analysingProfitToCloseTradesJob = JobBuilder.Create<AnalysingProfitToCloseTrades>()
                .WithIdentity("analysingProfitToCloseTradesJob")
                .Build();

            var analysingProfitToCloseTradesTrigger = TriggerBuilder.Create()
                .WithIdentity("analysingProfitToCloseTradesTrigger")
                .WithSimpleSchedule()
                .Build();

            await scheduler.ScheduleJob(analysingProfitToCloseTradesJob, analysingProfitToCloseTradesTrigger);

            var cleanupJob = JobBuilder.Create<CleanupJob>()
                .WithIdentity("cleanupJob")
                .Build();

            var cleanupTrigger = TriggerBuilder.Create()
                .WithIdentity("cleanupTrigger")
                .WithSimpleSchedule()
                .Build();

            await scheduler.ScheduleJob(cleanupJob, cleanupTrigger);

            // Schedule price log job (every 10 seconds)
            var priceLogJob = JobBuilder.Create<PriceLogJob>()
                .WithIdentity("priceLogJob")
                .Build();

            var priceLogTrigger = TriggerBuilder.Create()
                .WithIdentity("priceLogTrigger")
                .WithSimpleSchedule()
                .Build();

            await scheduler.ScheduleJob(priceLogJob, priceLogTrigger);

            Console.WriteLine("Trading Bot is running. Press Ctrl+C to exit.");
            for (int i = 0; i < 360; i++)
            {
                Console.WriteLine($"Minute {i + 1} alive at {DateTime.UtcNow}");
                Thread.Sleep(60 * 1000); // Sleep for 1 minute
            }
            Console.ReadLine();
        }
    }

    public class PriceUpdateJob : IJob
    {
        private readonly MongoDBService _mongoDbService;
        private readonly KucoinService _kucoinService;

        public PriceUpdateJob()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var appSettings = configuration.Get<AppSettings>() ?? throw new InvalidOperationException("Failed to load application settings.");
            _mongoDbService = new MongoDBService(appSettings.MongoDB);
            _kucoinService = new KucoinService();
        }

        public async Task Execute(IJobExecutionContext context)
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                try
                {
                    var configuration = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .Build();
                    var appSettings = configuration.Get<AppSettings>() ?? throw new InvalidOperationException("Failed to load application settings.");

                    var coins = await _kucoinService.GetAllCoins();
                    var coinDetails = await _mongoDbService.GetAllCoinDetails();
                    if(coinDetails.Count > 0)
                    {
                        foreach (var coin in coins)
                        {
                            var coinDetail = await _mongoDbService.GetCoinDetail(coin);
                            coin.Id = coinDetail.Id;
                            await _mongoDbService.UpsertCoinDetail(coin);
                        }
                    }
                    else
                    {
                        await _mongoDbService.BulkInsertCoinDetail(coins);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in PriceUpdateJob: {ex.Message}");
                }
                finally
                {
                    await Task.Delay(10 * 1000, context.CancellationToken);
                }
            }
        }
    }

    public class TradingAnalysisJob : IJob
    {
        private readonly TradingService _tradingService;
        private readonly CandleAnalysisService _candleAnalysisService;
        private readonly MongoDBService _mongoDbService;
        private readonly TradingSettings _tradingSettings;

        public TradingAnalysisJob()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var appSettings = configuration.Get<AppSettings>() ?? throw new InvalidOperationException("Failed to load application settings.");
            _mongoDbService = new MongoDBService(appSettings.MongoDB);
            _tradingSettings = appSettings.Trading;
            var kucoinService = new KucoinService();
            _tradingService = new TradingService(_mongoDbService, kucoinService, appSettings.Trading);
            _candleAnalysisService = new CandleAnalysisService(configuration);
        }

        public async Task Execute(IJobExecutionContext context)
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                try
                {
                    var coins = await _mongoDbService.GetAllCoinDetails();
                    var openTrades = await _mongoDbService.GetOpenTrades();
                    var openTradesCoins = openTrades.Select(x => x.Symbol).ToList();
                    openTrades = openTrades.Where(x => !openTradesCoins.Contains(x.Symbol)).ToList();

                    var lookbackTime = DateTime.UtcNow.AddMinutes(-(_tradingSettings.CandleAnalysis.LookbackMinutes + 5));

                    var priceLogs = await _mongoDbService.GetAllPriceLogCollections(lookbackTime);

                    foreach (var coin in coins)
                    {
                        var shouldOpenTrade = await _candleAnalysisService.ShouldOpenTrade(coin.Symbol, priceLogs.Where(log => log.Symbol == coin.Symbol).ToList().OrderBy(log => log.Timestamp).ToList());
                        if (shouldOpenTrade)
                        {
                            Console.WriteLine($"Trading signal detected for {coin.Symbol} - Opening trade...");
                            await _tradingService.OpenTrade(coin);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in TradingAnalysisJob: {ex.Message}");
                }
                finally
                {
                    await Task.Delay(5 * 1000, context.CancellationToken);
                }
            }
        }
    }

    public class AnalysingProfitToCloseTrades : IJob
    {
        private readonly TradingService _tradingService;
        private readonly CandleAnalysisService _candleAnalysisService;
        private readonly MongoDBService _mongoDbService;
        private readonly TradingSettings _tradingSettings;

        public AnalysingProfitToCloseTrades()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var appSettings = configuration.Get<AppSettings>() ?? throw new InvalidOperationException("Failed to load application settings.");
            _mongoDbService = new MongoDBService(appSettings.MongoDB);
            _tradingSettings = appSettings.Trading;
            var kucoinService = new KucoinService();
            _tradingService = new TradingService(_mongoDbService, kucoinService, appSettings.Trading);
            _candleAnalysisService = new CandleAnalysisService(configuration);
        }

        public async Task Execute(IJobExecutionContext context)
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _tradingService.AnalysingOpenTradesToBeClosed();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in TradingAnalysisJob: {ex.Message}");
                }
                finally
                {
                    await Task.Delay(5 * 1000, context.CancellationToken);
                }
            }
        }
    }

    public class CleanupJob : IJob
    {
        private readonly MongoDBService _mongoDbService;

        public CleanupJob()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var appSettings = configuration.Get<AppSettings>() ?? throw new InvalidOperationException("Failed to load application settings.");
            _mongoDbService = new MongoDBService(appSettings.MongoDB);
        }

        public async Task Execute(IJobExecutionContext context)
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _mongoDbService.CleanupOldPriceLogs();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in CleanupJob: {ex.Message}");
                }
                finally
                {
                    await Task.Delay(3600 * 1000, context.CancellationToken);
                }
            }
        }
    }

    public class PriceLogJob : IJob
    {
        private readonly MongoDBService _mongoDbService;
        private readonly KucoinService _kucoinService;

        public PriceLogJob()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var appSettings = configuration.Get<AppSettings>() ?? throw new InvalidOperationException("Failed to load application settings.");
            _mongoDbService = new MongoDBService(appSettings.MongoDB);
            _kucoinService = new KucoinService();
        }

        public async Task Execute(IJobExecutionContext context)
        {
            while (!context.CancellationToken.IsCancellationRequested)
            {
                try
                {
                    var coins = await _kucoinService.GetAllCoins();
                    var priceLogs = coins.Select(coin =>
                    {
                        return new PriceLog
                        {
                            Symbol = coin.Symbol,
                            Price = coin.CurrentPrice,
                            Timestamp = DateTime.UtcNow,
                            PriceChangePercentage = coin.PriceChangePercentage
                        };
                    }).ToList();

                    await _mongoDbService.BulkInsertPriceLogs(priceLogs);
                    Console.WriteLine($"Price logs updated at {DateTime.UtcNow}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in PriceLogJob: {ex.Message}");
                }
                finally
                {
                    await Task.Delay(60 * 1000, context.CancellationToken);
                }
            }
        }
    }
}
