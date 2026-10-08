#!/usr/bin/env python3
# The agent, as a character: Mazapan's mazapán (the same bitten one as the
# icon, make-icon.py) with a face, little arms and feet, in 80s pixel art.
# One frame is 48×48; each state is a row of frames in one sprite sheet,
# played at its own speed:
#   idle    breathing, a blink now and then (waiting for what you ask)
#   listen  eyes wide, the crumbs jumping (hearing your voice)
#   think   looking up, three dots coming
#   work    leaning in, sparks flying (preparing a change)
#   wait    looking at you, a foot tapping (your approval)
#   happy   a jump, sparkles (done)
#   shrug   arms out (you said no)
#   read    little reading glasses (only reading: about something not yours)
#   sleep   Zzz (every account at its limit)
#   dizzy   spiral eyes, stars (something failed)
# Writes plugins/agent/media/mazapan-agent.png and mazapan-agent.json
# (each state's row, frames and speed), with only Python's own library.
#   python3 assets/make-mascot.py [--preview DIR]   (DIR: each state as a big PNG)
import json, math, os, struct, sys, zlib

here = os.path.dirname(os.path.abspath(__file__))
F = 48                     # a frame's side
N = 40                     # the mazapán's own grid (as the icon)

# --- the mazapán: the icon's shape, squashed or stretched as it moves ---------------

def mazapan(rx=15.2, ry=10.5, H=7.0, bite=True):
    cx, cy = 20, 15.5
    BX, BY, BR = 34.5, 6.0, 5.8

    def parts(px, py):
        top = ((px - cx) / rx) ** 2 + ((py - cy) / ry) ** 2 <= 1
        side = abs(px - cx) <= rx and py >= cy and ((px - cx) / rx) ** 2 + ((py - cy - H) / ry) ** 2 <= 1
        return top, top or side

    def bite_at(px, py, dy):
        if not bite:
            return False
        y = py - dy
        if math.hypot(px - BX, y - BY) <= BR:
            return True
        for k in range(-2, 3):
            a = math.radians(222 + k * 30)
            tx, ty = BX + BR * 1.12 * math.cos(a), BY + BR * 1.12 * math.sin(a)
            if math.hypot(px - tx, y - ty) <= 2.3:
                return True
        return False

    def h(x, y):
        return ((x * 73856093) ^ (y * 19349663)) % 97

    S, T, B = {}, {}, {}
    for y in range(N):
        for x in range(N):
            px, py = x + .5, y + .5
            t, b = parts(px, py)
            bi = bite_at(px, py, 0) or bite_at(px, py, H)
            S[x, y] = b and not bi
            T[x, y] = t and not bi
            B[x, y] = b and bi
    for _ in range(2):
        for (x, y) in list(S):
            if S[x, y] and sum(S.get((x + dx, y + dy), False) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))) <= 1:
                S[x, y] = T[x, y] = False
    col = {}
    for (x, y), s in S.items():
        if not s:
            continue
        nb = [(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))]
        outside = [p for p in nb if not S.get(p, False)]
        near = any(B.get((x + dx, y + dy), False) for dx in (-2, -1, 0, 1, 2) for dy in (-2, -1, 0, 1, 2))
        topface = T[x, y]
        rim = topface and not T.get((x, y + 1), False) and S.get((x, y + 1), False)
        if outside and not any(B.get(p, False) for p in outside): c = '#4a2a12'
        elif outside: c = '#7a4d24'
        elif near and not topface: c = ['#e9c98e', '#d4a861', '#f2dba6', '#c4914f'][h(x, y) % 4]
        elif near: c = ['#f6e1ad', '#e8c486', '#f6e1ad', '#dcb06a'][h(x, y) % 4]
        elif rim: c = '#a8743a'
        elif topface:
            c = '#eccb8c'
            if (x - 12) ** 2 / 26 + (y - 9) ** 2 / 6 < 1: c = '#f8e6b8'
            elif h(x, y) % 13 == 0 and not (9 <= x <= 25 and 11 <= y <= 20): c = '#d6a862'
        else:
            c = '#c89350' if y < cy + ry + 3 else '#b07a3d'
            if h(x, y) % 10 == 0: c = '#9c6a33'
        col[x, y] = c
    bottom = max(y for (x, y) in col)
    return col, bottom


# --- the face, arms and feet, drawn as little pictures -------------------------------

