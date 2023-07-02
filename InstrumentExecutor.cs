


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
using QuantOffice.DataProvider;
using QuantOffice.L2; //should be added if we want to use orderbook data
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
using QuantOffice.HistoryService.Data;
using Deltix.EMS.Coordinator;
using Deltix.EMS.API;
using Deltix.EMS.Simulator;
using Deltix.EMS.AlgoOrders;
using Deltix.EMS.AlgoOrders.Extended;
using Deltix.EMS.AlgoOrders.Slice;
using Deltix.EMS.SpecialOrders;
using Deltix.Timebase.Api.Messages;
using FinAnalysis.Base;
using FinAnalysis.TA;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.LinearRegression;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Distributions;

/// <summary>
/// This class contains instrument level event processing logic.
/// </summary>
public partial class InstrumentExecutor : InstrumentExecutorBase
{
    #region CustomProperties
    public double Marketcap; //Nullable -> double?
    public double PortfolioWeight;
    public double alpha;

    public double close;
    public double open;
    public double high;
    public double low;
    public double volume;
    public int barSize;
    public double midPrice;
    public string barUOM;

    public double[] volatility_list;
    public double[] priceReturn30bars;
    public double[] price_return_Y_value;
    public double[] price_return_AR1;
    public double[] price_return_AR2;

    public SimpleDataQueue pricequeue;
    public Rsi rsi;
    public SimpleDataQueue rsiQueue;






    public bool active
    {
        get
        {

            return (this.close > 0);
        }
    }


    #endregion

    #region LocalVariables

    public Font font = new Font("Times New Roman", 10);
    public OrderProcessor orderExecutor;
    private OrderBook orderBook;

    #endregion



    #region Events

    public override void OnInit()
    {
        pricequeue = new SimpleDataQueue(60);
        rsi = new Rsi(28);
        rsiQueue = new SimpleDataQueue(60, true);

        orderExecutor = PortfolioExecutor.orderExecutor;      // created in PortfolioExecutor. Reference shared by all symbols

        // Add Event Listeners to orderExecutor object


        orderExecutor.AddOrderStatusListener(OnOrderStatus, new OrderStatusFilter(Symbol));

        if (MetaInfo == null)
            throw new NotImplementedException("The dictionary doesn't contain definition for the symbol: " + Symbol);

    }

    public override void OnBarClose() //we can just uncomment this code even if we use daily subscription, because they will ignore this.
    {
        rsi.Add(this.close);
        try
        {
            // order sending algo should be come
            IBarInfo bar = this.bars.Current;
            if (bar != null && !Double.IsNaN(bar.Open))
            {

                this.UpdateInstrument(bar);
            }
            else
            {
                // bar becomes null when we subscribe IntraDaily data. In that case we need to use another method for updating bar
                this.UpdateDailyWithIntraday();
            }

            pricequeue.Put(this.close);

            rsiQueue.Put(rsi.RSI);
            if (pricequeue.Count < 60)
            {
                // Log(string.Format("Symbol: {0}, Number of date in queue: {1}", Symbol, pricequeue.Count));
                return;
            }
            else
            {

                double[] TwoMonthPrice = pricequeue.Reverse().ToArray();
                //List<double> Volatility_list = new List<double>();
                //for (int i = 0; i < 29; i++)
                //{
                //    double average = TwoMonthPrice.Skip(i).Take(i + 30).Average();
                //    double sumOfSquaresOfDifferences = TwoMonthPrice.Skip(i).Take(i + 30).Select(val => (val - average) * (val - average)).Sum();
                //    double sd = Math.Sqrt(sumOfSquaresOfDifferences / 30);
                //    Volatility_list.Add(sd);
                //}
                //volatility_list = Volatility_list.ToArray();


                List<double> temporary_30D_return_list = new List<double>(); ;
                for (int i = 0; i < 29; i++)
                {
                    double return_value_30d = TwoMonthPrice[i] / TwoMonthPrice[i + 30] - 1;
                    if (Double.IsNaN(return_value_30d) == true)
                    {
                        return_value_30d = 0.001;
                        temporary_30D_return_list.Add(return_value_30d);
                    }
                    else
                    {
                        temporary_30D_return_list.Add(return_value_30d);
                    }
                }
                priceReturn30bars = temporary_30D_return_list.ToArray();

                double[] price_array_for_autoregression = pricequeue.Reverse().Take(32).ToArray();


                List<double> price_return_Y_list = new List<double>();
                for (int i = 0; i < 29; i++)
                {
                    double priceReurn1D = price_array_for_autoregression[i] / price_array_for_autoregression[i + 1] - 1;
                    if (Double.IsNaN(priceReurn1D) == true)
                    {
                        priceReurn1D = 0.001;
                        price_return_Y_list.Add(priceReurn1D);
                    }
                    else
                    {
                        price_return_Y_list.Add(priceReurn1D);
                    }
                }
                price_return_Y_value = price_return_Y_list.ToArray();

                List<double> price_return_AR1_list = new List<double>();
                for (int i = 1; i < 30; i++)
                {
                    double priceReurn1D_AR1 = price_array_for_autoregression[i] / price_array_for_autoregression[i + 1] - 1;

                    if (Double.IsNaN(priceReurn1D_AR1) == true)
                    {
                        priceReurn1D_AR1 = 0.001;
                        price_return_AR1_list.Add(priceReurn1D_AR1);
                    }
                    else
                    {
                        price_return_AR1_list.Add(priceReurn1D_AR1);
                    }

                }

                price_return_AR1 = price_return_AR1_list.ToArray();

                List<double> price_return_AR2_list = new List<double>();
                for (int i = 2; i < 31; i++)
                {
                    double priceReurn1D_AR2 = price_array_for_autoregression[i] / price_array_for_autoregression[i + 1] - 1;

                    if (Double.IsNaN(priceReurn1D_AR2) == true)
                    {
                        priceReurn1D_AR2 = 0.001;
                        price_return_AR2_list.Add(priceReurn1D_AR2);
                    }
                    else
                    {
                        price_return_AR2_list.Add(priceReurn1D_AR2);
                    }

                }
                price_return_AR2 = price_return_AR2_list.ToArray();
                double[] rsiarray = rsiQueue.Reverse().Take(29).ToArray();

                alpha = residualWithLasso(priceReturn30bars, price_return_AR1, price_return_AR2, price_return_Y_value);
                if (Normal.InvCDF(0, alpha * alpha, 0.6) > Math.Abs(alpha))
                {
                    alpha = Exponential.PDF(1, alpha) * alpha;
                }



                Log(string.Format("Symbol: {0}, Alpha: {1}", Symbol, alpha));









            }
        }
        catch (Exception e)
        {
            if (portfolioExecutor.ExecutionMode != ExecutionMode.RealTime)
            {
                throw (e);
            }
            else
            {
                Log("InstrumentExecutor.OnBarClose " + e.ToString(), EventSeverity.Error);
            }
        }
    }

