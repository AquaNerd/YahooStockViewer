using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Xml.Linq;
using Microsoft.ComplexEventProcessing;
using Microsoft.ComplexEventProcessing.Linq;


using StockEngineLibrary.Helper;
using StockEngineLibrary.Payload;

namespace StreamSI {
    public class YahooQuote {
        public DateTime LastUpdate { get; set; }
        public decimal? LastTradePrice { get; set; }
        public string Symbol { get; set; }

        public override string ToString() {
            return string.Format("{0} - {1} - {2}", Symbol, LastTradePrice, LastUpdate.ToLongTimeString());
        }

        public YahooQuote(string symbol) {
            Symbol = symbol;
        }

        public YahooQuote() {
            Symbol = "";
        }
    }

    class Program {
        private const string BASE_URL = "http://query.yahooapis.com/v1/public/yql?q=select%20*%20from%20yahoo.finance.quotes%20where%20symbol%20in%20({0})&env=store%3A%2F%2Fdatatables.org%2Falltableswithkeys";

        public static void Fetch(ObservableCollection<YahooQuote> quotes) {
            string symbolList = String.Join("%2C", quotes.Select(w => "%22" + w.Symbol + "%22").ToArray());
            string url = string.Format(BASE_URL, symbolList);

            XDocument doc = XDocument.Load(url);
            Parse(quotes, doc);
        }

        private static void Parse(ObservableCollection<YahooQuote> quotes, XDocument doc) {
            XElement results = doc.Root.Element("results");

            if (!results.IsEmpty) {
                foreach (YahooQuote quote in quotes) {
                    XElement q = results.Elements("quote").First(w => w.Attribute("symbol").Value == quote.Symbol);

                    quote.LastTradePrice = GetDecimal(q.Element("LastTradePriceOnly").Value);
                    quote.LastUpdate = DateTime.Now;
                }
            } else {
                Console.WriteLine("No Response from Yahoo");
            }
        }

        private static decimal? GetDecimal(String input) {
            if (input == null) {
                return null;
            }

            input = input.Replace("%", "");

            decimal value;

            if (Decimal.TryParse(input, out value)) {
                return value;
            }

            return null;
        }

        private static DateTime? GetDateTime(string input) {
            if (input == null) {
                return null;
            }

            DateTime value;

            if (DateTime.TryParse(input, out value)) {
                return value;
            }

            return null;
        }

        private static readonly ObservableCollection<YahooQuote> inputQuote = new ObservableCollection<YahooQuote> {
            new YahooQuote("AAPL"),
            new YahooQuote("DELL")
        };

        static void Main(string[] args) {
            while (inputQuote.FirstOrDefault().LastTradePrice == null) {
                Fetch(inputQuote);
            }

            
            using (Server server = Server.Create("default")) {
                Application application = server.CreateApplication("app");

                IQStreamable<YahooQuote> inputStream = null;

                inputStream = application.DefineObservable(() => ToObservableInterval(inputQuote, TimeSpan.FromMilliseconds(1000), Scheduler.ThreadPool)).ToPointStreamable(
                    r => PointEvent<YahooQuote>.CreateInsert(DateTimeOffset.Now, r),
                    AdvanceTimeSettings.StrictlyIncreasingStartTime);

                var myQuery = from evt in inputStream
                                       select evt;

                foreach (var outputSample in myQuery.ToEnumerable()) {
                    Console.WriteLine(outputSample);
                }

                Console.WriteLine("Done. Press ENTER to terminate.");
                Console.ReadLine();
            }
        }

        private static IObservable<T> ToObservableInterval<T>(IEnumerable<T> source, TimeSpan period, IScheduler scheduler) {
            return Observable.Using(
                () => source.GetEnumerator(),
                it => Observable.Generate(
                    default(object),
                    _ => it.MoveNext(),
                    _ => _,
                    _ => it.Current,
                    _ => period, scheduler));
        }
    }
}
