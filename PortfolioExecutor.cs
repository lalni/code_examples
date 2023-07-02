
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Forms;
using System.Xml.Linq;
using System.Xml.Serialization;
using MathNet.Numerics;
using Deltix.StrategyServer.Api.Channels.Attributes;
using Deltix.Time.Utils;
using Deltix.Timebase.Api;
using Deltix.Timebase.Api.Messages.Universal;
using Deltix.Timebase.Api.Schema;
using Deltix.TradingCalendar;
using IdxEditor.Rendering.Attributes;
using QuantOffice.Calendar;
using QuantOffice.CodeGenerator;
using QuantOffice.Commons.Data;
using QuantOffice.Commons.Execution;
using QuantOffice.Commons.Execution.Utils;
using QuantOffice.Commons.HistoryService.Data;
using QuantOffice.Connection;
using QuantOffice.CustomMessages;
using QuantOffice.Data;
using QuantOffice.StrategyRunner;
using QuantOffice.SyntheticInstruments;
using QuantOffice.Execution;
using QuantOffice.Execution.Utils;
using QuantOffice.MarketDataProvider;
using QuantOffice.MarketDataProvider.DataCache;
using QuantOffice.Options;
using QuantOffice.Reporting.Custom;
using QuantOffice.SyntheticInstruments.MarketRunner;
using QuantOffice.SyntheticInstruments.MarketRunner.CustomMessages;
using QuantOffice.Utils;
using QuantOffice.Utils.Collections;
using QuantOffice.Utils.ObjectInspector;
using RTMath.Containers;
using UhfConnector.ServerSideConnector;
using Basket = QuantOffice.SyntheticInstruments.CustomView.Basket;
using Bar = QuantOffice.Data.IBarInfo;
using BestBidOfferMessage = QuantOffice.Data.BestBidOfferMessage;
using DacMessage = QuantOffice.Data.DacMessage;
using Exchange = QuantOffice.Execution.Exchange;
using ExecutorBase = QuantOffice.Execution.InstrumentExecutorBase;
using IBestBidOfferMessageInfo = QuantOffice.Data.IBestBidOfferMessageInfo;
using IDacMessageInfo = QuantOffice.Data.IDacMessageInfo;
using ILevel2MessageInfo = QuantOffice.Data.ILevel2MessageInfo;
using IL2SnapshotMessageInfo = QuantOffice.Data.IL2SnapshotMessageInfo;
using IL2MessageInfo = QuantOffice.Data.IL2MessageInfo;
using IMarketMessageInfo = QuantOffice.Data.IMarketMessageInfo;
using IPackageHeaderInfo = QuantOffice.Data.IPackageHeaderInfo;
using IPacketEndMessageInfo = QuantOffice.Data.IPacketEndMessageInfo;
using ITradeMessageInfo = QuantOffice.Data.ITradeMessageInfo;
using L2Message = QuantOffice.Data.L2Message;
using L2SnapshotMessage = QuantOffice.Data.L2SnapshotMessage;
using Level2Message = QuantOffice.Data.Level2Message;
using MarketMessage = QuantOffice.Data.MarketMessage;
using PackageHeader = QuantOffice.Data.PackageHeader;
using PacketEndMessage = QuantOffice.Data.PacketEndMessage;
using Path = System.IO.Path;
using TradeMessage = QuantOffice.Data.TradeMessage;
using TimeInterval = QuantOffice.Commons.Execution.Utils.TimeInterval;
using Rebalancer = QuantOffice.Rebalancer;
using QuantOffice.Reporting.Model;
using QuantOffice.HistoryService.Data;
using Deltix.EMS.Coordinator;
using Deltix.EMS.API;
using Deltix.EMS.Simulator;
using Deltix.EMS.AlgoOrders;
using Deltix.EMS.AlgoOrders.Extended;
using Deltix.EMS.AlgoOrders.Slice;
using Deltix.EMS.SpecialOrders;
using QuantOffice.Reporting.API;
using TradeType = QuantOffice.Reporting.API.TradeType;
using Deltix.Timebase.Api.Messages;
using Transformations;
using Reporter = RiskReporter.Reporter;