    /// <summary>
    /// Update OHLCV based on bar.
    /// </summary>
    public void UpdateInstrument(IBarInfo bar)
    {
        if (bar.HasClose())
        {
            this.close = bar.Close;
        }
        if (bar.HasOpen())
        {
            this.open = bar.Open;
        }
        if (bar.HasHigh())
        {
            this.high = bar.High;
        }
        if (bar.HasLow())
        {
            this.low = bar.Low;
        }
        if (bar.HasVolume())
        {
            this.volume = bar.Volume;
        }
    }


    /// <summary>
    /// Update the bar level information based on the high,low,open close so far.
    /// </summary>
    public void UpdateDailyWithIntraday()
    {
        IBarInfo bar = this.bars.Current;
        if (bar.HasClose())
        {
            this.close = bar.Close;
        }

        //If we subscribe daily data seperately, this.bars.IsDailyAggregator will be false
        if (this.bars.IsDailyAggregator || bar == null)
        {
            this.close = DayClose;
            this.open = DayOpen;
            this.high = DayHigh;
            this.low = DayLow;
            this.volume = Volume;
        }
        else
        {
            int bar_count = this.IntradayToDailyConvertNumber();
            if (bar.HasClose())
            {
                this.close = bar.Close;
            }
            IBarInfo previous_bar = this.bars.BarsAgo(bar_count);
            if (previous_bar != null && previous_bar.HasOpen()) {
                this.open = previous_bar.Open;
            }
            //for high and low, we need to stack every  bar close of use for loop here
            this.volume = 0;
            double high = bar.High;
            double low = bar.Low;
            for (int i = 0; i < bar_count + 1; i++)
            {
                IBarInfo past_bar = this?.bars?.BarsAgo(i);

                if (past_bar == null) { continue; }

                if (past_bar.HasVolume()) {
                    this.volume += past_bar.Volume;
                }
                if (past_bar.HasHigh())
                {
                    if (past_bar.High > high) high = past_bar.High;
                }
                if (past_bar.HasLow())
                {
                    if (past_bar.Low < low) low = past_bar.Low;
                }
            }
            this.high = high;
            this.low = low;
        }
    }

