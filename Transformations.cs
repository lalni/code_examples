using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Distributions;

using QuantOffice.Execution;

namespace Transformations
{
    public static class transform
    {   
        //Array to Vector
        public static Vector<double> Vectorization(List<double> input)
        {
            int input_lenght = input.Count; 
            double[] input_array = input.ToArray();
            Vector<double> input_vector = Vector<double>.Build.Dense(input_lenght);
            input_vector.SetValues(input_array);

            return input_vector;
        }

        //transition functions
        public static double[] SimpleTransition(List<double> current_state, List<double> desired_state)
        {
            return desired_state.ToArray();
        }
    	
        public static double[] SpeedbumpTransition(List<double> current, List<double> desired, double speedbump)
        {   
            //If you set speedbump to large number, long-short neutral can be broken and weight sum can exceed 1.
            int input_length = current.Count;
            Vector<double> current_state = Vectorization(current);
            Vector<double> desired_state = Vectorization(desired);

            double[] speedbump_array = Enumerable.Repeat(speedbump, input_length).ToArray();
            Vector<double> speedbump_state = Vector<double>.Build.Dense(input_length);
            speedbump_state.SetValues(speedbump_array);

            Vector<double> distances = desired_state.Subtract(current_state).PointwiseAbs();
            Vector<double> change_amount = distances.Subtract(speedbump_state);

            Vector<double> above_bump = (change_amount.PointwiseSign() + 1) / 2;
            Vector<double> below_bump = (1 - change_amount.PointwiseSign()) / 2;

            Vector<double> result = current_state.PointwiseMultiply(below_bump) + desired_state.PointwiseMultiply(above_bump);
            return result.ToArray();
        }

        public static double[] ReduceTurnover(List<double> current, List<double> desired, double max_turnover)
        {   
            //max_turnover : 0 ~ 1
            int input_length = current.Count;
            Vector<double> current_state = Vectorization(current);
            Vector<double> desired_state = Vectorization(desired);
            Vector<double> distances = desired_state.Subtract(current_state).PointwiseAbs();

            double desired_turnover = distances.Sum() / 2;
            if (desired_turnover < max_turnover)
            {
                return desired.ToArray();
            }
            else
            {
                var result = current_state + distances * (max_turnover / desired_turnover);
                return result.ToArray();
            }
        }

        public static Matrix<double> LinearTransition(List<double> current, List<double> desired, int period)
        {
            int input_length = current.Count;
            List<double> current_list = new List<double>(); //for concatenating current states to make matrix
            for (int i = 0; i < period; i++)
            { 
                current_list.AddRange(current);
            }
            Vector<double> current_state = Vectorization(current);
            Vector<double> desired_state = Vectorization(desired);
            Vector<double> distances = desired_state.Subtract(current_state);
            Vector<double> steps = DenseVector.OfArray(Enumerable.Range(1, period).Select(x => (double)x / period ).ToArray());

            Matrix<double> current_matrix = Matrix<double>.Build.Dense(input_length, period, current_list.ToArray());
            current_matrix = current_matrix.Transpose();
            Matrix<double> steps_matrix = Matrix<double>.Build.Dense(period, 1, steps.ToArray());
            Matrix<double> distances_matrix = Matrix<double>.Build.Dense(1, input_length, distances.ToArray());
            Matrix<double> plan_matrix = steps_matrix.Multiply(distances_matrix);

            return current_matrix + plan_matrix;
        }

        public static Matrix<double> ExponentialTransition(List<double> current, List<double> desired, int period, double rate)
        {   
            //rate : double < 1
            int input_length = current.Count;
            List<double> current_list = new List<double>(); //for concatenating current states to make matrix
            for (int i = 0; i < period; i++)
            {
                current_list.AddRange(current);
            }
            Vector<double> current_state = Vectorization(current);
            Vector<double> desired_state = Vectorization(desired);
            Vector<double> distances = desired_state.Subtract(current_state);
            Vector<double> steps = DenseVector.OfArray(Enumerable.Range(1, period).Select(x => Math.Pow(rate, x)).ToArray());
            double shortfall = (1 - steps.Sum()) / (period - 1);

            double[] cum_steps = new double[steps.Count]; //cumulative sum of 'steps' array
            for (int i = 0; i < steps.Count; i++)
            {
                if (i == 0)
                {
                    cum_steps[i] = steps[i];
                }
                else
                {
                    cum_steps[i] = steps[i] + cum_steps[i - 1] + shortfall;
                }
            }
            Matrix<double> current_matrix = Matrix<double>.Build.Dense(input_length, period, current_list.ToArray());
            current_matrix = current_matrix.Transpose();
            Matrix<double> steps_matrix = Matrix<double>.Build.Dense(period, 1, cum_steps);
            Matrix<double> distances_matrix = Matrix<double>.Build.Dense(1, input_length, distances.ToArray());
            Matrix<double> plan_matrix = steps_matrix.Multiply(distances_matrix);

            return current_matrix + plan_matrix;
        }


