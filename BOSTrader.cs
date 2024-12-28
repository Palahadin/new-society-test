using System;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BOSTradingBot : Robot
    {
        [Parameter("Risk Percentage", DefaultValue = 1.0)]
        public double RiskPercentage { get; set; }

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
            if (!Bars.LastBarIndex.Equals(Bars.Count - 1))
                return;

            // Find recent structure points
            var highestHigh = double.MinValue;
            var lowestLow = double.MaxValue;
            
            for (int i = 1; i <= LookbackPeriods; i++)
            {
                highestHigh = Math.Max(highestHigh, Bars.High[i]);
                lowestLow = Math.Min(lowestLow, Bars.Low[i]);
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
                if (Bars.Low[i] > previousLow)
                    higherLows++;
                previousLow = Bars.Low[i];
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
                if (Bars.High[i] < previousHigh)
                    lowerHighs++;
                previousHigh = Bars.High[i];
            }

            return lowerHighs >= LookbackPeriods / 2;
        }

        private void ExecuteBuyOrder()
        {
            double stopLoss = CalculateStopLoss(TradeType.Buy);
            double takeProfit = CalculateTakeProfit(TradeType.Buy);
            double volume = CalculatePositionSize(stopLoss, TradeType.Buy);

            ExecuteMarketOrder(TradeType.Buy, Symbol.Name, volume, "BOS Buy", stopLoss, takeProfit);
        }

        private void ExecuteSellOrder()
        {
            double stopLoss = CalculateStopLoss(TradeType.Sell);
            double takeProfit = CalculateTakeProfit(TradeType.Sell);
            double volume = CalculatePositionSize(stopLoss, TradeType.Sell);

            ExecuteMarketOrder(TradeType.Sell, Symbol.Name, volume, "BOS Sell", stopLoss, takeProfit);
        }

        private double CalculateStopLoss(TradeType tradeType)
        {
            // Place stop loss beyond the structure
            if (tradeType == TradeType.Buy)
                return Math.Min(Bars.Low[1], Bars.Low[2]) - (10 * pipSize);
            else
                return Math.Max(Bars.High[1], Bars.High[2]) + (10 * pipSize);
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

        private double CalculatePositionSize(double stopLoss, TradeType tradeType)
        {
            double currentPrice = tradeType == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            double riskAmount = Account.Balance * (RiskPercentage / 100);
            double pipValue = Symbol.PipValue;
            double pips = Math.Abs(currentPrice - stopLoss) / pipSize;

            return Math.Round(riskAmount / (pips * pipValue), 2);
        }

        private bool HasOpenPosition()
        {
            return Positions.FindAll("BOS Buy", Symbol.Name).Length > 0 || 
                   Positions.FindAll("BOS Sell", Symbol.Name).Length > 0;
        }
    }
}