    public int IntradayToDailyConvertNumber()
    {
        barSize = this.bars.BarSize.Size;
        barUOM = this.bars.BarSize.UOM.ToString();
        if (barUOM == "Hour")
        {
            return 24 / barSize;
        }
        else if (barUOM == "Minute")
        {
            return 24 * 60 / barSize;
        }
        else if (barUOM == "Second")
        {
            return 24 * 60 * 60 / barSize;
        }
        else
        {
            throw new NotImplementedException("Are you sure using daily calculated alpha? Then assign individual daily bar subscription for memory");
        }
    }




    public double VresidualReg(double[] a, double[] x, double[] y, double[] z)
    {

        if (a == null || x == null || y == null || z == null)
        {

            return 0;
        }
        //Log(string.Format("VresidualReg Values for {0}, a : {1}, x: {2}, y: {3}, z: {4}", Symbol, string.Join(" ", a), string.Join(" ", x), string.Join(" ", y), string.Join(" ", z)));
        double[][] x1 = { a, x, y };
        Matrix X1 = (Matrix)DenseMatrix.OfRowArrays(x1).Transpose();
        double[][] x2 = { x, y, z };
        Matrix X2 = (Matrix)DenseMatrix.OfRowArrays(x2).Transpose();

        Matrix X3 = (Matrix)X1.Stack(X2);
        DenseVector Y3 = DenseVector.OfArray(z.Concat(a).ToArray());
        DenseVector Y1 = DenseVector.OfArray(z);
        DenseVector Y2 = DenseVector.OfArray(a);

        double mean;
        double lastone;

        try
        {
            Vector<double> R = MultipleRegression.NormalEquations(X3, Y3);
            Vector<double> E = X3 * R;
            Vector<double> Res = Y3 - E;
            double[] last = Res.ToArray();
            lastone = last.Skip(27).Take(2).Average();

            mean = Res.Average();// RR.Average();
        }
        catch (ArgumentException e)
        {
            mean = 0;
            lastone = 0;
        }

        return lastone * -1;
    }


    public double residualWithLasso(double[] a, double[] x, double[] y, double[] z)
    {

        if (a == null || x == null || y == null || z == null)
        {

            return 0;
        }
        double[][] x1 = { a, x, y };
        double [] weights = LassoRegression(x1, z, 0.5, 10);
        Matrix X1 = (Matrix)DenseMatrix.OfRowArrays(x1).Transpose();
        DenseVector Y1 = DenseVector.OfArray(z);
        DenseVector W = DenseVector.OfArray(weights);
        Vector<double> E = X1 * W;
        Vector<double> Res = Y1 - E;
        double[] last = Res.ToArray();
        double lastone = last.Skip(27).Take(2).Average();



        double mean = Res.Average();// RR.Average();





        return lastone*-1;
    }

    public static double[] LassoRegression(double[][] inputs, double[] outputs, double lambda, int maxIterations)
    {
        Matrix X1 = (Matrix)DenseMatrix.OfRowArrays(inputs).Transpose();
        DenseVector Y1 = DenseVector.OfArray(outputs);
        int numFeatures = X1.ColumnCount;
        int numSamples = X1.RowCount;

        // Initialize weights with zeros
        double[] weights = new double[numFeatures];

        // Perform coordinate descent optimization
        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            for (int feature = 0; feature < numFeatures; feature++) //
            {
                double sumSquaredErrors = 0;

                // Compute residual  
                for (int sample = 0; sample < numSamples; sample++)
                {
                    double prediction = Predict(inputs[sample], weights); 
                    double error = prediction - outputs[sample];
                    sumSquaredErrors += error * error;
                }

                double weight = weights[feature]; // double weight = weights[feature];

                // Update weight using soft thresholding 
                double update = 0; // f
                double featureSum = 0;
                for (int sample = 0; sample < numSamples; sample++)
                {
                    double prediction = Predict(inputs[sample], weights); // first sample's features * weights  second features * weights 
                    double error = prediction - outputs[sample];
                    featureSum += inputs[sample][feature] * error; // i
                   
                }

                if (featureSum < -lambda)
                    update = (featureSum + lambda) / numSamples;
                else if (featureSum > lambda)
                    update = (featureSum - lambda) / numSamples;

                weights[feature] = update;
            }
        }

        return weights;
    }

    public static double Predict(double[] input, double[] weights)
    {
        double prediction = 0;
        for (int i = 0; i < input.Length; i++)
        {
            prediction += input[i] * weights[i];
        }
        return prediction;
    }

