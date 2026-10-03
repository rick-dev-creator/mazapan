# Capture

One way to take the screen (`Print`, "Capture" on the palette's first
screen). Every screen freezes under a dim veil: drag a region, or click a
window, or empty screen to take it all. Then:

- **Copy** (`c`) puts the picture on the clipboard.
- **Save** (`s`) keeps it in `~/Pictures/Screenshots` (`pictures`), as
  "Screenshot" and the date and time.
- **Text** (`t`) reads the text in it (OCR) and copies that.
- **QR code** (`q`) copies what a QR code in it holds, as a secret: it's
  never shown, and the clipboard history doesn't keep it (it may be a
  password or a Wi-Fi key).
- **Annotate** (`a`): pen, arrow, box or marker, in a few colors; Undo
  (or Ctrl+Z) takes back the last one, Done (or ↵) goes back to the
  actions. What you draw is in what you copy or save.
- **Record** (`r`) records that region as a video.

↵ copies and saves at once; Esc leaves. Pointing at an action shows the
command it runs. A notification says what happened (with the picture,
when it was saved).

While it records, a red dot and the time sit in the middle of the bar. A
click on it, `Print` again or "Stop the screen recording" in the palette
stops it, and it's kept in `~/Videos/Recordings` (`videos`). Sound isn't
recorded unless you ask (`audio`: the default output). Notifications are
held back while a picture is taken or a video records, so they're never
in it.

Text is read in the system's language and English, those whose Tesseract
data is installed (`tesseract-data-spa`, `-fra`…; the installer adds the
language's when it's online). The frozen screens stay
in a private folder and go as soon as they're done with. With grim,
wl-clipboard, Tesseract, zbar and wf-recorder.
