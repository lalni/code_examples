# C# backtest template

## What
This is a repository to standardize the way our strategies are being executed on Quant Office.

## Why
This makes it easier to share functionality that is common across many strategies and reduce the amount of testing required for each individual test.

## Mental model

### InstrumentExecutor:
- Responsible for generating an```alpha``` variable which is to be interpreted by the PortfolioExecutor
- Has access to its own bar data
- Is responsible for its own event processing logic (OnDayClose, OnBarClose etc.)

### PortfolioExecutor:
- Has access to alpha signals from each instrument
- Is responsible for converting alpha signals into a Portfolio weighting (recalculate_weights)
- Is responsible for calling order execution (fulfilled by Rebalancer, see adjust_positions) since it has access to event processing logic (OnAfterBarClose, OnAfterDayClose)

### Transformations
The functions in this class should be stateless and are useful mathematical functions to be used in either generating a portfolio weighting or adjusting the portfolio weighting based on requirements such as neutrality.

### Rebalancer
Rebalancer should primarily be resonsible for execution of trades (see AdjustPositions). Currently it has the dual responsibility of making some transformations that can't be made static.

### Reporter
Set of messages to be sent to Slack based on portfolio events e.g. OnExit,OnDayClose 



## Quick Start

https://wavebridge-dev.atlassian.net/wiki/spaces/QauntStrategy/pages/1108639760/QO+102

## Common workflows:

### Change the signal of each instrument
Open InstrumentExecutor.cs in your preferred IDE. Find ```this.alpha```.

```csharp
    public override void OnBarClose() //we can just uncomment this code even if we use daily subscription, because they will ignore this.
    {
        try
        {
            // order sending algo should be come
            IBarInfo bar = this.bars.Current;
            if (bar is object)
            {
                this.UpdateInstrument(bar);
            }
            this.alpha = //Your code here
        } catch (Exception e)
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

```



### Change the way signals are combined into a portfolio

This example divides each instr.alpha by the absolute sum of alphas.

```csharp
    public void recalculate_weights()
    {
        Dictionary<string, double> instrFactor = new Dictionary<string, double>();
        if (Universe.Count == 0) return;
        foreach (InstrumentExecutor instr in Universe)
        {
            if (instr.alpha != double.NaN) // should already be filtered from universe but double check
            {
                instrFactor[instr.Symbol] = (double) instr.alpha;
            }
        }
        List<double> values = instrFactor.Values.ToList();
        double sum = values.Sum(x => Math.Abs(x));
        if (sum == 0 || double.IsNaN(sum)) return;
        List<string> symbols = instrFactor.Keys.ToList();

        // If you want to use Rebalance function, follow below steps
        Dictionary<string, double> instrWeights = new Dictionary<string, double>();
        for (int i = 0; i < symbols.Count; i++)
        {
            instrWeights.Add(symbols[i], values[i]/sum);
        }
        this.instrWeights = instrWeights;
    }
```

This example neutralizes the portfolio weights by sector.

```csharp
    public void recalculate_weights()
    {
        Dictionary<string, double> instrFactor = new Dictionary<string, double>();
        if (Universe.Count == 0) return;
        foreach (InstrumentExecutor instr in Universe)
        {
            if (instr.alpha != double.NaN) // should already be filtered from universe but double check
            {
                instrFactor[instr.Symbol] = (double) instr.alpha;
            }
        }

        // If you want to use Rebalance function, follow below steps
        Dictionary<string, double> instrWeights = new Dictionary<string, double>();
        this.instrWeights = rebalancer.Rebalance(instrFactor,sector_neutral=true);
    }
```

### Change the way orders are submitted 
For example adding filling orders over several time periods. For this to work, Rebalancer.AdjustPositions must be called several times for each time recalculate_weights is called.


```csharp
    public Rebalancer(PortfolioExecutor portfolioExecutor) {
        ...
            this.transitionFunction = weightedTransition; //define which Transition to use here
    }
```

```csharp
        public void AdjustPositions(Dictionary<string, double> instrumentWeights)
        {
            //...
            this.initialState = this.getCurrentState();
            // When there is a transition function defined then the rebalance occurs over several timeperiods/events rather than all at once.
            instrumentWeights = transitionFunction(); //see the current linearTransition function as an example
            //...
        }

```

The transition function definition:

```csharp
        public Dictionary<string, double> weightedTransition()
        {
            if (this.initialState.Equals(desiredState)) { return this.desiredState; };
            (List<string> symbols, List<double> initialValues, List<double> desiredValues) = DictionariesToList(this.initialState, this.desiredState);
            int numberOfTransitions = 4;
            if (this.rebalanceTurn >= numberOfTransitions) { return this.desiredState; };

            List<double> transitionWeights = new List<double> { 0.2, 0.3, 0.5 };

            #declare which transition function to use with required parameters.
            Matrix<double> weightedTransition = Transformations.transform.WeightedTransition(initialValues, desiredValues, transitionWeights);

            #select the row based on which rebalance turn is being executed.
            double[] transitionalWeights = weightedTransitions.ToRowArrays()[this.rebalanceTurn];

            Dictionary<string, double> result = new Dictionary<string, double>();
            for (int i = 0; i < symbols.Count; i++)
            {
                result[symbols[i]] = transitionalWeights[i];
            }
            return result;
        }
```



### Running tests

Load the TransformationTests/UnitTests.sln in Visual Studio. Make sure you've built the c-backtest-template project and then run the Test suite. Additionally you can call the ./pre-commit program.