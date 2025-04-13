using TradingBot.Services;

namespace TradingBot.Configuration
{
    public class AppSettings
    {
        public MongoDBSettings MongoDB { get; set; } = new MongoDBSettings();
        public TradingSettings Trading { get; set; } = new TradingSettings();
    }

    public class MongoDBSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = string.Empty;
    }

    public class TradingSettings
    {
        public CandleAnalysisSettings CandleAnalysis { get; set; }
        public decimal InitialBalance { get; set; }
        public decimal TradeAmount { get; set; }
        public decimal TakeProfitPercentage { get; set; }
        public decimal StopLossPercentage { get; set; }
    }

    public class CandleAnalysisSettings
    {
        public int LookbackMinutes { get; set; }
        public int CandleIntervalMinutes { get; set; }
        public int NumberOfCandles { get; set; }
        public GreenCandleThresholdSettings GreenCandleThreshold { get; set; }
        public int LastFiveCandlesThreshold { get; set; }
    }

    public class GreenCandleThresholdSettings
    {
        public int LastThreeCandles { get; set; }
        public int MinimumGreenCandles { get; set; }
    }
} 