/// <summary>
/// This class contains portfolio level event processing logic.
/// Much of the logic has been moved into the Rebalancer class but
/// this class still has 2 major responsibilities.
/// 1. Calculate the desired portfolio positions given each instruments Alpha
/// 2. Call the rebalancer functionality during the proper events.
/// </summary>
public partial class PortfolioExecutor : PortfolioExecutorBase
{
    #region LocalVariables

    public OrderProcessor orderExecutor;
    public Dictionary<string, double> instrWeights = new Dictionary<string, double>();
    public Rebalancer rebalancer;
    [NonRendered]
    public ReportEngine reportEngine;
    public double RealizedPnL;
    public double UnrealizedPnL;
    public double CumulativePnL;
    public double CapitalAvailable;
    public double RefilledCapital;
    public int RefillTime;
    public StrategyTimer timer = null;
    public Reporter reporter;

    public Queue<LimitOrder> buyLimitOrders;


    public List<InstrumentExecutor> Universe
    {
        get
        {
            List<InstrumentExecutor> result = new List<InstrumentExecutor>();
            foreach (InstrumentExecutor instr in Slice)
            {
                if (instr.active) result.Add(instr); //marketcap initialized with zero
            }
            return result;
        }
    }

    public Dictionary<String, InstrumentExecutor> InstrumentExecutorDictionary = new Dictionary<String, InstrumentExecutor>();

    #endregion

    #region BuildingBlocks


    public static EMSParameters CreateEMSParameters()
    {
        EMSParameters emsParameters = new EMSParameters();
        OrderSimulatorParameters simulatorParams = new OrderSimulatorParameters();
        simulatorParams.simulationMode = SimulationMode.IntradayBar;
        simulatorParams.slip_cur = 0;
        simulatorParams.slip_stock = 0;
        simulatorParams.slip_fut = 0;
        emsParameters.OrderExecutor.Parameters = simulatorParams;
        return emsParameters;
    }
    #endregion



    #region Events


    public override void OnInit()
    {
        reporter = new Reporter(this);
        reporter.OnInit();

        buyLimitOrders = new Queue<LimitOrder>();

        foreach (InstrumentExecutor instr in Slice)
        {
            /* 
             * this object is used primarily when importing data from a custom subcription
             * as a quick way to locate each instr object from a symbol string
             */
            InstrumentExecutorDictionary.Add(instr.Symbol, instr);
        }

        orderExecutor = new OrderProcessor(this);
        orderExecutor.SetParameters(emsParameters);
        orderExecutor.AutoClearInactiveOrders = true;

        if (generateReports)
        {
            reportEngine = new ReportEngine(this, orderExecutor, orderExecutor);
            reportEngine.TradeReport.Enable();
            reportEngine.OrderReport.Enable();
            reportEngine.ExecutionReport.Enable();
            reportEngine.PerformanceReport.Enable();
            reportEngine.ByInstrumentReport.Enable();
            reportEngine.ByPeriodReport.Enable();
            reportEngine.DrawdownReport.Enable();
            reportEngine.AllocationReport.Enable();
            reportEngine.Start();
        }


        this.rebalancer = new Rebalancer(this);
        this.CapitalAvailable = InitialCap;


        base.OnInit();


    }