        public static Matrix<double> WeightedTransition(List<double> current, List<double> desired, List<double> weights)
        {
            //weights = list of floats. sum of weights == 1, The lenght of weights indicate the time period 
            int input_length = current.Count;
            int period = weights.Count;
            List<double> current_list = new List<double>(); //for concatenating current states to make matrix
            for (int i = 0; i < period; i++)
            {
                current_list.AddRange(current);
            }
            Vector<double> current_state = Vectorization(current);
            Vector<double> desired_state = Vectorization(desired);
            Vector<double> distances = desired_state.Subtract(current_state);

            double[] steps = new double[weights.Count]; //cumulative sum of 'steps' array
            for (int i = 0; i < weights.Count; i++)
            {
                if (i == 0)
                {
                    steps[i] = weights[i];
                }
                else
                {
                    steps[i] = weights[i] + steps[i - 1];
                }
            }
            Matrix<double> current_matrix = Matrix<double>.Build.Dense(input_length, period, current_list.ToArray());
            current_matrix = current_matrix.Transpose();
            Matrix<double> steps_matrix = Matrix<double>.Build.Dense(period, 1, steps);
            Matrix<double> distances_matrix = Matrix<double>.Build.Dense(1, input_length, distances.ToArray());
            Matrix<double> plan_matrix = steps_matrix.Multiply(distances_matrix);

            return current_matrix + plan_matrix;
        }


        //rank functions, apply default parameter of ascending = true    	
        public static Dictionary<string, double> Rank(Dictionary<string, double> input) {
    		//cross-sectional rank
            return Rank(input,false); //small alpha go long (descending order)
	    }

		public static Dictionary<string, double> Rank(Dictionary<string, double> input, bool ascending)
		{   
            //input : instrFactor dictionary {symbol : alpha}
            //linear : true -> linear weighting, false -> discrete weighting

			int input_length = input.Count;
			List<double> inputValues = input.Values.ToList();
			Dictionary<string, double> rank_dictionary = new Dictionary<string, double>();
			if (ascending)
			{
				rank_dictionary = input.OrderBy(g => g.Value)
					.Select((g, index) => (key: g.Key, rank: (double)index / (input_length - 1)))
					.ToDictionary(x => x.key, x => x.rank);
			}
			else
			{
				rank_dictionary = input.OrderByDescending(g => g.Value)
					.Select((g, index) => (key: g.Key, rank: (double)index / (input_length-1)))
					.ToDictionary(x => x.key, x => x.rank);
			}

            return rank_dictionary;
            
        }

        public static List<double> LinearRank(Dictionary<string, double> rank_dictionary)
        {
            List<double> rank = rank_dictionary.Values.ToList();
            if (!rank.TrueForAll(x => x == 0))
            {
                rank_dictionary = rank_dictionary.Select(i => (key: i.Key, value: i.Value - 0.5f))
                    .ToDictionary(x => x.key, x => x.value);
            }

            return rank_dictionary.Values.ToList();
        }

        public static List<double> DiscreteRank(Dictionary<string, double> rank_dictionary, double threshold)
        {   
            //threshold : short for highest quantile, long for lowest quantile (threshold should be 0~0.5)
            List<double> rank = rank_dictionary.Values.ToList();
            if (!rank.TrueForAll(x => x == 0))
            {
                rank_dictionary = rank_dictionary.Select(i => (key: i.Key, value: i.Value >= 1-threshold ? (double) -1 : i.Value <= threshold ? 1 : 0))
                    .ToDictionary(x => x.key, x => x.value); ;
            }

            return rank_dictionary.Values.ToList();
        }

        public static List<double> SigmoidRank(Dictionary<string, double> rank_dictionary, double expanding)
        {   
            //expanding : can expand x range wider or narrower
            double Sigmoid(double x)
            {
                return 1 / (1 + Math.Exp(expanding * (-x + 0.5f))) - 0.5f;
            }

            List<double> rank = rank_dictionary.Values.ToList();
            if (!rank.TrueForAll(x => x == 0))
            {
                rank_dictionary = rank_dictionary.Select(i => (key: i.Key, value: Sigmoid(i.Value)))
                    .ToDictionary(x => x.key, x => x.value);
            }

            return rank_dictionary.Values.ToList();
        }

