#!/usr/bin/env python3
"""Measure a window animation frame by frame.

Runs a Hyprland dispatch, then grabs a 1px-tall strip of the screen as fast
as grim allows (~7ms) and tracks the left edge of the focused window's
border (the theme's accent color). Prints the time to settle and the
overshoot, so motion settings can be compared by numbers instead of by eye.

    motion-probe.py 'hl.dsp.layout("swapcol r")' --color 7847eb --row 300
"""
import argparse
import subprocess
import time


def grab(row, width):
    ppm = subprocess.run(
        ["grim", "-g", f"0,{row} {width}x1", "-t", "ppm", "-"],
        capture_output=True, check=True,
    ).stdout
    # P6\n<w> <h>\n<max>\n<rgb bytes>
    header_end = 0
    for _ in range(3):
        header_end = ppm.index(b"\n", header_end) + 1
    return ppm[header_end:]


def edge(pixels, color, tol=40):
    for x in range(len(pixels) // 3):
        r, g, b = pixels[3 * x : 3 * x + 3]
        if abs(r - color[0]) + abs(g - color[1]) + abs(b - color[2]) < tol:
            return x
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("dispatch", help="Lua dispatcher expression for hyprctl dispatch")
    ap.add_argument("--color", default="7847eb", help="active border color, rrggbb")
    ap.add_argument("--row", type=int, default=300)
    ap.add_argument("--width", type=int, default=1010)
    ap.add_argument("--seconds", type=float, default=1.5)
    args = ap.parse_args()
    color = bytes.fromhex(args.color)

    start_x = edge(grab(args.row, args.width), color)
    subprocess.run(["hyprctl", "dispatch", args.dispatch], capture_output=True, check=True)
    t0 = time.monotonic()
    samples = []
    while (t := time.monotonic() - t0) < args.seconds:
        samples.append((t * 1000, edge(grab(args.row, args.width), color)))

    xs = [x for _, x in samples if x is not None]
    if not xs or start_x is None:
        print("border not found; wrong --color or --row?")
        return
    end_x = xs[-1]
    travel = end_x - start_x
    if travel == 0:
        print(f"no movement (x={end_x})")
        return
    # Overshoot: how far past the target it went, as % of the travel.
    past = max((x - end_x) * (1 if travel > 0 else -1) for x in xs)
    settle = next(
        (t for t, x in reversed(samples) if x is not None and abs(x - end_x) > 2),
        0.0,
    )
    print(f"frames={len(samples)} x {start_x}->{end_x} travel={travel}px")
    print(f"settled_ms={settle:.0f} overshoot={100 * past / abs(travel):.1f}%")
    trace = " ".join(f"{t:.0f}:{x}" for t, x in samples[:: max(1, len(samples) // 24)])
    print(f"trace(ms:x) {trace}")


if __name__ == "__main__":
    main()
