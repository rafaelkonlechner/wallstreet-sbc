﻿using SharedFeatures.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using XcoSpaces;
using XcoSpaces.Collections;
using XcoSpaces.Exceptions;

namespace SpaceServer
{
    class Server
    {
        static void Main(string[] args)
        {
            using (XcoSpace space = new XcoSpace(9000))
            {
                var qRequests = new XcoQueue<Request>();
                space.Add(qRequests, "RequestQ");
                qRequests.AddNotificationForEntryEnqueued((s, r) => Console.WriteLine("New request queued for {0}, publishing {1} shares for {2} Euros.", r.FirmName, r.Shares, r.PricePerShare));

                var firmDepots = new XcoDictionary<string, FirmDepot>();
                space.Add(firmDepots, "FirmDepots");
                firmDepots.AddNotificationForEntryAdd((s, k, r) => Console.WriteLine("Depot entry created/overwritten for {0}, publishing/adding {1} shares.", k, r.OwnedShares));

                var stockInformation = new XcoDictionary<string, Tuple<int, double>>();
                space.Add(stockInformation, "StockInformation");
                stockInformation.AddNotificationForEntryAdd((s, k, r) => Console.WriteLine("New info for {0}: price per share {1:C}, at a volume of {2} shares.", k, r.Item2, r.Item1));

                var investorRegistrations = new XcoQueue<Registration>();
                space.Add(investorRegistrations, "InvestorRegistrations");
                investorRegistrations.AddNotificationForEntryEnqueued((s, r) => Console.WriteLine("New registration queued for Email address {0} and budget {1}.", r.Email, r.Budget));

                var investorDepots = new XcoDictionary<string, InvestorDepot>();
                space.Add(investorDepots, "InvestorDepots");
                investorDepots.AddNotificationForEntryAdd((s, k, r) => Console.WriteLine("New investor depot entry for Email address {0} (Budget: {1}).", k, r.Budget));

                var orders = new XcoDictionary<string, Order>();
                space.Add(orders, "Orders");
                orders.AddNotificationForEntryAdd((s, k, v) => Console.WriteLine("New {0} order for Investor {1}, intending to buy {2} shares from {3}.", v.Type, v.InvestorId, v.ShareName, v.TotalNoOfShares));

                var transactions = new XcoList<Transaction>();
                space.Add(transactions, "Transactions");
                transactions.AddNotificationForEntryAdd((s, t, i) => Console.WriteLine("New transaction between {1} and {2}, transfering {2} shares for {3} Euros per share.", t.SellerId, t.BuyerId, t.NoOfSharesSold, t.PricePerShare));

                var orderCoordinator = new XcoDictionary<string, string>();
                space.Add(orderCoordinator, "OrderCoordinator");
                orderCoordinator.AddNotificationForEntryAdd((s, k, v) => Console.WriteLine("New order with ID {0} in coordination container.", v));

                Console.WriteLine("Press enter to quit ...");
                Console.ReadLine();
                space.Remove(qRequests);
                space.Remove(firmDepots);
                space.Remove(stockInformation);
                space.Remove(investorRegistrations);
                space.Remove(investorDepots);
                space.Remove(orders);
                space.Remove(transactions);
                space.Close();
            }
        }
    }
}