    public static double[] Zscore(double[] input)
        {
            double mean = input.Average();
            double var = input.Sum(x => Math.Pow(x - mean, 2)) / input.Length;
            double std = Math.Sqrt(var);
            return input.Select(x => (x - mean) / std).ToArray();
        }

    public static double[] Tilt(double[] input)
        {
            double [] tilted = new double[] { };        
            foreach (double x in input)
            {
                if (x >= 0)
                {
                    double y = x + 1;
                    tilted = tilted.Append(y).ToArray();
                }
                else if (x <0)
                {
                    double y = 1 / (1 - x);
                    tilted = tilted.Append(y).ToArray();
                }       

            }
            return tilted;  
        }



    public static List<double> ZscoreRank(Dictionary<string, double> rank_dictionary)
        {
            List<double> rank = rank_dictionary.Values.ToList();
            int index = rank.FindIndex(x => double.IsNaN(x));
            if (index !=-1)
            {
                rank[index] = 0;
            }
            double mean = rank.Average();
            double var = rank.Sum(x => Math.Pow(x - mean, 2)) / rank.Count;
            double std = Math.Sqrt(var);

            if (!rank.TrueForAll(x => x == 0))
            {
                rank_dictionary = rank_dictionary.Select(i => (key: i.Key, value: (i.Value - mean)/std ))
                    .ToDictionary(x => x.key, x => x.value);
            }

            return rank_dictionary.Values.ToList();
        }

        public static double TsRank(double[] input, bool ascending)
        {
            //input : historical alpha list of one instrument
            //return current rank among full history -> can be used as signal in Instrument Level.

            double last_input = input[input.Length - 1];
            int result;

            if (ascending)
            {
                var sorted_input = input.OrderBy(g => g).ToList();
                result = sorted_input.IndexOf(last_input);
            }
            else
            {
                var sorted_input = input.OrderByDescending(g => g).ToList();
                result = sorted_input.IndexOf(last_input);
            }

            return (double)result/ (double) (input.Length - 1);
        }

        
        

        public static double[] neutralization(List<double> input)
        {
            //after executing neutralization function, we can get desired state which can be used in 'ReduceTurnover' and 'SpeedBump'
            //input : List that indicates desired state(should contain both positive and negative values)
            
            int input_length = input.Count;
            double min = input.Min();
            double max = input.Max();
            if (min >= 0 || max <= 0)
            {
                double[] zeros = new double[input_length];
                Array.Clear(zeros, 0, input_length);
                return zeros;
            }


            double[] current_state_array = input.ToArray();

            Vector<double> current_state = Vector<double>.Build.Dense(input_length);

            current_state.SetValues(current_state_array);

            Vector<double> longs = current_state.PointwiseMultiply((current_state.PointwiseSign() + 1) / 2); //original value if > 0 else 0
            longs = longs / (2 * longs.PointwiseAbs().Sum());
            Vector<double> shorts = -1 * current_state.PointwiseMultiply((current_state.PointwiseSign() - 1) / 2); // original value if < 0 else 0
            shorts = shorts / (2 * shorts.PointwiseAbs().Sum());
            

            return (shorts + longs).ToArray();

        }


        public static double[] GranualNeutralization(List<double> input, List<List<int>> groups = null, List<double> betas = null)
        {
            //after executing neutralization function, we can get desired state which can be used in 'ReduceTurnover' and 'SpeedBump'
            int input_length = input.Count;

            if (groups == null)
            {
                groups = new List<List<int>> { Enumerable.Range(0, input_length - 1).ToList() };
            }
            double[] total_result = new double[input_length];

            foreach(List<int> group in groups)
            {
                if (group.Count == input_length)
                {
                    return neutralization(input);
                }
                List<double> group_input = new List<double>();
                foreach(int index in group) { group_input.Add(input[index]); }
                double[] group_result = neutralization(group_input);
                for (int i=0; i<group.Count; i++)
                {
                    total_result[group[i]] = group_result[i];
                }
            }

            double denominator = total_result.Select(x => Math.Abs(x)).ToArray().Sum();

            return total_result.Select(x => x / denominator).ToArray();
        }

