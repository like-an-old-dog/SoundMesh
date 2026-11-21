"""Generate native launcher marks and an original signal illustration. Requires Pillow."""
from pathlib import Path
from PIL import Image, ImageDraw
import math, json
base=Path(__file__).resolve().parents[1]
brand=base/'brand'
android=base/'android/res'
def save(im,path,**kw):
    path.parent.mkdir(parents=True,exist_ok=True);im.save(path,**kw)
def curve(points,scale=1):
    a,b,c,d=points
    result=[]
    for i in range(81):
        t=i/80
        result.append(tuple(scale*((1-t)**3*a[j]+3*(1-t)**2*t*b[j]+3*(1-t)*t*t*c[j]+t**3*d[j]) for j in (0,1)))
    return result
def stroke(draw,points,width,color):
    draw.line(points,fill=color,width=width,joint='curve')
    r=width/2
    for x,y in (points[0],points[-1]):draw.ellipse((x-r,y-r,x+r,y+r),fill=color)
def mark(mono=False):
    im=Image.new('RGBA',(1024,1024));d=ImageDraw.Draw(im);scale=8
    # Two interlocking signal paths, separated by a narrow diagonal cut.
    for y,x,color in ((0,0,'#FAFBFF'),(18,10,'#81FFF0')):
        parts=[[(24,73),(37,73),(35,40),(51,40)],[(51,40),(67,40),(61,72),(77,72)],[(77,72),(88,72),(88,51),(98,51)]]
        pts=[]
        for pts0 in parts:pts+=curve([(xx+x,yy+y-6) for xx,yy in pts0],scale)
        stroke(d,pts,57,'white' if mono else color)
    return im
fg=mark()
background=Image.new('RGB',(1024,1024))
tiny=Image.new('RGB',(128,128))
for y in range(128):
    for x in range(128):
        t=(x*.65+y*.35)/127
        tiny.putpixel((x,y),tuple(round(a+(b-a)*t) for a,b in zip((76,94,246),(42,38,122))))
background=tiny.resize((1024,1024),Image.Resampling.BICUBIC)
full=background.convert('RGBA');full.alpha_composite(fg)
mask=Image.new('L',full.size);ImageDraw.Draw(mask).rounded_rectangle((2,2,1021,1021),radius=230,fill=255)
icon=full.copy();icon.putalpha(mask)
save(icon,brand/'mark.png')
save(icon,base/'windows/SoundMesh.ico',sizes=[(x,x) for x in (16,20,24,32,40,48,64,128,256)])
ios=base/'ios/Phone/Assets.xcassets/AppIcon.appiconset'
save(full.convert('RGB'),ios/'app.png')
(ios/'Contents.json').write_text(json.dumps({'images':[{'filename':'app.png','idiom':'universal','platform':'ios','size':'1024x1024'}],'info':{'author':'xcode','version':1}}))
for density,size in [('mdpi',48),('hdpi',72),('xhdpi',96),('xxhdpi',144),('xxxhdpi',192)]:
    save(icon.resize((size,size),Image.Resampling.LANCZOS),android/('mipmap-'+density)/'launcher.png')
safe=Image.new('RGBA',(1024,1024));safe.alpha_composite(fg.resize((745,745),Image.Resampling.LANCZOS),(140,140))
save(safe.resize((432,432),Image.Resampling.LANCZOS),android/'drawable-nodpi/icon_fg.png')
notif=mark(True).crop(mark(True).getbbox());notif.thumbnail((86,86),Image.Resampling.LANCZOS)
white=Image.new('RGBA',(96,96));white.alpha_composite(notif,((96-notif.width)//2,(96-notif.height)//2))
save(white,android/'drawable-xxxhdpi/ic_status.png')
# Static illustration deliberately distinct from a real audio level meter.
W,H=1200,420
wall=Image.new('RGB',(W,H));d=ImageDraw.Draw(wall)
for x in range(W):
    t=x/W;d.line((x,0,x,H),fill=tuple(int(a+(b-a)*t) for a,b in zip((22,28,58),(47,48,111))))
for i in range(44):
    k=i/43;pts=[]
    for x in range(-4,W+8,3):
        t=x/W;amp=140*max(0,t-.19)
        y=H*(.57+.32*k)+amp*math.sin(t*9.4-k*2.7)+55*t*math.sin(t*12+k*2)
        pts.append((x,y))
    color=tuple(int(a+(b-a)*k) for a,b in zip((71,81,178),(132,226,235)))
    # Fade the left side into the background behind text.
    for j in range(len(pts)-1):
        t=pts[j][0]/W;alpha=max(0,min(1,(t-.25)*2.1));bg=wall.getpixel((min(W-1,max(0,int(t*W))),0))
        d.line((pts[j],pts[j+1]), fill=tuple(int(a+(b-a)*alpha) for a,b in zip(bg,color)),width=1)
save(wall,brand/'wave.png');save(wall.resize((900,315),Image.Resampling.LANCZOS),android/'drawable-nodpi/session_wave.png')
