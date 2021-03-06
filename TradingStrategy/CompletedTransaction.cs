﻿using System;
using System.Collections.Generic;

namespace TradingStrategy
{
    public sealed class CompletedTransaction
    {
        public string Code { get; set; } // code of trading object
        public string Name { get; set; } // name of trading object
        public DateTime ExecutionTime { get; set; } // transaction execution time
        public double AverageBuyPrice { get; set; } // the average buy price (there might be several buys before sold)
        public double SoldPrice { get; set; } // sold price
        public long Volume { get; set; } // volume
        public double Commission { get; set; } // buy commission + sold commission
        public double BuyCost { get; set; } // AverageBuyPrice * Volume
        public double SoldGain { get; set; } // SoldPrice * Volume
    }
}