        // same as double[] neutralization but does the dictionary to array transform internally.
        public static Dictionary<string, double> neutralization(Dictionary<string, double> input)
        {

            double[] desiredWeights = new double[input.Count];
            int i = 0;
            foreach (double value in input.Values)
            {
                desiredWeights[i] = value;
                i = i + 1;
            }

            string[] desiredSymbols = new string[input.Count];
            int j = 0;
            foreach (string key in input.Keys)
            {
                desiredSymbols[j] = key;
                j = j + 1;
            }

 
            var outputWeights = neutralization(desiredWeights.ToList());
            Dictionary<string, double> output = new Dictionary<string, double>();

            for (int k=0; k < outputWeights.Length; k++)
            {
                output.Add(desiredSymbols[k], outputWeights[k]);
            }
            return output;

        }


        public static Dictionary<string, double> GranualNeutralization(Dictionary<string, double> input, List<List<int>> groups = null, List<double> betas = null)
        {

            int input_length = input.Count;
            double[] desiredWeights = new double[input.Count];
            string[] desiredSymbols = new string[input.Count];

            for (int i = 0; i < input.Count; i++)
            {
                desiredSymbols[i] = input.Keys.ToList()[i];
                desiredWeights[i] = input.Values.ToList()[i];
            }

            if (groups == null)
            {
                groups = new List<List<int>> { Enumerable.Range(0, input_length - 1).ToList() };
            }
            Dictionary<string, double> total_result = new Dictionary<string, double>();

            foreach (List<int> group in groups)
            {
                if (group.Count == input_length)
                {
                    return neutralization(input);
                }
                Dictionary<string, double> group_input = new Dictionary<string, double>();
                foreach (int index in group) { group_input.Add(desiredSymbols[index], desiredWeights[index]); }
                Dictionary<string, double> group_result = neutralization(group_input);
                foreach(var res in group_result)
                {
                    total_result.Add(res.Key, res.Value);
                }
            }

            double denominator = total_result.Select(x => Math.Abs(x.Value)).ToArray().Sum();

            return total_result.Select(i => (key: i.Key, value: i.Value/denominator))
                    .ToDictionary(x => x.key, x => x.value);

        }

        public static double[] Winsorize(double[] input, double percentile)
        {
            if (input.Length == 1)
            {
                return new double[] { 0 };
            }

            var quantiles = Percentile(input, percentile);
            double lowerQuantile = quantiles.Item1;
            double upperQuantile = quantiles.Item2;
            Vector<double> original = Vectorization(input.ToList());
            Vector<double> upperOutliers = (original.Subtract(upperQuantile).PointwiseSign()+1)/2; //1 for point which is above upperQuantile otherwise 0 e.g. [1,0,0,0,1..]
            Vector<double> lowerOutliers = -1 * (original.Subtract(lowerQuantile).PointwiseSign() - 1) / 2; //1 for point which is below lowerquantile otherwise 0 e.g. [1,0,0,0,1..]
            Vector<double> result = original - upperOutliers.PointwiseMultiply(original) + upperOutliers * upperQuantile - lowerOutliers.PointwiseMultiply(original) + lowerOutliers * lowerQuantile;
            return result.ToArray();

        }


        public static double[] Winsorize(double[] input, double upperCutoff, double lowerCutoff)
        {
            if (input.Length == 1)
            {
                return new double[] { 0 };
            }

            Vector<double> original = Vectorization(input.ToList());
            Vector<double> upperOutliers = (original.Subtract(upperCutoff).PointwiseSign() + 1) / 2; //1 for point which is above upperQuantile otherwise 0 e.g. [1,0,0,0,1..]
            Vector<double> lowerOutliers = -1 * (original.Subtract(lowerCutoff).PointwiseSign() - 1) / 2; //1 for point which is below lowerquantile otherwise 0 e.g. [1,0,0,0,1..]
            Vector<double> result = original - upperOutliers.PointwiseMultiply(original) + upperOutliers * upperCutoff - lowerOutliers.PointwiseMultiply(original) + lowerOutliers * lowerCutoff;
            return result.ToArray();

        }



        //taken from https://stackoverflow.com/questions/8137391/percentile-calculation
        public static (double,double) Percentile(double[] sequence, double excelPercentile)
        {
            Array.Sort(sequence);
            int N = sequence.Length;
            double n = (N - 1) * excelPercentile + 1;
            double n2 = (N - 1) * (1 - excelPercentile) +1;
            // Another method: double n = (N + 1) * excelPercentile;
            if (n == 1d) return (sequence[0], sequence[N - 1]) ;
            else if (n == N) return (sequence[N - 1], sequence[0]);
            else
            {
                int k = (int)n;
                int k2 = (int)n2;

                double d = n - k;
                double d2 = n2 - k2;

                return (sequence[k - 1] + d * (sequence[k] - sequence[k - 1]), sequence[k2- 1] + d2 * (sequence[k2] - sequence[k2 - 1])) ;
            }
        }



    }
}
