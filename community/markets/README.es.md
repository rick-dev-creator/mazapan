# Mercados

Crypto y acciones de un vistazo, con su tendencia, para quien sigue el
mismo símbolo en varias temporalidades.

- **Principal**: tu lista. Cada entrada es un símbolo *y* una temporalidad:
  `BTC 4H`, `BTC 1W`, `BTC 1M`, `SOL 1D`… cada una con su minigráfico. El
  cambio es el de la vela actual, como en un gráfico de trading.
- **Crypto**: el top 20 por capitalización (sin stablecoins ni copias
  envueltas), luego las monedas que añadas; todo en la temporalidad que
  elijas.
- **Acciones**: los índices y acciones que sigues, en una temporalidad.
- Elige una fila para ver su gráfico: velas con volumen, en cualquier
  temporalidad (15m, 1H, 4H, 1D, 1W, 1M), y apertura, máximo, mínimo y
  cierre de la vela bajo el puntero.
- **Editar** añade símbolos (con búsqueda), cambia temporalidades, reordena
  y fija hasta tres en la barra, donde se muestran por turnos.

`SUPER + M` lo abre (también en la paleta). Todo sigue el tema.

## De dónde salen los datos

- Velas de crypto: los datos públicos de Binance
  (`data-api.binance.vision`), sin cuenta. Una moneda del top 20 que
  Binance no opera (o dejó de operar) muestra el precio y el cambio de 24
  horas de CoinPaprika, sin gráfico; una moneda que añadas tiene que
  operarse en Binance.
- El top 20: CoinPaprika (`api.coinpaprika.com`), sin cuenta.
- Acciones: Yahoo Finance (no oficial: puede cambiar), o Twelve Data con
  una clave gratuita (`stock_source = "twelvedata"`, `twelvedata_key`: se
  guarda en texto plano, como cualquier ajuste; su plan gratuito admite 8
  peticiones por minuto, así que las acciones se actualizan una cada 8
  segundos). Yahoo no tiene velas de 4 horas: se juntan las horarias de
  cuatro en cuatro dentro de cada día.

Los precios están en USD (una acción en otra moneda la indica). Los
símbolos fijados se actualizan cada `refresh_seconds`; el resto, solo con
el panel abierto. Tus listas están en
`~/.local/state/myarch-markets/watchlist.json`.

No es asesoría de inversión; los datos pueden llegar con retraso.
