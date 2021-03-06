﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StockAnalysis.Share;

using TradingStrategy;

namespace TradingStrategy.Strategy
{
    public sealed class CompositionStrategy : ITradingStrategy
    {
        private ITradingStrategyComponent[] _components = null;
        private IPositionSizingComponent _positionSizing = null;
        private List<IMarketEnteringComponent> _marketEntering = new List<IMarketEnteringComponent>();
        private List<IMarketExitingComponent> _marketExiting = new List<IMarketExitingComponent>();
        private IStopLossComponent _stopLoss = null;

        private string _name;
        private string _description;

        private IEvaluationContext _context;
        private List<Instruction> _instructionsInCurrentPeriod;
        private Dictionary<long, Instruction> _activeInstructions = new Dictionary<long, Instruction>();
        private DateTime _period;
        private Dictionary<ITradingObject, Bar> _barsInPeriod;
        private Dictionary<string, ITradingObject> _codeToTradingObjectMap;

        public string Name
        {
            get { return _name; }
        }

        public string Description
        {
            get { return _description;  }
        }

        public IEnumerable<ParameterAttribute> GetParameterDefinitions()
        {
            foreach (var component in _components)
            {
                foreach (var attribute in component.GetParameterDefinitions())
                {
                    yield return attribute;
                }
            }
        }

        public void Initialize(IEvaluationContext context, IDictionary<ParameterAttribute, object> parameterValues)
        {
            foreach (var component in _components)
            {
                component.Initialize(context, parameterValues);
            }

            _context = context;
        }

        public void WarmUp(ITradingObject tradingObject, StockAnalysis.Share.Bar bar)
        {
            foreach (var component in _components)
            {
                component.WarmUp(tradingObject, bar);
            }
        }

        public void StartPeriod(DateTime time)
        {
            foreach (var component in _components)
            {
                component.StartPeriod(time);
            }

            _instructionsInCurrentPeriod = new List<Instruction>();
            _barsInPeriod = new Dictionary<ITradingObject, Bar>();
            _codeToTradingObjectMap = new Dictionary<string, ITradingObject>();
            _period = time;
        }

        public void Evaluate(ITradingObject tradingObject, Bar bar)
        {
            if (bar.Invalid())
            {
                return;
            }

            // remember the trading object and bar because the object could be used in AfterEvaulation
            _barsInPeriod.Add(tradingObject, bar);
            _codeToTradingObjectMap.Add(tradingObject.Code, tradingObject);

            // evaluate all components
            foreach (var component in _components)
            {
                component.Evaluate(tradingObject, bar);
            }

            string comments = string.Empty;
            var positions = _context.ExistsPosition(tradingObject.Code)
                ? _context.GetPositionDetails(tradingObject.Code)
                : (IEnumerable<Position>)new List<Position>();

            // decide if we need to exit market for this trading object. This is the first priorty work
            if (positions.Count() > 0)
            {
                foreach (var component in _marketExiting)
                {
                    if (component.ShouldExit(tradingObject, out comments))
                    {
                        _instructionsInCurrentPeriod.Add(
                            new Instruction()
                            {
                                Action = TradingAction.CloseLong,
                                Comments = "market exiting condition triggered. " + comments,
                                SubmissionTime = _period,
                                TradingObject = tradingObject,
                                SellingType = SellingType.ByVolume,
                                Volume = positions.Sum(p => p.Volume),
                            });

                        return;
                    }
                }
            }

            // decide if we need to stop loss for some positions
            int totalVolume = 0;
            foreach (var position in positions)
            {
                if (position.StopLossPrice > bar.ClosePrice)
                {
                    totalVolume += position.Volume;
                }
            }

            if (totalVolume > 0)
            {
                _instructionsInCurrentPeriod.Add(
                    new Instruction()
                    {
                        Action = TradingAction.CloseLong,
                        Comments = string.Format("stop loss @{0:0.000}", bar.ClosePrice),
                        SubmissionTime = _period,
                        TradingObject = tradingObject,
                        SellingType = SellingType.ByStopLossPrice,
                        StopLossPriceForSell = bar.ClosePrice,
                        Volume = totalVolume
                    });

                return;
            }

            // decide if we should enter market
            if (positions.Count() == 0) 
            {
                List<string> allComments = new List<string>(_marketEntering.Count + 1);
                allComments.Add("Entering market. ");

                bool canEnter = true;
                foreach (var component in _marketEntering)
                {
                    string subComments;

                    if (!component.CanEnter(tradingObject, out subComments))
                    {
                        canEnter = false;
                        break;
                    }

                    allComments.Add(subComments); 
                }

                if (canEnter)
                {
                    CreateIntructionForBuying(tradingObject, bar.ClosePrice, string.Join(";", allComments));
                }
            }
        }

