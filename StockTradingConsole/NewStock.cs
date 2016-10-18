﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using StockAnalysis.Share;
namespace StockTradingConsole
{
    public sealed class NewStock
    {
        public DateTime DateToBuy { get; set; }
        public StockName Name { get; set; }
        public float BuyPriceUpLimitInclusive { get; set; }
        public float BuyPriceDownLimitInclusive { get; set; }
        public float TotalCapitalUsedToBuy { get; set; }
        public float ActualOpenPrice { get; set; }

        public NewStock()
        {
        }

        public NewStock(NewStockForSerialization nss)
        {
            DateToBuy = nss.DateToBuy;
            Name = new StockName(nss.SecurityCode, nss.SecurityName);
            BuyPriceUpLimitInclusive = nss.BuyPriceUpLimitInclusive;
            BuyPriceDownLimitInclusive = nss.BuyPriceDownLimitInclusive;
            TotalCapitalUsedToBuy = nss.TotalCapitalUsedToBuy;
            ActualOpenPrice = 0.0f;
        }
    }
}
