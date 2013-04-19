using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;

using StockEngineLibrary.Helper;
using StockEngineLibrary.Payload;

namespace YahooStockViewer.ViewModel {
    public class YahooViewModel : DependencyObject {
        private readonly DispatcherTimer timer = new DispatcherTimer(DispatcherPriority.Background);

        public ObservableCollection<ObservableQuote> Quotes { get; set; }
        
        public YahooViewModel() {
            Quotes = new ObservableCollection<ObservableQuote>();

            Quotes.Add(new ObservableQuote("AAPL"));
            Quotes.Add(new ObservableQuote("MSFT"));
            Quotes.Add(new ObservableQuote("DELL"));

            YahooStockEngine.Fetch(Quotes);

            timer.Interval = new TimeSpan(0, 0, 5);
            timer.Tick += (o, e) => YahooStockEngine.Fetch(Quotes);

            timer.Start();
        }
    }
}
