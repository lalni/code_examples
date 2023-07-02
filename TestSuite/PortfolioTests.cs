using Microsoft.VisualStudio.TestTools.UnitTesting;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;



namespace UnitTests
{
    [TestClass]
    public class PortfolioTests
    {

        [TestMethod]
        public void TestRecalculateWeights()
        {
            Dictionary<string, double> desiredState = new Dictionary<string, double>() { { "BTCUSDT", 0.5 }, { "XRPUSDT", -0.2 }, { "ETHUSDT", 0.4 } };

            PortfolioWeights portfolioWeights = PortfolioExecutor.RecalculateWeights(desiredState);

            double portoflioWeightSum = portfolioWeights.values.Sum(x => Math.Abs(x));

            Assert.IsTrue(portoflioWeightSum == 1.0);
        }


        [TestMethod]
        public void TestRecalculateWeightsZerosInput()
        {
            Dictionary<string, double> desiredState = new Dictionary<string, double>() { { "BTCUSDT", 0.0 }, { "XRPUSDT", 0.0 }, { "ETHUSDT", 0.0 }, { "LTCUSDT", 0.0 }, { "1INCHUSDT", 0.0 }, };

            PortfolioWeights portfolioWeights = PortfolioExecutor.RecalculateWeights(desiredState);

            double portoflioWeightSum = portfolioWeights.values.Sum(x => Math.Abs(x));

            Assert.IsTrue(portoflioWeightSum == 0.0);
        }

    }
}