public override void OnDayClose()
    {


        try
        {
            IBarInfo bar = this.daily.Today;
         
            // if statement with simulation mode has limitation. / this.daily can be not null when we use individual daily bar stream.
            if (bar != null && !Double.IsNaN(bar.Open))
            {
                this.UpdateInstrument(bar);
            }
            else
            {
                // bar becomes null when we subscribe IntraDaily data. In that case we need to use another method for updating bar
                this.UpdateDailyWithIntraday();
      
            }
        }
        catch (Exception e)
        {
            if (portfolioExecutor.ExecutionMode != ExecutionMode.RealTime)
            {
                throw (e);
            }
            else
            {
                Log("InstrumentExecutor.OnDayClose " + e.ToString(), EventSeverity.Error);
            }
        }
    }
    public void OnOrderStatus(object sender, OrderStatusEventArgs e)
    {


        OrderStatusInfo info = e.OrderStatusInfo;
        Order order = orderExecutor.GetOrderData(info.OrderId);

        if (order == null)
        {
            Log("Order is Null");
            return;
        }


        switch (info.OrderStatus)
        {
            case OrderStatus.Filled:
                if (order.Side == OrderSide.Sell)
                {
                    this.portfolioExecutor.TransitionToSendOrders(this.midPrice);
                }
                break;

            case OrderStatus.Rejected:
                try
                {
                    /* Position pos = orderExecutor.GetPositionData(Symbol, portfolioExecutor.StrategyName);
                     Position newPosition = (Position)pos.Clone();

                     if (order.Side == OrderSide.Buy)
                     {
                         newPosition.Size += (long)order.Size;
                         Log(string.Format("{0} position increased by {1} after the order got rejected", Symbol, order.Size));
                     }
                     else
                     {
                         newPosition.Size -= (long)order.Size;
                         Log(string.Format("{0} position decreased by {1} after the order got rejected", Symbol, order.Size));
                     }
                     orderExecutor.SetPositionData(newPosition);*/
                }
                catch (Exception e1)
                {
                    Log("Position Data could not be updated: " + e1.ToString(), EventSeverity.Error);
                }

                break;

            default:
                break;
        }
    }

    public override void OnBestBidAsk(IBestBidOfferMessageInfo bestBidAsk)
    {
        base.OnBestBidAsk(bestBidAsk);

        if (bestBidAsk != null)
        {
            if (Double.IsNaN(bestBidAsk.BidPrice) && Double.IsNaN(bestBidAsk.OfferPrice))
            {
                midPrice = (bestBidAsk.BidPrice + bestBidAsk.OfferPrice) / 2;
            }
        }

    }
    #endregion

    #region Scales

    #endregion

    #region InputParameters

    #endregion


    #region UtilityFunctions


    public override string ToString() {
        return String.Format("Symbol: {0}, Close: {1}, Alpha: {2}", Symbol, close, alpha);
    }


    public new int Log(string text, EventSeverity eventSeverity)
    {
        if (eventSeverity == EventSeverity.Error)
        {
            //Send error with additional context
            int row = base.Log(text, eventSeverity);
            base.Log(this.ToString(), eventSeverity, row);
            return row;
        }
        else
        {
            return base.Log(text, eventSeverity);
        }
    }




    public double getMinOrderSize()
    {
        SecurityMetaInfo securityInfo = (SecurityMetaInfo)MetaInfo;
        Deltix.DFP.Decimal64 minOrderSizeDecimal = (Deltix.DFP.Decimal64)this.SecurityMetaInfoProvider.FindSecurityMetaInfo(this.Symbol + "." + this.portfolioExecutor.exchange).GetCustomAttribute("minOrderSizeExt");

        double minOrderSize = minOrderSizeDecimal.ToDouble();
        return minOrderSize;
    }

    public double getSizeIncrement()
    {
        SecurityMetaInfo securityInfo = (SecurityMetaInfo)MetaInfo;
        Deltix.DFP.Decimal64 sizeIncrementDecimal = (Deltix.DFP.Decimal64)this.SecurityMetaInfoProvider.FindSecurityMetaInfo(this.Symbol + "." + this.portfolioExecutor.exchange).GetCustomAttribute("sizeIncrement");
        double sizeIncrement = sizeIncrementDecimal.ToDouble();
        return sizeIncrement;
    }

    public double getTick()
    {
        CurrencyMetaInfo currencyInfo = (CurrencyMetaInfo)MetaInfo;
        double Tick = currencyInfo.Tick;
        return Tick;
    }

    public double getContractMultiplier()
    {
        CurrencyMetaInfo currencyInfo = (CurrencyMetaInfo)MetaInfo;
        double ContractMultiplier = currencyInfo.ContractMultiplier;
        return ContractMultiplier;
    }

    public double getBid()
    {
        double approximateDownTick = close.RoundToMultiple(getTick());
        return approximateDownTick;
    }

    public double getAsk()
    {
        double approximateUpTick = close.RoundToMultiple(getTick());
        return approximateUpTick;
    }
    #endregion
}






