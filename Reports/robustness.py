import pandas as pd
import numpy as np
from scipy.stats.mstats import winsorize


instrument_report = pd.read_csv("InstrumentReport.csv")
instrument_report = instrument_report[instrument_report["Type"] == "All Trades"]

def instrument_evaluation():
	instrument_report = pd.read_csv("InstrumentReport.csv")
	instrument_report = instrument_report[instrument_report["Type"] == "All Trades"]
	total_profit = instrument_report["Net Profit/Loss"].sum()
	sampled_profits = []
	for i in range(1000):
		sampled_profits.append(instrument_report["Net Profit/Loss"].sample(frac=0.7).sum())

	profitable_proportion = sum(0 < np.array(sampled_profits))/len(sampled_profits)
	print(f"""
	Average profit: 					${np.mean(sampled_profits)}
	
	Proportion of profit: 				{100*profitable_proportion:.2f}%
""")


def trade_evaluation():
	trade_report = pd.read_csv("TradeReport.csv")
	sampled_profits = []
	for i in range(1000):
		sampled_profits.append(trade_report['Net Profit'].sample(frac=0.7).sum())

	profitable_proportion = sum(0 < np.array(sampled_profits)) / len(sampled_profits)
	print(f"""
	Average profit: 					${np.mean(sampled_profits)}

	Proportion of profit: 				{100*profitable_proportion:.2f}%
""")


def period_evaluation():
	period_report = pd.read_csv("PeriodReport.csv")
	total_profit = np.prod(period_report["Return %"] +1)
	sampled_sharpe = []
	for i in range(1000):
		sample =  period_report["Return %"].sample(frac=0.7)

		sampled_sharpe.append(np.sqrt(365) * sample.mean()/sample.std())

	print(f"""After sampling 70% of trades 1000 times from the trade report the sharpe distribution:""")
	print(pd.Series(sampled_sharpe).describe())
	print("Winsorizing trading profits:")
	print(winsorize(period_report["Net Profit"], limits=[0.05,0.05]).sum())


def annualize_return(rate, days: int) -> float:
	"""Return the annualized return percentage given the holding return percentage and the number of months held.


	"""
	years= days/365
	# Ref: https://stackoverflow.com/a/52618808/
	rate = ((rate + 1)**(1 / years)) - 1
	return rate

def returns_over_drawdown(daily_returns):
	"""
	This is a measure of expected reward over path dependent risk. A value > 2 is reasonable
	"""

	days = 365
	sampled_returns = daily_returns.sample(len(daily_returns), replace=True)

	annual_drawdown = (np.cumprod(sampled_returns + 1) / np.cumprod(sampled_returns + 1).expanding().max() - 1).min()

	cumulative_returns = np.prod(sampled_returns + 1) - 1

	annualized_returns = annualize_return(cumulative_returns, days)

	return annual_drawdown ,abs(annualized_returns / annual_drawdown)


def market_trend():
	trades = pd.read_csv("TradeReport.csv")
	detrend = pd.read_csv("OKEXPerpHistDetrend1h.csv")

	detrend = detrend[['symbol', 'timestamp', 'BarMessage.close']]
	detrend['timestamp'] = pd.to_datetime(detrend['timestamp'], utc=True)
	trades = trades[['Instrument', 'Type', 'Size', 'Entry Time', 'Entry Price', 'Exit Time', 'Exit Price']]
	trades['Entry Time'] = pd.to_datetime(trades['Entry Time'], utc=True)
	trades['Exit Time'] = pd.to_datetime(trades['Exit Time'], utc=True)

	trades = pd.merge(detrend, trades, left_on=['symbol', 'timestamp'], right_on=['Instrument', 'Exit Time'])
	trades['Exit Price'] = trades['BarMessage.close']
	del (trades['timestamp'])
	del (trades['BarMessage.close'])
	del (trades['symbol'])

	trades = pd.merge(detrend, trades, left_on=['symbol', 'timestamp'], right_on=['Instrument', 'Entry Time'])
	trades['Entry Price'] = trades['BarMessage.close']

	trades['Position'] = (trades['Type'] == 'Long') * 1 + (trades['Type'] == 'Short') * -1

	detrended_profit = ((trades['Exit Price'] - trades['Entry Price']) * trades['Position'] * trades['Size']).sum()
	print(f"""
    Detrended net profit:   			${detrended_profit} 
    """)



def draw_down_analysis():
	period_report = pd.read_csv("PeriodReport.csv")
	returns_over_drawdown_samples = []
	drawdowns = []
	for _ in range(100):
		drawdown, return_over_drawdown = returns_over_drawdown(period_report['Return %'])
		returns_over_drawdown_samples.append(return_over_drawdown)
		drawdowns.append(drawdown)

	good_proportion_calmar = sum(0.5 < np.abs(np.array(returns_over_drawdown_samples)))/len(returns_over_drawdown_samples)
	good_proportion_drawdown = sum(0.4 > abs(np.array(drawdowns)))/len(drawdowns)
	avg_drawdown = np.array(drawdowns).mean()
	median_calmar = np.median(np.abs(np.array(returns_over_drawdown_samples)))
	print(f"""
	Annual drawdown (<40%):             {100*good_proportion_drawdown:.0f}%
    
    Average drawdown:					{avg_drawdown:.0f}%
    
    Calmar ratio (>0.5):                {100*good_proportion_calmar:.2f}%
    
    Median Calmar ratio:				{median_calmar:.2f}
""")


def return_analysis():
	period_report = pd.read_csv("PeriodReport.csv")
	sharpes = []
	for i in range(1000):
		sample_returns = period_report['Return %'].sample(frac=1, replace=True)
		sharpes.append(np.sqrt(365) * sample_returns.mean() / sample_returns.std())
	good_proportion_sharpes = sum(0.5 < np.array(sharpes))/len(sharpes)

	CAGRs = []
	for i in range(1000):
		CAGR = np.prod(period_report['Return %'].sample(n=365 ,replace=True)+1) -1
		CAGRs.append(CAGR)
	good_proportion_CAGR = sum(0.1 < np.array(CAGRs))/len(CAGRs)


	print(f"""
    Sharpe ratio(>0.5)                  {100*good_proportion_sharpes:.0f}%
    
    CAGR (>10%)                         {100*good_proportion_CAGR:.0f}% """)


print(f"""
# Robustness Test

""")

#instrument_evaluation()
#trade_evaluation()


#Max drawdown
#Calmar ratio

print("## Sampling from daily returns:")

print("""
    Metric:                  			Proportion
""")
return_analysis()
draw_down_analysis()
market_trend()


print("## Sampling from Instrument Returns:")
instrument_evaluation()

print("## Sampling from Trade Returns: ")
trade_evaluation()

print("## With detrended prices")
market_trend()
