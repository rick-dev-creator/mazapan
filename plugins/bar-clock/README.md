# Clock

The day, date and time in the middle of the bar, in the system's language:
"Sat 28 Sep" and the time as the language writes it (24-hour in Spanish,
12-hour in US English). It changes with the minute, not every second.

A click opens a card with the time to the second, the full date and the
month. ‹ and ›, the arrow keys or the mouse wheel go to other months;
"today" comes back. Weeks start on the day your language starts them.

`format`, empty by default, takes a Qt date format of your own instead,
e.g. `dddd d · HH:mm` (Qt's formatDateTime). `font_size` is the size of
the text (9.5 points).
