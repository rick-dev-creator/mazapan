# Weather

The weather now in the middle of the bar: an icon and the temperature. A
click opens the details: the conditions, what it feels like, the day's
high and low, humidity, wind, the chance of rain, sunrise and sunset; then
the next hours and the next days. A click outside, Esc or the widget again
closes them.

Where (`location`): `auto` finds you by your public IP, which asks
ipinfo.io; a city name is looked up with Open-Meteo's geocoding; or give
coordinates as "lat,lon", and nothing but the forecast is asked. The
place is found once; the forecast comes from Open-Meteo, with no account
and no key, every 15 minutes (`refresh_minutes`), once for every bar.

`units`: `metric` (°C, km/h) or `imperial` (°F, mph). Day names and
times are in the system's language.
