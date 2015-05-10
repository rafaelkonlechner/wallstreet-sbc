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
        static private XcoList<ShareInformation> stockPrices;
        static private XcoQueue<string> stockPricesUpdates;
        static private XcoList<Order> orders;


        static void Main(string[] args)
        {

                try
                {
                    space = new XcoSpace(0);
                    stockPrices = space.Get<XcoList<ShareInformation>>("StockInformation", new Uri("xco://" + Environment.MachineName + ":" + 9000));
                    stockPricesUpdates = space.Get<XcoQueue<string>>("StockInformationUpdates", new Uri("xco://"+ Environment.MachineName + ":" + 9000));
                    orders = space.Get<XcoList<Order>>("Orders", new Uri("xco://" + Environment.MachineName + ":" + 9000));

                    if (args.Length > 0 && args[0].Equals("-Manual"))
                    {
                        Console.WriteLine("Type \"list\" to list all shares and set the price by typing <sharename> <price>");
                        while (true)
                        {
                            var input = Console.ReadLine();
                            if (input.Equals("list"))
                            {
                                for (int i = 0; i < stockPrices.Count; i++)
                                {
                                    ShareInformation s = stockPrices[i];
                                    Console.WriteLine(s.FirmName + "\t" + s.PricePerShare);
                                }
                            }
                            else
                            {
                                var info = input.Split(' ');
                                var stock = Utils.FindShare(stockPrices, info[0]);
                                ShareInformation s = new ShareInformation()
                                {
                                    FirmName = info[0],
                                    NoOfShares = stock.NoOfShares,
                                    PricePerShare = Double.Parse(input.Split(' ')[1])
                                };
                                Utils.ReplaceShare(stockPrices, s);
                            }
                        }
                    }
                    else
                    {

                    timer = Observable.Interval(TimeSpan.FromSeconds(2));
                    timer.Subscribe(_ => UpdateStockPrices());
                    Thread.Sleep(1000);

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
                Console.WriteLine("UPDATE stock prices: " + DateTime.Now);

                try
                {
                    for (int i = 0; i < stockPrices.Count; i++)
                    {
                            ShareInformation oldPrice = stockPrices[i,true];
                            long pendingBuyOrders = PendingOrders(oldPrice.FirmName, Order.OrderType.BUY);
                            long pendingSellOrders = PendingOrders(oldPrice.FirmName, Order.OrderType.SELL);
                            double x = ComputeNewPrice(oldPrice.PricePerShare, pendingBuyOrders, pendingSellOrders);
                            ShareInformation newPrice = new ShareInformation()
                            {
                                FirmName = oldPrice.FirmName,
                                NoOfShares = oldPrice.NoOfShares,
                                PricePerShare = x
                            };
                            Console.WriteLine("Update {0} from {1} to {2}.", newPrice.FirmName, oldPrice.PricePerShare, newPrice.PricePerShare);

                            Utils.ReplaceShare(stockPrices, newPrice);
                            stockPricesUpdates.Enqueue(newPrice.FirmName,true);
                    }

                    RandomlyUpdateASingleStock();

                }
                catch (XcoException e)
                {
                    Console.WriteLine("Could not update stock due to: " + e.Message);
                }
        }

        private static void RandomlyUpdateASingleStock()
        {
            counter++;
            if (stockPrices.Count > 0 && counter % 3 == 0)
            {
                counter = 0;
                Random rrd = new Random();
                ShareInformation oldPrice = stockPrices[rrd.Next(0, stockPrices.Count - 1), true];
                Console.WriteLine("UPDATE stock price {0} randomly: {1}", oldPrice.FirmName, DateTime.Now);

                double x = Math.Max(1, oldPrice.PricePerShare * (1 + (rrd.Next(-3, 3) / 100.0)));
                ShareInformation newPrice = new ShareInformation()
                {
                    FirmName = oldPrice.FirmName,
                    NoOfShares = oldPrice.NoOfShares,
                    PricePerShare = x
                };
                Console.WriteLine("Update {0} from {1} to {2}.", newPrice.FirmName, oldPrice.PricePerShare, newPrice.PricePerShare);

                Utils.ReplaceShare(stockPrices, newPrice);
                stockPricesUpdates.Enqueue(newPrice.FirmName);
            }
        }

        static private long PendingOrders(string stockName, Order.OrderType orderType)
        {
                long stocks = 0;

                for (int i = 0; i < orders.Count; i++)
                {
                    Order order = orders[i];
                    if (order.ShareName == stockName && (order.Status == Order.OrderStatus.OPEN || order.Status == Order.OrderStatus.PARTIAL) && order.Type == orderType)
                    {
                        stocks += order.NoOfOpenShares;
                    }
                }

                return stocks;
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
