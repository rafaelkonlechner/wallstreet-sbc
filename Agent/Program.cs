using SharedFeatures.Model;
using System;
using System.Threading;
using System.Reactive.Linq;
using XcoSpaces;
using XcoSpaces.Collections;
using XcoSpaces.Exceptions;

namespace Agent
{
    class Program
    {
        static private IObservable<long> timer;
        static int counter = 0;
        static private XcoSpace space;
        static private XcoDictionary<string, Tuple<int, double>> stockPrices;
        static private XcoDictionary<string, FirmDepot> firmDepots;
        static private XcoDictionary<string, Order> orders;


        static void Main(string[] args)
        {

                try
                {
                    space = new XcoSpace(0);
                    stockPrices = space.Get<XcoDictionary<string, Tuple<int, double>>>("StockInformation", new Uri("xco://" + Environment.MachineName + ":" + 9000));
                    firmDepots = space.Get<XcoDictionary<string, FirmDepot>>("FirmDepots", new Uri("xco://" + Environment.MachineName + ":" + 9000));
                    orders = space.Get<XcoDictionary<string, Order>>("Orders", new Uri("xco://" + Environment.MachineName + ":" + 9000));

                    if (args.Length > 0 && args[0].Equals("-Manual"))
                    {
                        Console.WriteLine("Type \"list\" to list all shares and set the price by typing <sharename> <price>");
                        while (true)
                        {
                            var input = Console.ReadLine();
                            if (input.Equals("list"))
                            {
                                foreach (string key in stockPrices.Keys)
                                {
                                    Console.WriteLine(key + "\t" + stockPrices[key].Item2);
                                }
                            }
                            else
                            {
                                var info = input.Split(' ');
                                var stock = stockPrices[info[0]];
                                stockPrices[info[0]] = new Tuple<int, double>(stock.Item1, Double.Parse(input.Split(' ')[1]));
                            }
                        }
                    }
                    else
                    {

                    timer = Observable.Interval(TimeSpan.FromSeconds(2));
                    timer.Subscribe(_ => UpdateStockPrices());
                    Thread.Sleep(1000);

                    Random random = new Random();
                    while (true)
                    {
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (XcoException)
            {
                Console.WriteLine("Unable to reach server.\nPress enter to exit.");
                Console.ReadLine();
                if (space != null && space.IsOpen) { space.Close(); }
            }
        }

        static void UpdateStockPrices()
        {
            using (XcoTransaction tx = space.BeginTransaction())
            {
                Console.WriteLine("UPDATE stock prices: " + DateTime.Now);

                foreach (string firmKey in firmDepots.Keys)
                {

                    if (stockPrices.ContainsKey(firmKey))
                    {
                        Tuple<int, double> oldPrice = stockPrices[firmKey];
                        long pendingBuyOrders = PendingOrders(firmKey, Order.OrderType.BUY);
                        long pendingSellOrders = PendingOrders(firmKey, Order.OrderType.SELL);
                        double x = ComputeNewPrice(oldPrice.Item2, pendingBuyOrders, pendingSellOrders);
                        Tuple<int, double> newPrice = new Tuple<int, double>(oldPrice.Item1, x);
                        Console.WriteLine("Update {0} from {1} to {2}.", firmKey, oldPrice.Item2, newPrice.Item2);
                        stockPrices[firmKey] = newPrice;
                    }
                }

                RandomlyUpdateASingleStock();

                tx.Commit();
            }

        }

        private static void RandomlyUpdateASingleStock()
        {
            counter++;
            if (firmDepots.Keys.Length > 0 && counter % 3 == 0)
            {
                counter = 0;
                Random rrd = new Random();
                string firmKey = firmDepots.Keys[rrd.Next(0, firmDepots.Count - 1)];
                Console.WriteLine("UPDATE stock price {0} randomly: {1}", firmKey, DateTime.Now);
                Tuple<int, double> oldPrice = stockPrices[firmKey];
                double x = Math.Max(1, oldPrice.Item2 * (1 + (rrd.Next(-3, 3) / 100.0)));
                Tuple<int, double> newPrice = new Tuple<int, double>(oldPrice.Item1, x);
                Console.WriteLine("Update {0} randomly from {1} to {2}.", firmKey, oldPrice.Item2, newPrice.Item2);
                stockPrices[firmKey] = newPrice;
            }
        }

        static private long PendingOrders(string stockName, Order.OrderType orderType)
        {
            using (XcoTransaction tx = space.BeginTransaction())
            {
                long stocks = 0;
                foreach (string orderId in orders.Keys)
                {
                    Order order = orders[orderId];
                    if (order.ShareName == stockName && order.Status == Order.OrderStatus.OPEN && order.Type == orderType)
                    {
                        stocks += order.NoOfOpenShares;
                    }
                }

                tx.Commit();
                return stocks;
            }
        }

        static double ComputeNewPrice(double oldPrice, long pendingBuyOrders, long pendingSellOrders)
        {
            double d = Math.Max(1, pendingBuyOrders + pendingSellOrders);
            double n = (double)(pendingBuyOrders - pendingSellOrders);
            double x = (1 + ((n / d) * (1.0 / 16.0)));

            return Math.Max(1, oldPrice * x);
        }
    }
}