        public void AfterEvaluation()
        {
            // decide if existing position should be adjusted
            string[] codesForAddingPosition;
            PositionIdentifier[] positionsForRemoving;

            if (_positionSizing.ShouldAdjustPosition(out codesForAddingPosition, out positionsForRemoving))
            {
                if (positionsForRemoving != null
                    && positionsForRemoving.Length > 0)
                {
                    // remove positions
                    foreach (var identifier in positionsForRemoving)
                    {
                        if (!_context.ExistsPosition(identifier.Code))
                        {
                            throw new InvalidOperationException("There is no position for code " + identifier.Code);
                        }

                        ITradingObject tradingObject;

                        if (!_codeToTradingObjectMap.TryGetValue(identifier.Code, out tradingObject))
                        {
                            // ignore the request of removing position because the trading object has 
                            // no valid bar this period.
                            continue;
                        }

                        var positions = _context.GetPositionDetails(identifier.Code)
                            .Where(p => p.ID == identifier.PositionId);

                        if (positions == null || positions.Count() == 0)
                        {
                            throw new InvalidOperationException(
                                string.Format("position id {0} does not exist.", identifier.PositionId));
                        }

                        System.Diagnostics.Debug.Assert(positions.Count() == 1);

                        _instructionsInCurrentPeriod.Add(
                            new Instruction()
                            {
                                Action = TradingAction.CloseLong,
                                Comments = "adjust position triggered. ",
                                SubmissionTime = _period,
                                TradingObject = tradingObject,
                                SellingType = SellingType.ByPositionId,
                                PositionIdForSell = identifier.PositionId,
                                Volume = positions.Sum(p => p.Volume),
                            });
                    }
                }
                else if (codesForAddingPosition != null
                    && codesForAddingPosition.Length > 0)
                {
                    // adding positions
                    foreach (var code in codesForAddingPosition)
                    {
                        if (!_context.ExistsPosition(code))
                        {
                            throw new InvalidOperationException("There is no position for code " + code);
                        }

                        ITradingObject tradingObject;

                        if (!_codeToTradingObjectMap.TryGetValue(code, out tradingObject))
                        {
                            // ignore the request of adding position because the trading object has 
                            // no valid bar this period.
                            continue;
                        }

                        Bar bar = _barsInPeriod[tradingObject];

                        CreateIntructionForBuying(tradingObject, bar.ClosePrice, "Adding position. ");
                    }
                }

            }
        }

        private void CreateIntructionForBuying(ITradingObject tradingObject, double price, string comments)
        {
            double stopLossGap = _stopLoss.EstimateStopLossGap(tradingObject, price);
            if (stopLossGap >= 0.0)
            {
                throw new InvalidProgramException("the stop loss gap returned by the stop loss component is greater than zero");
            }

            int volume = _positionSizing.EstimatePositionSize(tradingObject, price, stopLossGap);

            // adjust volume to ensure it fit the trading object's contraint
            volume -= volume % tradingObject.VolumePerBuyingUnit;

            if (volume > 0)
            {
                _instructionsInCurrentPeriod.Add(
                    new Instruction()
                    {
                        Action = TradingAction.OpenLong,
                        Comments = comments,
                        SubmissionTime = _period,
                        TradingObject = tradingObject,
                        Volume = volume
                    });
            }
        }

