#!/usr/bin/env python3
"""Reproduce the code-authored Synups pulse icon. Optional authoring tool only; Pillow is not a runtime/CI dependency."""
from pathlib import Path
from PIL import Image, ImageDraw
repo=Path(__file__).resolve().parent.parent
assets=repo/'synups/Synups.Desktop/Assets'
svg='''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64"><rect x="2" y="2" width="60" height="60" rx="14" fill="#101722"/><path d="M12 34H23L29 16L37 48L43 30H52" fill="none" stroke="#7de4ce" stroke-width="3.5" stroke-linecap="round" stroke-linejoin="round"/></svg>\n'''
(assets/'Synups.svg').write_text(svg)
size=1024;scale=size/64
image=Image.new('RGBA',(size,size),(0,0,0,0));draw=ImageDraw.Draw(image)
draw.rounded_rectangle((2*scale,2*scale,62*scale,62*scale),radius=14*scale,fill='#101722')
points=[(12,34),(23,34),(29,16),(37,48),(43,30),(52,30)];width=round(3.5*scale)
draw.line([(round(x*scale),round(y*scale)) for x,y in points],fill='#7de4ce',width=width,joint='curve')
for x,y in points:
 radius=width/2;cx=x*scale;cy=y*scale
 draw.ellipse((round(cx-radius),round(cy-radius),round(cx+radius),round(cy+radius)),fill='#7de4ce')
image.save(assets/'Synups.png')
image.save(assets/'Synups.ico',sizes=[(s,s) for s in [16,24,32,48,64,128,256]])
image.save(repo/'eng/packaging/Synups.icns')
