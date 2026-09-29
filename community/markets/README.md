# Markets

Crypto and stocks at a glance, with their trend, for traders who watch the
same symbol in several timeframes.

- **Main**: your watchlist. Each entry is a symbol *and* a timeframe:
  `BTC 4H`, `BTC 1W`, `BTC 1M`, `SOL 1D`… each with its own sparkline. The
  change is the current candle's, as on a trading chart.
- **Crypto**: the top 20 by market cap (stablecoins and wrapped copies left
  out), then the coins you add; all in the timeframe you pick.
- **Stocks**: the indices and stocks you follow, in one timeframe.
- Pick a row for its chart: candles with volume, any timeframe (15m, 1H,
  4H, 1D, 1W, 1M), the candle under the pointer's open, high, low, close.
- **Edit** adds symbols (with search), changes timeframes, reorders, and
  pins up to three to the bar, where they show in turn.

`SUPER + M` opens it (also in the palette). Everything follows the theme.

## Where the data comes from

- Crypto candles: Binance's public market data (`data-api.binance.vision`),
  no account. A coin of the top 20 that Binance doesn't trade (or no longer
  does) shows CoinPaprika's price and 24-hour change, without a chart; a
  coin you add must trade on Binance.
- The top 20: CoinPaprika (`api.coinpaprika.com`), no account.
- Stocks: Yahoo Finance (unofficial: it may change), or Twelve Data with a
  free key (`stock_source = "twelvedata"`, `twelvedata_key`: kept in plain
  text, as any setting; its free plan takes 8 requests a minute, so stocks
  refresh one every 8 seconds). Yahoo has no 4-hour candles: hourly ones
  are joined four by four within a day.

Prices are in USD (a stock in another currency shows it). The pinned
symbols are fetched every `refresh_seconds`; the rest only while the panel
is open. Your lists are in `~/.local/state/myarch-markets/watchlist.json`.

Not investment advice; data can be delayed.
