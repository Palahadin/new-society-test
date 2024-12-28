using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BOSTradingBot : Robot
    {
        // Fixed lot size instead of risk percentage
        private const double FIXED_LOT_SIZE = 0.01;

        [Parameter("Lookback Periods", DefaultValue = 10)]
        public int LookbackPeriods { get; set; }

        [Parameter("Structure Break Threshold (Pips)", DefaultValue = 2.0)]
        public double BreakThresholdPips { get; set; }

        private double pipSize;
        private double breakThreshold;

        protected override void OnStart()
        {
            pipSize = Symbol.PipSize;
            breakThreshold = BreakThresholdPips * pipSize;
        }

        protected override void OnTick()
        {
            // Only process on bar close
            if (MarketSeries.Close.Last(1) == 0)
                return;

            // Find recent structure points
            var highestHigh = double.MinValue;
            var lowestLow = double.MaxValue;
            
            for (int i = 1; i <= LookbackPeriods; i++)
            {
                highestHigh = Math.Max(highestHigh, MarketSeries.High.Last(i));
                lowestLow = Math.Min(lowestLow, MarketSeries.Low.Last(i));
            }

            // Check for Break of Structure
            double currentPrice = Symbol.Bid;
            
            bool bullishBOS = currentPrice > (highestHigh + breakThreshold) && 
                             IsPriceInUptrend();
            
            bool bearishBOS = currentPrice < (lowestLow - breakThreshold) && 
                             IsPriceInDowntrend();

            // Trading Logic
            if (bullishBOS && !HasOpenPosition())
            {
                ExecuteBuyOrder();
            }
            else if (bearishBOS && !HasOpenPosition())
            {
                ExecuteSellOrder();
            }
        }

        private bool IsPriceInUptrend()
        {
            // Simple trend detection using higher lows
            double previousLow = double.MaxValue;
            int higherLows = 0;

            for (int i = 1; i <= LookbackPeriods; i++)
            {
                if (MarketSeries.Low.Last(i) > previousLow)
                    higherLows++;
                previousLow = MarketSeries.Low.Last(i);
            }

            return higherLows >= LookbackPeriods / 2;
        }

        private bool IsPriceInDowntrend()
        {
            // Simple trend detection using lower highs
            double previousHigh = double.MinValue;
            int lowerHighs = 0;

            for (int i = 1; i <= LookbackPeriods; i++)
            {
                if (MarketSeries.High.Last(i) < previousHigh)
                    lowerHighs++;
                previousHigh = MarketSeries.High.Last(i);
            }

            return lowerHighs >= LookbackPeriods / 2;
        }

        private void ExecuteBuyOrder()
        {
            double stopLoss = CalculateStopLoss(TradeType.Buy);
            double takeProfit = CalculateTakeProfit(TradeType.Buy);

            ExecuteMarketOrder(TradeType.Buy, Symbol.Name, FIXED_LOT_SIZE, "BOS Buy", stopLoss, takeProfit);
        }

        private void ExecuteSellOrder()
        {
            double stopLoss = CalculateStopLoss(TradeType.Sell);
            double takeProfit = CalculateTakeProfit(TradeType.Sell);

            ExecuteMarketOrder(TradeType.Sell, Symbol.Name, FIXED_LOT_SIZE, "BOS Sell", stopLoss, takeProfit);
        }

        private double CalculateStopLoss(TradeType tradeType)
        {
            // Place stop loss beyond the structure
            if (tradeType == TradeType.Buy)
                return Math.Min(MarketSeries.Low.Last(1), MarketSeries.Low.Last(2)) - (10 * pipSize);
            else
                return Math.Max(MarketSeries.High.Last(1), MarketSeries.High.Last(2)) + (10 * pipSize);
        }

        private double CalculateTakeProfit(TradeType tradeType)
        {
            // Risk:Reward ratio of 1:2
            double currentPrice = tradeType == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            double stopLoss = CalculateStopLoss(tradeType);
            double risk = Math.Abs(currentPrice - stopLoss);

            if (tradeType == TradeType.Buy)
                return currentPrice + (2 * risk);
            else
                return currentPrice - (2 * risk);
        }

        private bool HasOpenPosition()
        {
            return Positions.FindAll("BOS Buy", Symbol.Name).Length > 0 || 
                   Positions.FindAll("BOS Sell", Symbol.Name).Length > 0;
        }
    }
}