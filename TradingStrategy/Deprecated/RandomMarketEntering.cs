﻿using System;
using TradingStrategy.Base;

namespace TradingStrategy.Strategy
{
    [DeprecatedStrategy]
    public sealed class RandomMarketEntering
        : GeneralMarketEnteringBase
    {
        private const int RandomRange = 10000;

        private Random _random = new Random();

        public override string Name
        {
            get { return "随机入市"; }
        }

        public override string Description
        {
            get { return "随机选择入市时机"; }
        }

        [Parameter(5000, "入市阈值。当随机整数（取值0~9999）小于阈值时入市")]
        public int EnterMarketThreshold { get; set; }

        public override MarketEnteringComponentResult CanEnter(ITradingObject tradingObject)
        {
            var result = new MarketEnteringComponentResult();

            int rand = _random.Next(RandomRange);
            if ( rand < EnterMarketThreshold)
            {
                result.Comments = string.Format("Random: {0}", rand);
                result.CanEnter = true;
            }

            return result;
        }
    }
}