# Effects (sparks, dots, Zzz) in colors that read on a dark theme and a light one.
INK = {'k': '#2b1608', 'w': '#ffffff', 'r': '#c0504d', 'p': '#ec9a7a', 'o': '#4a2a12',
       'f': '#c89350', 'l': '#e2b679', 'g': '#3b2614', 'b': '#bfe3f0', 'y': '#f0a830',
       'z': '#6c7bd6', 's': '#4fa3d1', 'c': '#e8c486', 'd': '#d3a45e', 'W': '#fff6d8',
       'a': '#a8743a'}

def glyph(rows):
    return [(x, y, ch) for y, row in enumerate(rows) for x, ch in enumerate(row) if ch not in '. ']

EYES = {
    'open':   ['kw', 'kk', 'kk'],
    'blink':  ['..', '..', 'kk'],
    'half':   ['..', 'kk', 'kk'],
    'up':     ['kw', 'kk', '..'],
    'wide':   ['kkw', 'kkk', 'kkk'],
    'happy':  ['...', '.k.', 'k.k'],
    'closed': ['...', '...', 'kkk'],
    'dizzy':  ['k.k', '.k.', 'k.k'],
    'side':   ['wk', 'kk', 'kk'],
}
MOUTHS = {
    'smile': ['k..k', '.kk.'],
    'big':   ['kkkk', 'krrk', '.kk.'],
    'o':     ['.kk.', 'krrk', '.kk.'],
    'small': ['.k.', 'k.k', '.k.'],
    'flat':  ['....', 'kkkk'],
    'wavy':  ['.k.k', 'k.k.'],
    'tiny':  ['.kk.'],
    'grin':  ['k..k', 'kkkk'],
}
# Arms, the left one (the right is its mirror); (0, 0) is where it meets the body.
ARMS = {
    'down':  (glyph(['.ooo', 'ollo', 'offo', 'offo', '.oo.']), (-3, -1)),
    'up':    (glyph(['.oo...', 'ollo..', 'offlo.', '.offo.', '..offo', '...ooo']), (-5, -6)),
    'out':   (glyph(['ooo....', 'olo....', 'offoooo', 'offfffo', '.oooooo']), (-6, -2)),
    'work1': (glyph(['.ooo', 'ollo', 'offo', '.oo.']), (-3, -3)),
    'work2': (glyph(['.ooo', 'ollo', 'offo', '.oo.']), (-3, 0)),
    'none':  ([], (0, 0)),
}
FOOT = glyph(['.ooo.', 'offfo', 'ooooo'])

# Where things go on the mazapán (its own grid): the eyes, the mouth, the cheeks,
# the shoulders.
EYE_L, EYE_R = (12, 12), (20, 12)
MOUTH = (15, 17)
CHEEKS = [(9, 16), (10, 16), (24, 16), (25, 16)]
SHOULDER_L, SHOULDER_R = (5, 23), (34, 23)

# The crumbs by the bite, falling clear of the right arm.
CRUMBS = [(37, 12, 'c'), (39, 15, 'd'), (36, 17, 'c'), (40, 18, 'd')]

# Effects, in the frame's own grid.
SPARK = glyph(['.y.', 'yWy', '.y.'])
SPARK_S = glyph(['y'])
DOT = glyph(['.a.', 'aWa', '.a.'])
Z = glyph(['zzzz', '..z.', '.z..', 'zzzz'])
Z_BIG = glyph(['zzzzz', '...z.', '..z..', '.z...', 'zzzzz'])
DROP = glyph(['.s.', '.s.', 'sss', 'sss', '.s.'])
STAR = glyph(['.y.', 'yyy', '.y.'])


