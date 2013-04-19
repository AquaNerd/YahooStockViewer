using System;
using System.Collections.Generic;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Microsoft.ComplexEventProcessing;
using Microsoft.ComplexEventProcessing.Linq;

namespace SimpleSI {
    public class YahooQuote {
        public DateTime LastUpdate { get; set; }
        public decimal LastTradePrice { get; set; }
        public string Symbol { get; set; }

        public override string ToString() {
            return string.Format("{0} - {1} - {2}", Symbol, LastTradePrice, LastUpdate);
        }
    }
    static class SimpleSI {
        static void Main() {
            using (Server server = Server.Create("Default")) {
                Application application = server.CreateApplication("app");

                IQStreamable<YahooQuote> inputStream = null;

                Console.WriteLine("Press L for Live or H for Historic Data");
                ConsoleKeyInfo key = Console.ReadKey();
                Console.WriteLine();

                if (key.Key == ConsoleKey.L) {
                    inputStream = CreateStream(application, true);
                } else if (key.Key == ConsoleKey.H) {
                    inputStream = CreateStream(application, false);
                } else {
                    Console.WriteLine("Invalid Key");
                    return;
                }

                decimal threshold = new decimal(14.00);

                var alteredForward = inputStream.AlterEventStartTime(s => s.StartTime.AddSeconds(1));

                var crossedThreshold = from evt in inputStream
                                       from prev in alteredForward
                                       where prev.LastTradePrice < threshold && evt.LastTradePrice > threshold
                                       select new {
                                           LastUpdate = evt.LastUpdate,
                                           Low = prev.LastTradePrice,
                                           High = evt.LastTradePrice
                                       };

                foreach (var outputSample in crossedThreshold.ToEnumerable()) {
                    Console.WriteLine(outputSample);
                }

                Console.WriteLine("Done. Press ENTER to terminate");
                Console.ReadLine();
            }
        }

        private static readonly YahooQuote[] HistoricData = new YahooQuote[] {
            new YahooQuote { Symbol = "DELL", LastTradePrice = (decimal)12.21, LastUpdate = new DateTime(2013, 4, 5, 11, 14, 0, DateTimeKind.Local) }, 
            new YahooQuote { Symbol = "DELL", LastTradePrice = (decimal)14.21, LastUpdate = new DateTime(2013, 4, 5, 11, 15, 0, DateTimeKind.Local) },
            new YahooQuote { Symbol = "DELL", LastTradePrice = (decimal)13.21, LastUpdate = new DateTime(2013, 4, 5, 11, 16, 0, DateTimeKind.Local) }
        };

        private static IObservable<YahooQuote> SimulateLiveData() {
            return ToObservableInterval(HistoricData, TimeSpan.FromMilliseconds(1000), Scheduler.ThreadPool);
        }

        private static IObservable<T> ToObservableInterval<T>(IEnumerable<T> source, TimeSpan period, IScheduler scheduler) {
            return Observable.Using(
                () => source.GetEnumerator(),
                it => Observable.Generate(
                    default(object),
                    _ => it.MoveNext(),
                    _ => _,
                    _ => {
                        Console.WriteLine("Input {0}", it.Current);
                        return it.Current;
                    },
                    _ => period, scheduler));
        }

        static IQStreamable<YahooQuote> CreateStream(Application application, bool isRealTime) {
            DateTimeOffset startTime = new DateTime(2013, 4, 5, 11, 13, 0, DateTimeKind.Local);

            if (isRealTime) {
                return
                    application.DefineObservable(() => SimulateLiveData()).ToPointStreamable(
                        r => PointEvent<YahooQuote>.CreateInsert(startTime.AddSeconds(1), r),
                        AdvanceTimeSettings.StrictlyIncreasingStartTime);
            }
            
            return
                application.DefineEnumerable(() => HistoricData).ToPointStreamable(
                    r => PointEvent<YahooQuote>.CreateInsert(startTime.AddSeconds(1), r),
                    AdvanceTimeSettings.StrictlyIncreasingStartTime);
        }
    }
}
