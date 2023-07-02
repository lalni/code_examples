using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


/// <summary>
/// Type which enforces ||weights|| == 1 
/// </summary>
/// 

public class PortfolioWeights
{
    public readonly double[] values; //readonly to make immutable after declaration
    public readonly string[] symbols;
    public readonly Dictionary<string, double> weights;
    public readonly bool empty;

    public PortfolioWeights(Dictionary<string, double> input)
    {
        if (input.Count == 0)
        {
            values = new double[0];
            symbols = new string[0];
            weights = new Dictionary<string, double>();
            empty = true;
        }

        double sum = input.Values.Sum(x => Math.Abs(x));
        if (sum != 0)
        {
            values = input.Values.Select(x => x / sum).ToArray();
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] >= 0)
                {
                    values[i] = Math.Min(0.1, Math.Max(0.03,values[i]));


                }

                else
                {
                    values[i] = Math.Max(-0.1, Math.Min(-0.03, values[i]));

                }
            }

        }
        else
        {
            values = input.Values.ToArray();
        }

        symbols = input.Keys.ToArray();
        empty = false;

        weights = new Dictionary<string, double>();
        for (int i = 0; i < symbols.Length; i++)
        {
            weights.Add(symbols[i], values[i]);
        }

    }

}

