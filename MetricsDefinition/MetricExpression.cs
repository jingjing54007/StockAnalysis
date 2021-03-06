﻿using System;
using StockAnalysis.Share;

namespace MetricsDefinition
{
    public abstract class MetricExpression
    {
        public abstract string[] FieldNames { get; }

        public abstract double[] Values { get; }

        public double Value { get { return Values[0]; } }

        public virtual void SingleOutputUpdate(Bar data)
        {
            throw new NotImplementedException();
        }

        public virtual void MultipleOutputUpdate(Bar data)
        {
            throw new NotImplementedException();
        }

        public virtual void SingleOutputUpdate(double data)
        {
            throw new NotImplementedException();
        }

        public virtual void MultipleOutputUpdate(double data)
        {
            throw new NotImplementedException();
        }
    }
}
