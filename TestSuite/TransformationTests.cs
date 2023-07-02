using Microsoft.VisualStudio.TestTools.UnitTesting;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Transformations;



namespace UnitTests
{
    [TestClass]
    public class TransformationTests
    {

        [TestMethod]
        public void TestSimpleTransitions()
        {

            //        public static double[] SimpleTransition(List<double> current_state, List<double> desired_state)
            List<double> currentState = new List<double>() { 0, 0.1, 0.2, 0.1, -0.1 };
            List<double> desiredState = new List<double>() { 0.1, 0.2, 0.1, -0.1, 0 };
            bool areEqual = desiredState.SequenceEqual(transform.SimpleTransition(currentState, desiredState));
            Assert.IsTrue(areEqual);
            // Assert.IsTrue(transform.SimpleTransformation(currentState, desiredState) == desiredState);

        }

        [TestMethod]
        public void TestLinearTransitions()
        {

            List<double> currentState = new List<double>() { 0, 0.1, 0.2, 0.1, -0.1 };
            List<double> desiredState = new List<double>() { 0.1, 0.2, 0.1, -0.1, 0 };
            Matrix<double> result = transform.LinearTransition(currentState, desiredState, 5);
            bool areEqual = desiredState.SequenceEqual(result.Row(4).AsArray().ToList());
            Assert.IsTrue(areEqual);
        }

        [TestMethod]
        public void TestSpeedBumpTransition()
        {

            List<double> currentState = new List<double>() { 0.099, 0.1, 0.2, 0.1, -0.1 };
            List<double> desiredState = new List<double>() { 0.1, 0.2, 0.1, -0.1, 0 };
            List<double> expectedState = new List<double>() { 0.099, 0.2, 0.1, -0.1, 0 };
            double[] result = transform.SpeedbumpTransition(currentState, desiredState, 0.05);
            bool areEqual = expectedState.SequenceEqual(result.ToList());
            Assert.IsTrue(areEqual);
        }

        [TestMethod]
        public void TestRank()
        {
            Dictionary<string, double> input = new Dictionary<string, double>() { { "BTCUSDT", 100 }, { "ETHUSDT", 80 }, { "LTCUSDT", 60 } };
            var output = transform.Rank(input);
            Dictionary<string, double> expected = new Dictionary<string, double>() { { "BTCUSDT", 0 }, { "ETHUSDT", 0.5 }, { "LTCUSDT", 1 } };
            bool areEqual = output.SequenceEqual(expected);
            Assert.IsTrue(areEqual);

        }

        [TestMethod]
        public void TestNeutralization()
        {
            List<double> input = new List<double>() { 0.2, 0.2, 0.2, 0.2, -0.2 };
            var output = transform.neutralization(input);
            var expected = new double[5] { 0.125, 0.125, 0.125, 0.125, -0.5 };
            bool areEqual = output.SequenceEqual(expected);
            Assert.IsTrue(areEqual);
            //public static Dictionary<string, double> rank(Dictionary<string, double> input, bool ascending

        }



        [TestMethod]
        public void TestNeutralizationDict()

        {
            Dictionary<string, double> desiredState = new Dictionary<string, double>() { { "BTCUSDT", 0.5 } };


            Dictionary<string, double> expected = new Dictionary<string, double>() { { "BTCUSDT", 0 } };

            var output = transform.neutralization(desiredState);
            bool areEqual = output.SequenceEqual(expected);
            Assert.IsTrue(areEqual);
            //public static Dictionary<string, double> rank(Dictionary<string, double> input, bool ascending

        }

        [TestMethod]
        public void TestPercentile()
        {

            var temp = new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100 };
            var percentiles = transform.Percentile(temp, 0.05);
            Assert.IsTrue(percentiles.Item1 == 5);
            Assert.IsTrue(percentiles.Item2 == 95);

        }

        [TestMethod]
        public void TestWinsorize()
        {

            var temp = new double[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100 };
            var winsorized = transform.Winsorize(temp, 0.05);
            Assert.IsTrue(winsorized.Min() == 5);
            Assert.IsTrue(winsorized.Max() == 95);
        }



    }
}