def frame(face='open', mouth='smile', arms=('down', 'down'), dx=0, dy=0, squash=0,
          feet=(0, 0), glasses=False, effects=(), cheeks=True, crumbs=None, tilt=0):
    """One 48×48 frame: {(x, y): color}."""
    rx, ry, H = 15.2 + squash * 0.8, 10.5 - squash * 0.5, 7.0 - squash * 0.9
    body, bottom = mazapan(rx, ry, H)
    ox = 4 + dx
    oy = (F - 4) - bottom + dy - 1          # its feet on the ground
    px = {}

    def put(x, y, c):
        if 0 <= x < F and 0 <= y < F:
            px[x, y] = c

    # Feet first: the body sits on them.
    fy = oy + bottom
    for i, (fxs, lift) in enumerate(zip((13, 22), feet)):
        for x, y, ch in FOOT:
            put(ox + fxs + x, fy + y - lift, INK[ch])
    for (x, y), c in body.items():
        put(ox + x, oy + y + (tilt * (x - 20) // 20 if tilt else 0), c)

    def on(x, y):                             # a point on the mazapán, in the frame
        return ox + x, oy + y + (tilt * (x - 20) // 20 if tilt else 0)

    # The face.
    if cheeks:
        for x, y in CHEEKS:
            put(*on(x, y), INK['p'])
    for (ex, ey), mirror in ((EYE_L, False), (EYE_R, True)):
        g = EYES[face]
        w = len(g[0])
        for y, row in enumerate(g):
            for x, ch in enumerate(row):
                if ch in '. ':
                    continue
                xx = (w - 1 - x) if (mirror and face in ('open', 'up', 'wide', 'side')) else x
                # the highlight stays on the same side in both eyes
                if face in ('open', 'up', 'wide', 'side'):
                    xx = x
                put(*on(ex + xx - (1 if w == 3 else 0), ey + y), INK[ch])
    if glasses:
        for (ex, ey) in (EYE_L, EYE_R):
            for x in range(-2, 4):
                put(*on(ex + x, ey - 2), INK['g']); put(*on(ex + x, ey + 3), INK['g'])
            for y in range(-2, 4):
                put(*on(ex - 2, ey + y), INK['g']); put(*on(ex + 3, ey + y), INK['g'])
            for x in range(-1, 3):
                for y in range(-1, 3):
                    if (ex + x, ey + y) not in [(ex + a, ey + b) for a in range(2) for b in range(3)]:
                        put(*on(ex + x, ey + y), INK['b'])
        for x in (EYE_L[0] + 4, EYE_L[0] + 5):
            put(*on(x, EYE_L[1]), INK['g'])
    mg = MOUTHS[mouth]
    for y, row in enumerate(mg):
        for x, ch in enumerate(row):
            if ch not in '. ':
                put(*on(MOUTH[0] + x, MOUTH[1] + y), INK[ch])

    # Arms.
    for name, (sx, sy), side in ((arms[0], SHOULDER_L, 1), (arms[1], SHOULDER_R, -1)):
        pts, (ax, ay) = ARMS[name]
        for x, y, ch in pts:
            put(*on(sx + side * (ax + x), sy + ay + y), INK[ch])

    # Crumbs by the bite (they jump while it listens).
    for x, y, c in (crumbs or CRUMBS):
        put(*on(x, y), INK[c])

    for kind, x, y in effects:
        for gx, gy, ch in {'spark': SPARK, 'spark_s': SPARK_S, 'dot': DOT, 'z': Z, 'zbig': Z_BIG,
                           'drop': DROP, 'star': STAR}[kind]:
            put(x + gx, y + gy, INK[ch])
    return px


# --- the states -----------------------------------------------------------------------

def bob(n, amp=1):
    return [round(-amp * (1 - math.cos(2 * math.pi * i / n)) / 2) for i in range(n)]

STATES = {}

# idle: breathing; a blink once a loop.
STATES['idle'] = (6, [frame(dy=d, face='blink' if i == 9 else 'open') for i, d in enumerate(bob(12))])

# listen: eyes wide, mouth a little "o", the crumbs jumping with the voice.
def crumbs_jump(i):
    j = [(0, -2), (0, -1), (0, 0), (0, -1)]
    base = CRUMBS
    return [(x, y + j[(i + k) % 4][1], c) for k, (x, y, c) in enumerate(base)]
STATES['listen'] = (8, [frame(face='wide', mouth='small', dy=(-1 if i % 4 in (1, 2) else 0), crumbs=crumbs_jump(i),
                              arms=('down', 'down')) for i in range(8)])

# think: looking up, three dots, a slight lean.
think = []
for i in range(9):
    n = min(3, i // 2) if i < 8 else 3
    dots = [('dot', 33 + 4 * k, 7 - 2 * k) for k in range(n)]
    think.append(frame(face='up', mouth='flat', arms=('down', 'work2'), dx=(1 if i % 6 < 3 else 0), effects=dots))
STATES['think'] = (5, think)

# work: focused, arms busy, sparks.
work = []
for i in range(6):
    sp = [('spark', 40, 6), ('spark_s', 44, 12)] if i % 3 == 0 else [('spark', 42, 10), ('spark_s', 38, 3)] if i % 3 == 1 else [('spark_s', 41, 4), ('spark_s', 45, 9)]
    work.append(frame(face='half', mouth='tiny', arms=(('work1', 'work2') if i % 2 else ('work2', 'work1')),
                      dy=(0 if i % 2 else -1), effects=sp))
STATES['work'] = (10, work)

# wait: looking at you, a foot tapping.
STATES['wait'] = (6, [frame(face='open', mouth='smile', feet=(0, 1 if i % 2 else 0), arms=('down', 'down'),
                            dy=0) for i in range(8)])

# happy: a jump, arms up, sparkles.
jump = [0, -1, -3, -5, -6, -5, -3, -1, 0, 0]
sq = [1, 0, -1, -1, -1, -1, -1, 0, 1, 0]
happy = []
for i, (d, s) in enumerate(zip(jump, sq)):
    sp = [('spark', 3, 8), ('spark', 42, 4)] if i in (3, 4, 5) else [('spark_s', 5, 12), ('spark_s', 43, 8)] if i in (2, 6) else []
    happy.append(frame(face='happy', mouth='big', arms=('up', 'up') if d < -1 else ('down', 'down'), dy=d, squash=s, effects=sp))
STATES['happy'] = (10, happy)

# shrug: arms out, a flat mouth, a sweat drop.
STATES['shrug'] = (6, [frame(face='open', mouth='wavy', arms=('out', 'out') if 1 <= i <= 5 else ('down', 'down'),
                             dy=(-1 if 2 <= i <= 4 else 0), effects=[('drop', 38, 8)] if 2 <= i <= 5 else []) for i in range(8)])

# read: idle, with reading glasses.
STATES['read'] = (6, [frame(dy=d, glasses=True, face='blink' if i == 9 else 'open', mouth='tiny') for i, d in enumerate(bob(12))])

# sleep: eyes shut, Zzz rising, slow breathing.
sleep = []
for i in range(8):
    zs = [('z', 36, 12 - i), ('zbig', 40, 6 - i // 2)] if i < 6 else [('z', 36, 6)]
    sleep.append(frame(face='closed', mouth='tiny', squash=(1 if i % 4 < 2 else 0), arms=('down', 'down'),
                       cheeks=True, effects=[e for e in zs if e[2] >= 0]))
STATES['sleep'] = (3, sleep)

# dizzy: spiral eyes, stars going round, wobbling.
dizzy = []
for i in range(8):
    a = 2 * math.pi * i / 8
    stars = [('star', int(22 + 12 * math.cos(a + k * 2.1)), int(4 + 3 * math.sin(a + k * 2.1))) for k in range(3)]
    dizzy.append(frame(face='dizzy', mouth='wavy', dx=[0, 1, 1, 0, 0, -1, -1, 0][i], arms=('down', 'down'), effects=stars))
STATES['dizzy'] = (8, dizzy)

ORDER = ['idle', 'listen', 'think', 'work', 'wait', 'happy', 'shrug', 'read', 'sleep', 'dizzy']


# --- PNG, with zlib alone ----------------------------------------------------------------------

def rgba(c):
    return bytes(int(c[i:i + 2], 16) for i in (1, 3, 5)) + b'\xff'

def png(w, h, pixels, path):
    raw = bytearray()
    for y in range(h):
        raw.append(0)
        for x in range(w):
            raw += pixels.get((x, y), b'\x00\x00\x00\x00')
    def chunk(t, d):
        return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
                + chunk(b'IDAT', zlib.compress(bytes(raw), 9)) + chunk(b'IEND', b''))


def main():
    cols = max(len(STATES[s][1]) for s in ORDER)
    sheet, meta = {}, {'frame': F, 'states': {}}
    for row, name in enumerate(ORDER):
        fps, frames = STATES[name]
        meta['states'][name] = {'row': row, 'frames': len(frames), 'fps': fps}
        for i, fr in enumerate(frames):
            for (x, y), c in fr.items():
                sheet[i * F + x, row * F + y] = rgba(c)
    out = os.path.join(here, '..', 'plugins', 'agent', 'media')
    if '--preview' in sys.argv:
        out = sys.argv[sys.argv.index('--preview') + 1]
    os.makedirs(out, exist_ok=True)
    png(cols * F, len(ORDER) * F, sheet, os.path.join(out, 'mazapan-agent.png'))
    with open(os.path.join(out, 'mazapan-agent.json'), 'w') as f:
        json.dump(meta, f, indent=2)
        f.write('\n')


if __name__ == '__main__':
    main()
