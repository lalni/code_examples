.PHONY: environment clean

#to enable the anaconda shell Go to Visual Studio Code > "Ctrl + `" > 
#Select ⚙ settings cog > Terminal > Add > "Shell location" = "C:\Windows\system32\cmd.exe"
# and "Arguments" = "/K" C:\ProgramData\Miniconda3\Scripts\activate.bat C:\ProgramData\Miniconda3
# Select that shell from shell dropdown menu

# run commands with 'make feasability' from commandline.

all: feasability.md robustness.md
	echo Done
	
feasability.md: feasability.py PerformanceReport.csv
	python feasability.py > feasability.md

robustness.md: robustness.py TradeReport.csv PeriodReport.csv InstrumentReport.csv OKEXPerpHistDetrend1h.csv
	python robustness.py > robustness.md

OKEXPerpHistDetrend1h.csv:
	git archive --format=zip --remote=git@bitbucket.org:wavestoneproject/monte-carlo-trade-evaluation.git HEAD OKEXPerpHistDetrend1h.csv > OKEXPerpHistDetrend1h.zip
	python -c "import shutil; shutil.unpack_archive('OKEXPerpHistDetrend1h.zip','temp'); shutil.move('temp/OKEXPerpHistDetrend1h.csv','OKEXPerpHistDetrend1h.csv')"
	python -c "import shutil; shutil.rmtree('temp')"

OKEXPerpHist1h.csv:
	git archive --format=zip --remote=git@bitbucket.org:wavestoneproject/monte-carlo-trade-evaluation.git HEAD OKEXPerpHistDetrend1h.csv > OKEXPerpHistDetrend1h.zip
	python -c "import shutil; shutil.unpack_archive('OKEXPerpHistDetrend1h.zip','temp'); shutil.move('temp/OKEXPerpHistDetrend1h.csv','OKEXPerpHistDetrend1h.csv')"
	python -c "import shutil; shutil.rmtree('temp')"

clean:
	rm -f robustness.md feasability.md