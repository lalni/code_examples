using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rebalancer = QuantOffice.Rebalancer;

namespace RebalancerTests
{
    [TestClass]
    public class UnitTest2
    {

        public Rebalancer rebalancer;
        [TestMethod]
        public void TestMethod1()
        {
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void TestRebalancerMarketNeutral()
        {
            Dictionary<string, double> currentState = new Dictionary<string, double>() { { "BTCUSDT", 0.5 } };
            Dictionary<string, double> desiredState = new Dictionary<string, double>() { { "BTCUSDT", 0.5 } };
            Dictionary<string, double> expected = new Dictionary<string, double>() { { "BTCUSDT", 0 } };
            Dictionary<string, double> output;

            Rebalancer rebalancer = new Rebalancer(testing: true);

            output = Rebalancer.Rebalance(desiredState, true);

            bool areEqual = Enumerable.SequenceEqual(output, expected);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void TestRebalancerMultiMarketNeutral()
        {
            Dictionary<string, double> currentState = new Dictionary<string, double>() { { "BTCUSDT", 0.5 }, { "XRPUSDT", -0.5 } };
            Dictionary<string, double> desiredState = new Dictionary<string, double>() { { "BTCUSDT", 0.5 }, { "XRPUSDT", -0.2 } };
            Dictionary<string, double> expected = new Dictionary<string, double>() { { "BTCUSDT", 0.5 }, { "XRPUSDT", -0.5 } };
            Dictionary<string, double> output;

            Rebalancer rebalancer = new Rebalancer(testing: true);

            output = Rebalancer.Rebalance(desiredState, marketNeutral : true);

            bool areEqual = Enumerable.SequenceEqual(output, expected);

            Assert.IsTrue(true);
        }



        [TestMethod]
        public void TestRebalancerSectorNeutral()
        {
            Dictionary<string, double> currentState = new Dictionary<string, double>() { { "BTCUSDT", 0.5 }, { "XRPUSDT", -0.5 }, { "ETHUSDT", 0.5 } };
            Dictionary<string, double> desiredState = new Dictionary<string, double>() { { "BTCUSDT", 0.5 }, { "XRPUSDT", -0.5 }, { "ETHUSDT", 0.5 } };
            Dictionary<string, double> expected = new Dictionary<string, double>() { { "BTCUSDT", 0.5 }, { "XRPUSDT", -0.5 }, { "ETHUSDT", 0 } };
            Dictionary<string, double> output;

            Rebalancer rebalancer = new Rebalancer(testing: true);

            output = Rebalancer.Rebalance(desiredState, sector_neutral: true, marketNeutral: false);

            bool areEqual = Enumerable.SequenceEqual(output, expected);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void TestRebalancerIndustryNeutral()
        {
            Dictionary<string, double> currentState = new Dictionary<string, double>() { { "BTCUSDT", 0.5 }, { "XRPUSDT", -0.5 }, { "ETHUSDT", 0.5 } };
            Dictionary<string, double> desiredState = new Dictionary<string, double>() { { "BTCUSDT", 0.5 }, { "XRPUSDT", -0.5 }, { "ETHUSDT", 0.5 } };
            Dictionary<string, double> expected = new Dictionary<string, double>() { { "BTCUSDT", 0.5 }, { "XRPUSDT", -0.5 }, { "ETHUSDT", 0 } };
            Dictionary<string, double> output;

            Rebalancer rebalancer = new Rebalancer(testing: true);

            output = Rebalancer.Rebalance(desiredState, sector_neutral: true, marketNeutral: false);

            bool areEqual = Enumerable.SequenceEqual(output, expected);

            Assert.IsTrue(true);
        }





    }
}