        public void NotifyTransactionStatus(Transaction transaction)
        {
            Instruction instruction;
            if (!_activeInstructions.TryGetValue(transaction.InstructionId, out instruction))
            {
                throw new InvalidOperationException(
                    string.Format("can't find instruction {0} associated with the transaction.", transaction.InstructionId));
            }

            if (transaction.Succeeded && transaction.Action == TradingAction.OpenLong)
            {
                // update the stop loss and risk for new positions
                string code = transaction.Code;
                if (!_context.ExistsPosition(code))
                {
                    throw new InvalidOperationException(
                        string.Format("There is no position for {0} when calling this function", code));
                }

                var positions = _context.GetPositionDetails(code);

                // set stop loss and initial risk for all new postions
                foreach (var position in positions)
                {
                    if (!position.IsStopLossPriceInitialized())
                    {
                        double stopLossGap = _stopLoss.EstimateStopLossGap(instruction.TradingObject, position.BuyPrice);

                        double stopLossPrice = Math.Max(0.0, position.BuyPrice + stopLossGap);

                        position.SetStopLossPrice(stopLossPrice);
                    }
                }
            }
            else
            {
                // do nothing now
            }

            // remove the instruction from active instruction collection.
            _activeInstructions.Remove(instruction.ID);
        }

        public IEnumerable<Instruction> RetrieveInstructions()
        {
            if (_instructionsInCurrentPeriod != null)
            {
                var temp = _instructionsInCurrentPeriod;

                foreach (var instruction in _instructionsInCurrentPeriod)
                {
                    _activeInstructions.Add(instruction.ID, instruction);
                }

                _instructionsInCurrentPeriod = null;

                return temp;
            }
            else
            {
                return null;
            }
        }

        public void EndPeriod()
        {
            foreach (var component in _components)
            {
                component.EndPeriod();
            }

            _instructionsInCurrentPeriod = null;
            _barsInPeriod = null;
        }

        public void Finish()
        {
            foreach (var component in _components)
            {
                component.Finish();
            }

            if (_activeInstructions.Count > 0)
            {
                foreach (var id in _activeInstructions.Keys)
                {
                    _context.Log(string.Format("unexecuted instruction {0}.", id));
                }
            }
        }

        public CompositionStrategy(IEnumerable<ITradingStrategyComponent> components)
        {
            if (components == null || components.Count() == 0)
            {
                throw new ArgumentNullException();
            }

            _components = components.ToArray();

            foreach (var component in components)
            {
                if (component is IPositionSizingComponent)
                {
                    SetComponent(component, ref _positionSizing);
                }
                
                if (component is IMarketEnteringComponent)
                {
                    _marketEntering.Add((IMarketEnteringComponent)component);
                }

                if (component is IMarketExitingComponent)
                {
                    _marketExiting.Add((IMarketExitingComponent)component);
                }
                
                if (component is IStopLossComponent)
                {
                    SetComponent(component, ref _stopLoss);
                }
            }

            if (_positionSizing == null
                || _marketExiting.Count == 0
                || _marketEntering.Count == 0
                || _stopLoss == null)
            {
                throw new ArgumentException("Missing at least one type of component");
            }

            _name = "复合策略，包含以下组件：\n";
            _name += string.Join(Environment.NewLine, _components.Select(c => c.Name));

            _description = "复合策略，包含以下组件描述：\n";
            _description += string.Join(Environment.NewLine, _components.Select(c => c.Description));
        }

        private static void SetComponent<T>(ITradingStrategyComponent component, ref T obj)
        {
            if (component == null)
            {
                throw new ArgumentNullException();
            }

            if (component is T)
            {
                if (obj == null)
                {
                    obj = (T)component;
                }
                else
                {
                    throw new ArgumentException(string.Format("Duplicated {0} objects", typeof(T)));
                }
            }
            else
            {
                throw new ArgumentException(string.Format("unmatched component type {0}", typeof(T)));
            }
        }
    }
}
