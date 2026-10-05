#!/bin/sh
# Records one Learn lesson in the dev VM's session (vm/learn-record runs
# it): its practice windows, then the moves at the seconds lessons.json.tmpl
# says (`at`, counted from the clip's start), recorded with wf-recorder and
# made an animated WebP in plugins/learn/media (on the shared repository).
#   learn-record.sh ID
set -eu
id=$1
repo=/home/arch/my-arch
out=$repo/plugins/learn/media
practice=$HOME/.local/share/mazapan/bin/learn-practice
tmp=$(mktemp -d)
mkdir -p "$out"

d() { hyprctl dispatch "$1" >/dev/null; }
e() { hyprctl eval "$1" >/dev/null; }
ipc() { qs ipc -c mazapan call "$@" >/dev/null; }

# What each lesson does, and when (seconds from the clip's start); its
# length; the practice windows it takes (as the panel opens them). In
# an argument, ~ is a space.
case $id in
focus) len=7; win="3"; script='1.5 d hl.dsp.focus({direction="right"})
3.5 d hl.dsp.focus({direction="right"})
5.5 d hl.dsp.focus({direction="left"})' ;;
move) len=7; win="3"; script='1.5 d hl.dsp.layout("swapcol~r")
3.5 d hl.dsp.layout("swapcol~r")
5.5 d hl.dsp.layout("swapcol~l")' ;;
width) len=7.5; win="3"; script='1.5 e mazapan_columns.grow()
3.5 e mazapan_columns.shrink()
5.5 d hl.dsp.layout("colresize~+conf")' ;;
equal) len=5; win="3 uneven"; script='2 e mazapan_columns.equal()' ;;
max) len=6; win="3"; script='1.5 e mazapan_columns.maximize()
4 e mazapan_columns.maximize()' ;;
workspaces) len=7.5; win="1"; script='1.5 d hl.dsp.focus({workspace="r+1"})
3.5 d hl.dsp.focus({workspace="r-1"})
5.5 d hl.dsp.focus({workspace="r+2"})' ;;
overview) len=6; win="3"; script='1.5 ipc mazapan~panel~overview
4.5 ipc mazapan~panel~overview' ;;
palette) len=8; win=""; script='1 ipc mazapan~panel~palette
2.5 type term
5.5 key Return' ;;
keys) len=6; win=""; script='1.5 ipc keys~toggle
4.5 ipc keys~toggle' ;;
*) echo "no lesson $id" >&2; exit 2 ;;
esac

if [ -n "$win" ]; then sh "$practice" open $win; fi
# No pointer in the recording (it jumps to each window focused).
hyprctl eval 'hl.config({ cursor = { invisible = true } })' >/dev/null
sleep 1
# The clip starts a second into the recording (wf-recorder's own start).
wf-recorder -r 15 -f "$tmp/$id.mp4" >/dev/null 2>&1 &
rec=$!
sleep 1
start=$(date +%s.%N)
echo "$script" | while read -r at kind arg; do
  now=$(date +%s.%N)
  wait=$(echo "$start + $at - $now" | bc)
  case $wait in -*) ;; *) sleep "$wait" ;; esac
  arg=$(echo "$arg" | tr '~' ' ')
  case $kind in
  d) d "$arg" ;;
  e) e "$arg" ;;
  ipc) ipc $arg ;;
  type) wtype -d 120 "$arg" ;;
  key) wtype -k "$arg" ;;
  esac
done
now=$(date +%s.%N)
rest=$(echo "$start + $len - $now" | bc)
case $rest in -*) ;; *) sleep "$rest" ;; esac
kill -INT "$rec"
wait "$rec" 2>/dev/null || true
hyprctl eval 'hl.config({ cursor = { invisible = false } })' >/dev/null

# Tidy up what the lesson opened.
case $id in
palette) hyprctl dispatch 'hl.dsp.window.close()' >/dev/null ;;
esac
if [ -n "$win" ]; then sh "$practice" close; fi

ffmpeg -loglevel error -y -ss 1 -t "$len" -i "$tmp/$id.mp4" \
  -vf "fps=12,scale=960:-2:flags=lanczos" -c:v libwebp_anim -lossless 0 -quality 62 -compression_level 6 -loop 0 \
  "$out/$id.webp"
rm -rf "$tmp"
ls -la "$out/$id.webp"
