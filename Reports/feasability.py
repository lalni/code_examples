import pandas as pd

df = pd.read_csv("PerformanceReport.csv")

all_trades = df[df["Type"] == "All Trades"].iloc[0]

print(f"""
CAGR (>10%):			{all_trades["CAGR"]:.2f}, {all_trades["CAGR"]>0.1}

Gain to Pain ratio (>1.2): 	{all_trades["Total Profit"]/abs(all_trades["Total Loss"]):.2f}, {all_trades["Total Profit"]/abs(all_trades["Total Loss"])>1.2}

Sharpe ratio (>0.5):		{all_trades["Sharpe Ratio"]:.2f},{all_trades["Sharpe Ratio"]>0.5}

Historical 95% VaR (>-3%):	{all_trades["Historical VaR 95% 1D"]/all_trades["InitialCap"]:.2f},{all_trades["Historical VaR 95% 1D"]/all_trades["InitialCap"]>-0.03}

Longest drawdown (<6 months):   {all_trades["Max Drawdown Duration"]} days,{all_trades["Max Drawdown Duration"]< 150}

Max drawdown (<40%): 		{100*abs(all_trades["Max Drawdown %"]):.2f}%, {abs(all_trades["Max Drawdown %"])<0.4}

Calmar ratio (>0.5): 		{all_trades["Return/Drawdown Ratio"]:.2f}, {all_trades["Return/Drawdown Ratio"]> 0.5}

""")