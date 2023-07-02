using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RiskReporter
{
    public class Reporter
    {
        public string standardHeader
        {
            get { return string.Format("Strategy {0} at time {1} using {2} mode", this.portfolioExecutor, this.portfolioExecutor.CurrentTime, this.portfolioExecutor.ExecutionMode); }
        }
        List<string> currentState
        {
            get { return this.portfolioExecutor.rebalancer.getCurrentState().Select(i => $"{i.Key}: {i.Value}").ToList(); }
        }

        string channel
        {
            get
            {
                if (this.portfolioExecutor.ExecutionMode == QuantOffice.Execution.ExecutionMode.RealTime)
                {
                    return "quant-strategy-bot-notifications";
                }
                else
                {
                    return "알림-quant-strategy-backtest";
                }
            }
        }

        public PortfolioExecutor portfolioExecutor;
        public double previousPnL;
        public Reporter(PortfolioExecutor portfolioExecutor)
        {
            this.portfolioExecutor = portfolioExecutor;
        }

        public void OnDayClose()
        {
            try
            {
                //if (this.portfolioExecutor.ExecutionMode == QuantOffice.Execution.ExecutionMode.RealTime)
                //{
                    //0.5% loss/gain in a single day
                    double pct_gain = (this.portfolioExecutor.RealizedPnL - previousPnL) / this.portfolioExecutor.InitialCap;
                    if (pct_gain > 0.005)
                    {
                        SlackMessenger.Message(string.Format("Strategy has experienced a 0.5% gain in a single day."), goodnews: true, standardHeader, channel);
                    }
                    else if (pct_gain < -0.005)
                    {
                        SlackMessenger.Message(string.Format("Strategy has experienced a 0.5% loss in a single day."), goodnews: false, standardHeader, channel);
                    }

                    //drawdown at 25%
                    if (this.portfolioExecutor.RealizedPnL < this.portfolioExecutor.InitialCap * -0.1)
                    {
                        SlackMessenger.Message(string.Format("Strategy has experienced a 10% drawdown."), goodnews: false, standardHeader, channel);
                    }
                    //daily turnover reaches 600% for total portfolio

                    //highest trade volume in 30 days

                    previousPnL = this.portfolioExecutor.RealizedPnL;

                //}
            }
            catch (Exception e)
            {
                this.Error("error in Slack Reporter OnDayClose" + e.ToString());
            }

        }

        public void OnBarClose()
        {

        }

        public void OnInit()
        {
            if (this.portfolioExecutor.ExecutionMode == QuantOffice.Execution.ExecutionMode.RealTime)
            {
                SlackMessenger.Message("Strategy is starting", goodnews: true, standardHeader, channel);
            }

        }


        public void OnExit()
        {
            if (this.portfolioExecutor.ExecutionMode == QuantOffice.Execution.ExecutionMode.BackTesting)
            {
                SlackMessenger.Message("Backtest has finished", goodnews: true, standardHeader, channel);
            }
            else
            {
                SlackMessenger.Message("Strategy is exiting", goodnews: true, standardHeader, channel);
            }
        }

        public void Error(string error)
        {
            SlackMessenger.Message(error, goodnews: false, standardHeader, channel);
        }
    }
}
