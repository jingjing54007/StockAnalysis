﻿using System;
using System.Collections.Generic;
using TradingStrategy.Base;

namespace TradingStrategy.Strategy
{
    public sealed class EqualValuePositionSizing : GeneralPositionSizingBase
    {
        [Parameter(10, "权益被分割的块数，每份头寸将占有一份. 0表示自适应划分")]
        public int PartsOfEquity { get; set; }

        [Parameter(100, "自适应划分中所允许的最大划分数目")]
        public int MaxPartsOfAdpativeAllocation { get; set; }

        [Parameter(1, "自适应划分中所允许的最小划分数目")]
        public int MinPartsOfAdpativeAllocation { get; set; }

        [Parameter(1000, "最大待处理头寸数目，当待处理头寸数目超过此数时不买入任何头寸")]
        public int MaxObjectNumberToBeEstimated { get; set; }

        [Parameter(EquityEvaluationMethod.InitialEquity, "权益计算方法。0：核心权益法，1：总权益法，2：抵减总权益法，3：初始权益法，4：控制损失初始权益法，5：控制损失总权益法，6：控制损失抵减总权益法")]
        public EquityEvaluationMethod EquityEvaluationMethod { get; set; }

        [Parameter(1.0, "权益利用率，[0.0..1.0], 0.0代表自适应权益利用率")]
        public double EquityUtilization { get; set; }

        private double _dynamicEquityUtilizationForEachObject = 0.372;

        public override string Name
        {
            get { return "价格等值模型"; }
        }

        public override string Description
        {
            get { return "每份头寸占有的价值是总权益的固定比例(1/PartsOfEquity)"; }
        }

        protected override void ValidateParameterValues()
        {
            base.ValidateParameterValues();

            if (PartsOfEquity < 0)
            {
                throw new ArgumentOutOfRangeException("PartsOfEquity must be greater than or equal to 0");
            }

            if (EquityUtilization < 0.0 || EquityUtilization > 1.0)
            {
                throw new ArgumentException("EquityUtilization must be in [0.0..1.0]");
            }
        }

        private double GetDynamicEquityUtilization(int totalNumberOfObjectsToBeEstimated)
        {
            return 1.0 - Math.Pow(1.0 - _dynamicEquityUtilizationForEachObject, totalNumberOfObjectsToBeEstimated);
        }

        public override int EstimatePositionSize(ITradingObject tradingObject, double price, double stopLossGap, out string comments, int totalNumberOfObjectsToBeEstimated)
        {
            if (totalNumberOfObjectsToBeEstimated > MaxObjectNumberToBeEstimated)
            {
                comments = string.Empty;
                return 0;
            }

            var currentEquity = Context.GetCurrentEquity(CurrentPeriod, EquityEvaluationMethod);

            int parts = PartsOfEquity == 0
                ? Math.Max(Math.Min(totalNumberOfObjectsToBeEstimated, MaxPartsOfAdpativeAllocation), MinPartsOfAdpativeAllocation)
                : PartsOfEquity;

            double equityUtilization = Math.Abs(EquityUtilization) < 1e-6
                ? GetDynamicEquityUtilization(totalNumberOfObjectsToBeEstimated)
                : EquityUtilization;

            comments = string.Format(
                "positionsize = currentEquity({0:0.000}) * equityUtilization({1:0.000}) / Parts ({2}) / price({3:0.000})",
                currentEquity,
                equityUtilization,
                parts,
                price);

            return (int)(currentEquity * equityUtilization / parts / price);
        }
    }
}