    private void SaveToCsv(IList<Object[]> reportData, CustomReportColumnDef[] reportColumn, string path)
    {
        var lines = new List<string>();
        var header = string.Join(",", reportColumn.Select(x => x.ColumnTitle).ToList());
        lines.Add(header);
        foreach (Object[] row in reportData)
        {
            var valueLine = string.Join(",", row);
            lines.Add(valueLine);
        }
        File.WriteAllLines(path, lines.ToArray());
    }
    public override void OnExit(ExitState exitState)
    {

        base.OnExit(exitState);
        if (generateReports)
        {
            reportEngine.InitialCapital = InitialCap;
            reportEngine.TotalReinvestMode = false; //always false, to get reasonable value
                                                    //if we want to accurate byperiod method, we have to change initial capital parameter continuously by referring to event log.
            reportEngine.RiskFreeRate = InterestOnCash;
            reportEngine.Finish();
            reportEngine.TradeReport.Show();
            reportEngine.OrderReport.Show();
            reportEngine.ExecutionReport.Show();
            reportEngine.PerformanceReport.Show();
            PerformanceReport performance = reportEngine.PerformanceReport;
            reportEngine.PerformanceReport.Consolidate();
            reportEngine.ByInstrumentReport.Show();
            reportEngine.ByInstrumentReport.Consolidate();
            reportEngine.ByPeriodReport.Show();
            reportEngine.DrawdownReport.Show();
            reportEngine.AllocationReport.Show();

            //Add custom items to report to be generated at end of backtest
            short USDTcode = 1321;
            double monthlyAverage = reportEngine.ByPeriodReport.GetStatistic(TradeType.All, USDTcode, ByPeriodReport.Periodicity.Monthly).AggregateStatistics.AverageReturnPercentage;
            double monthlyStd = reportEngine.ByPeriodReport.GetStatistic(TradeType.All, USDTcode, ByPeriodReport.Periodicity.Monthly).AggregateStatistics.StandardDeviationReturnPercentage;
            CustomPerformanceReport.Log("CustomPerformance", "Monthly Sharpe", Math.Sqrt(12) * monthlyAverage / monthlyStd);

            if (ExecutionMode == ExecutionMode.BackTesting)
            {
                string path = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName, "Reports");
                string performance_path = Path.Combine(path, "PerformanceReport.csv");
                reportEngine.PerformanceReport.ConsolidateToDelimitedFile(performance_path);
                reportEngine.ByPeriodReport.SaveToDelimitedFile(Path.Combine(path, "PeriodReport.csv"));
                reportEngine.TradeReport.SaveToDelimitedFile(Path.Combine(path, "TradeReport.csv"));
                reportEngine.ByInstrumentReport.SaveToDelimitedFile(Path.Combine(path, "InstrumentReport.csv"));
            }
        }

    }


    public override void OnWarmupEnd()
    {

        Log("Warmup End! " + CurrentTime.ToString());

    }

    /// <summary>
    /// Occurs after market data is aggregated into a OHLCV bar, apply stop loss and adjust positions
    /// </summary>
    /// <param name="context"></param>
    public override void OnAfterBarClose(Context context) //for restricting maximum loss in short trade
    {
        base.OnAfterBarClose(context);
        if (this.CurrentTime.Hour != 0 && this.CurrentTime.Minute != 0 && this.CurrentTime.Second != 0)
        {
            Log("Exit the strategy but do not send orders");
            return;

        }
        try
        {
            if (ExecutionMode != ExecutionMode.Warmup && Universe.Count > 0) //Ex//if only daily data is available, active is false but data is carried from last value.
            {
                AdjustPositions();
            }
        }
        catch (Exception e)
        {
            if (ExecutionMode != ExecutionMode.RealTime)
            {
                return;// throw (e);
            }
            else
            {
                Log("PortfolioExecutor.OnAfterBarClose " + e.ToString(), EventSeverity.Error);
            }
        }

    }

    /// <summary>
    /// If strategy is using dailyBar then this will also call the OnAfterBarClose event
    /// </summary>
    ///// <param name="context"></param>
    //public override void OnDayClose(Context context)
    //{
    //    base.OnDayClose(context);

    //    // Log("hit day close");

    //    if (this.CurrentTime.Hour != 0 && this.CurrentTime.Minute != 0 && this.CurrentTime.Second != 0)
    //    {
    //        //execution has been cancelled do not call rest of DayClose logic
    //        Log("Exit the strategy but do not send orders");
    //        return;
    //    }

    //    try
    //    {

    //        // Adjust position should not be called during warmup or when the universe is empty
    //        if (ExecutionMode != ExecutionMode.Warmup && Universe.Count > 0) //ExecutionMode != ExecutionMode.Warmup &&
    //        {

    //            AdjustPositions();
    //        }

    //    }
    //    catch (Exception e)
    //    {
    //        Log("PortfolioExecutor.OnAfterDayClose " + e.ToString(), EventSeverity.Error);

    //        if (ExecutionMode != ExecutionMode.RealTime) { return; };//throw (e); };
    //    }
    //}


    // <summary>
    // This function will have to be written over for each strategy. The general flow of logic is.
    // 1. Define the universe to trade over
    // 2. Get the trading signals from each instrument (instr.alpha)
    // 3. Using the trading signals assign portfolio weights (this includes market neutralization)
    //
    // </summary>
    public Dictionary<string, double> getFactors(List<InstrumentExecutor> Universe)
    {
        Dictionary<string, double> instrFactor = new Dictionary<string, double>();
        if (Universe.Count == 0) return new Dictionary<string, double> { };

        foreach (InstrumentExecutor instr in Universe)
        {
            instrFactor[instr.Symbol] = (double)instr.alpha;
        }
        return instrFactor;

    }

    /// <summary>
    /// Calculate weights for portfolio, this should be changed for each strategy
    /// </summary>
    /// <param name="instrFactor">Taken from instr.alpha of each instrument</param>
    /// <returns></returns>
    public static PortfolioWeights RecalculateWeights(Dictionary<string, double> instrFactor)
    {
        List<string> symbols = instrFactor.Keys.ToList();

        double[] values = instrFactor.Values.ToArray();//Transformations.transform.ZscoreRank(instrFactor).ToArray(); //This list will be used for argument in neutralization
        //double[] valuess = Transformations.transform.Winsorize(values, 3, -3).ToArray();

        // If you want to use Rebalance function, follow below steps
        Dictionary<string, double> instrDesired = new Dictionary<string, double>();
        for (int i = 0; i < symbols.Count; i++)
        {
            //if (values[i] < 0)
            //{
            //    values[i] = 0;
            //}
            instrDesired.Add(symbols[i], values[i]);
        }
        Dictionary<string, double> instrWeights = Rebalancer.Rebalance(instrDesired, marketNeutral: true);

        return new PortfolioWeights(instrWeights);
    }

    /// <summary>
    /// Caclulate portfolio weights and send orders.
    /// </summary>
    public void AdjustPositions()
    {
        Dictionary<string, double> factors = getFactors(Universe);
        PortfolioWeights portfolioWeights = RecalculateWeights(factors);
        //Log(String.Format("portfolioWeights.Values : {0}, portfolioWeights.Values : {1} portfolioWeights.Values : {2}, portfolioWeights.Values : {3}, portfolioWeights.Values : {4}, portfolioWeights.Values : {5}",
        //   portfolioWeights.weights.Values.ToArray()[0], portfolioWeights.weights.Values.ToArray()[1],
        //   portfolioWeights.weights.Values.ToArray()[2], portfolioWeights.weights.Values.ToArray()[3],
        //   portfolioWeights.weights.Values.ToArray()[4], portfolioWeights.weights.Values.ToArray()[5]));

        SendOrders(portfolioWeights);
    }

    /// <summary>
    /// Send buy and sell orders so that portfolio capital is allocated proportionaly to each portfolio weight.
    /// </summary>
    public void SendOrders(PortfolioWeights portfolioWeights)
    {
        if (portfolioWeights.empty) { return; }

        //rebalancer.ReInvestCapital(this, true, 0.5);
        Log(String.Format("Current Time : {0}, PortfolioCapital : {1} RealizedPnL : {2}, UnrealizedPnL : {3}, Cumulative PnL : {4}, RefilledCapital : {5}, Refilled {6}",
            CurrentTime, this.rebalancer.portfolioCapital, this.RealizedPnL, this.UnrealizedPnL, this.CumulativePnL, this.RefilledCapital, this.RefillTime));
        CapitalReport.Log("CapitalReport", CurrentTime.ToString(), this.rebalancer.portfolioCapital, this.RealizedPnL, this.UnrealizedPnL, this.CumulativePnL, this.RefilledCapital, this.RefillTime);
        if (ExecutionMode == ExecutionMode.RealTime)
        {

            foreach (KeyValuePair<string, double> keyValuePair in portfolioWeights.weights)
            {
                StrategyPositions message = new StrategyPositions();
                message.Instrument = InstrumentExecutorDictionary[keyValuePair.Key].Symbol;
                message.Weight = keyValuePair.Value;
                message.Strategy = (MutableString)this.StrategyName;
                message.Urgency = (MutableString)"High";
                StrategyPositions.Send(message);
            }
        }
        rebalancer.SendOrders(portfolioWeights.weights);



        timer = Timers.CreateTimer(new TimeSpan(0, 10, 0), SendBuyOrders, "This is not used");
        //Log("Timer strated");
        timer.Start();
    }

    public void SendBuyOrders(object o)
    {
        Log("SendBuyOrders fired");
        //  this.rebalancer.OnTimer(o);

    }


    public override string ToString()
    {
        Dictionary<string, double> currentPositions = rebalancer.getCurrentState();
        return String.Format("Strategy name: {0}, cumulative PnL:{1}", this.StrategyName, this.CumulativePnL);
    }




    //override Parent Log method, extra context and slack message for errors.

    public void TransitionToSendOrders(double currentPrice)
    {
        this.rebalancer.SendBuyOrdersAfterSell(currentPrice);
    }


    public new int Log(string text, EventSeverity eventSeverity)
    {
        if (eventSeverity == EventSeverity.Error)
        {
            //Send error with additional context
            int row = base.Log(text, eventSeverity);
            base.Log(this.ToString(), eventSeverity, row);

            //send message to slack
            reporter.Error(text);
            return row;
        }
        else
        {
            return base.Log(text, eventSeverity);
        }
    }


    #endregion

    #region InputParameters
    [DisplayInfo(DisplayName = "Strategy Name:")]
    public string StrategyName = "Size_Tradable";

    [DisplayInfo(DisplayName = "Initial Capital (US$):")]
    public double InitialCap = 10;

    [DisplayInfo(DisplayName = "Bet Size :")] //not used now
    public int BetSize;

    [DisplayInfo(DisplayName = "Interest On Cash (%):")]
    public double InterestOnCash = 0;

    [DisplayInfo(DisplayName = "Stop Loss pct (%):")]
    public double StopLossPct = 100;

    [DisplayInfo(DisplayName = "Take Profit Pct (%):")]
    public double TakeProfitPct = 10000;

    [DisplayInfo(DisplayName = "Exchange:")]
    public string exchange = "OKEX";

    [DisplayInfoAttribute(DisplayName = "Simulation Mode")]
    public SimulationMode simulationMode;

    [DisplayInfo(DisplayName = "EMS Parameters")]
    public Deltix.EMS.Coordinator.EMSParameters emsParameters = CreateEMSParameters();

    [DisplayInfo(DisplayName = "Proportion of instruments to include in randomization (%):")]
    public double instrumentsProportion = 100;

    [DisplayInfo(DisplayName = "ReInvest ?")]
    public bool ReInvestMode = true;

    [DisplayInfo(DisplayName = "Generate Reports ?")]
    public bool generateReports = true;

    [DisplayInfo(DisplayName = "Funding Currency:")]
    public string FundCurrency = @"USDT";

    [DisplayInfo(DisplayName = "Trading Commission (%):")]
    public double commission = 0;

    [DisplayInfo(DisplayName = "Maximum Order Size (in BTC):")]
    public double maxOrderSize = 0.2;

    #endregion
}





