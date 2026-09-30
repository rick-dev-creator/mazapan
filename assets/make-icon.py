#!/usr/bin/env python3
# Mazapán's icon: a bitten mazapán, tilted to show its thickness, in 80s
# pixel art (40×40). Writes assets/mazapan.svg (the mazapán alone: the bar,
# the login screen, the installer) and assets/mazapan-app.svg (on a
# synthwave sunset: the app, the ISO), and the shell's copy.
#   python3 assets/make-icon.py
import os
here = os.path.dirname(os.path.abspath(__file__))
import math
N=40
cx,cy,rx,ry,H=20,15.5,15.2,10.5,7   # tilted: the top an ellipse, the thickness below
BX,BY,BR=34.5,6.0,5.8               # the bite, top right, down through its thickness
def parts(px,py):
    top=((px-cx)/rx)**2+((py-cy)/ry)**2<=1
    side=abs(px-cx)<=rx and py>=cy and ((px-cx)/rx)**2+((py-cy-H)/ry)**2<=1
    return top, top or side
def bite_at(px,py,dy):
    # the same bite on the top and on the bottom face (it goes through)
    y=py-dy
    if math.hypot(px-BX,y-BY)<=BR: return True
    for k in range(-2,3):
        a=math.radians(222+k*30)
        tx,ty=BX+BR*1.12*math.cos(a),BY+BR*1.12*math.sin(a)
        if math.hypot(px-tx,y-ty)<=2.3: return True
    return False
def bitten(px,py):
    return any(bite_at(px,py,d) for d in (0,H))
def h(x,y): return ((x*73856093)^(y*19349663))%97
S={};T={};B={}
for y in range(N):
    for x in range(N):
        px,py=x+.5,y+.5
        t,b=parts(px,py); bi=bitten(px,py)
        S[x,y]=b and not bi; T[x,y]=t and not bi and not bite_at(px,py,0)
        B[x,y]=b and bi
for _ in range(2):
    for (x,y) in list(S):
        if S[x,y] and sum(S.get((x+dx,y+dy),False) for dx,dy in((1,0),(-1,0),(0,1),(0,-1)))<=1: S[x,y]=False; T[x,y]=False
col={}
for (x,y),s in S.items():
    if not s: continue
    nb=[(x+dx,y+dy) for dx,dy in((1,0),(-1,0),(0,1),(0,-1))]
    outside=[p for p in nb if not S.get(p,False)]
    near=any(B.get((x+dx,y+dy),False) for dx in(-2,-1,0,1,2) for dy in(-2,-1,0,1,2))
    topface=T[x,y]
    # the edge where the top meets the side: a darker line gives the thickness
    rim=topface and not T.get((x,y+1),False) and S.get((x,y+1),False)
    if outside and not any(B.get(p,False) for p in outside): c='#4a2a12'
    elif outside: c='#7a4d24'
    elif near and not topface: c=['#e9c98e','#d4a861','#f2dba6','#c4914f'][h(x,y)%4]   # inside, through the bite
    elif near: c=['#f6e1ad','#e8c486','#f6e1ad','#dcb06a'][h(x,y)%4]
    elif rim: c='#a8743a'
    elif topface:
        c='#eccb8c'
        if (x-13)**2/34+(y-10)**2/9<1: c='#f8e6b8'
        elif h(x,y)%13==0: c='#d6a862'
    else:
        c='#c89350' if y<cy+ry+3 else '#b07a3d'
        if h(x,y)%10==0: c='#9c6a33'
    col[x,y]=c
for x,y,c in [(36,15,'#e8c486'),(38,18,'#d3a45e'),(35,19,'#f6e1ad'),(37,22,'#d3a45e'),(38,13,'#e8c486')]:
    col[x,y]=c
for x,y in [(5,4),(5,3),(5,5),(4,4),(6,4),(8,1)]:
    col[x,y]='#ffffff'
def svg(bg):
    o=[f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {N} {N}" shape-rendering="crispEdges">']
    if bg:
        o.append('<defs><linearGradient id="sky" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#170836"/><stop offset="1" stop-color="#4b1273"/></linearGradient>'
                 '<linearGradient id="sun" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#ffe066"/><stop offset=".5" stop-color="#ff7a6e"/><stop offset="1" stop-color="#e03aa6"/></linearGradient>'
                 '<clipPath id="r"><rect width="40" height="40" rx="7"/></clipPath></defs><g clip-path="url(#r)">')
        o.append('<rect width="40" height="40" fill="url(#sky)"/><circle cx="20" cy="17" r="15" fill="url(#sun)"/>')
        for yy,hh in ((20,1),(23,1.4),(26,1.8)): o.append(f'<rect x="0" y="{yy}" width="40" height="{hh}" fill="#4b1273"/>')
        o.append('<rect x="0" y="29" width="40" height="11" fill="#170836"/>')
        for xx in range(-20,61,5): o.append(f'<line x1="20" y1="29" x2="{xx}" y2="40" stroke="#ff4fb4" stroke-width=".4"/>')
        for yy in (30.5,32.5,35.5,39): o.append(f'<line x1="0" y1="{yy}" x2="40" y2="{yy}" stroke="#ff4fb4" stroke-width=".4"/>')
        o.append('</g><g transform="translate(4,4) scale(.8)">')
    for (x,y),c in sorted(col.items()):
        o.append(f'<rect x="{x}" y="{y}" width="1.02" height="1.02" fill="{c}"/>')
    if bg: o.append('</g>')
    o.append('</svg>')
    return '\n'.join(o)
open(os.path.join(here, 'mazapan.svg'), 'w').write(svg(False) + '\n')
open(os.path.join(here, 'mazapan-app.svg'), 'w').write(svg(True) + '\n')
open(os.path.join(here, '..', 'plugins', 'shell-bar', 'mazapan.svg.tmpl'), 'w').write(svg(False) + '\n')
