"""Generate compact shared PBR trim textures; no downloaded assets."""
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import random, os
ROOT='D:/Projects/OrbitalCargoDispatch/ArtSource/StationKit'
os.makedirs(ROOT+'/textures',exist_ok=True)
S=256; N=1024
colors=[(64,77,91),(105,113,119),(30,39,48),(136,146,151),(209,146,31),(30,33,36),(255,196,91),(95,235,242),(43,53,64),(34,43,53),(17,42,61),(40,43,45),(29,36,42),(68,77,83),(20,27,33),(31,39,47)]
metal=[.45,.48,.5,.85,.18,0,.05,.05,.35,.35,.1,.25,.4,.3,.2,.35]
rough=[.48,.52,.6,.32,.48,.85,.28,.22,.55,.55,.35,.6,.6,.6,.7,.55]
atlas=Image.new('RGB',(N,N)); orm=Image.new('RGB',(N,N)); emission=Image.new('RGB',(N,N)); rng=random.Random(713)
def font(size):return ImageFont.truetype('C:/Windows/Fonts/arialbd.ttf',size)
def text_center(draw,y,text,size,fill=(223,223,208)):
    draw.text((128,y),text,font=font(size),fill=fill,anchor='mt')
for i,c in enumerate(colors):
    tile=Image.new('RGB',(S,S),c); d=ImageDraw.Draw(tile)
    # Broad plates stay readable: low contrast grain, fine irregular wear.
    for j in range(3200):
        x,y=rng.randrange(S),rng.randrange(S);v=rng.randint(-5,5)
        d.point((x,y),fill=tuple(max(0,min(255,k+v)) for k in c))
    for j in range(70 if i<5 else 12):
        x,y=rng.randrange(8,248),rng.randrange(8,248);v=rng.randint(8,22)
        d.line((x,y,x+rng.randint(1,13),y+rng.choice([0,1])),fill=tuple(min(255,k+v) for k in c),width=1)
    if i<5:
        d.rectangle((2,2,253,253),outline=tuple(int(k*.7) for k in c),width=2)
        for j in range(25):
            x=rng.randrange(5,245);y=rng.choice([5,250]);d.line((x,y,x+rng.randrange(2,9),y),fill=(133,137,132),width=1)
    if i==5:
        for y in range(0,256,18):
            d.rectangle((0,y,255,y+3),fill=(13,17,20));d.line((0,y+4,255,y+4),fill=(49,53,55),width=1)
    if i==8:
        d.rectangle((0,0,255,255),fill=(64,77,91))
        d.text((14,103),'SCAN',font=font(47),fill=(223,223,208))
        for x in (175,207):d.polygon([(x,109),(x+17,128),(x,147),(x+12,147),(x+29,128),(x+12,109)],fill=(235,173,49))
    if i==9:
        d.rounded_rectangle((5,5,250,250),radius=9,outline=(192,133,35),width=6)
        text_center(d,29,'TO',35);text_center(d,75,'DISPOSAL',34)
        d.rectangle((116,139,140,183),fill=(224,221,196));d.polygon([(95,177),(161,177),(128,213)],fill=(224,221,196))
    if i==10:
        d.rounded_rectangle((5,5,250,250),radius=8,outline=(59,157,192),width=4)
        d.line((22,30,113,30),fill=(104,214,230),width=4)
        d.polygon([(67,79),(126,56),(187,80),(127,106)],outline=(89,212,244),width=3)
        d.polygon([(67,79),(127,106),(127,170),(67,142)],outline=(89,212,244),width=3)
        d.polygon([(127,106),(187,80),(187,144),(127,170)],outline=(89,212,244),width=3)
        d.rectangle((25,196,231,207),fill=(46,123,156));d.rectangle((25,196,179,207),fill=(102,210,229))
        for x in (26,94,162):d.rectangle((x,220,x+53,229),fill=(60,150,179))
    if i==11:
        for x in range(-256,512,64):d.polygon([(x,0),(x+32,0),(x-96,256),(x-128,256)],fill=(209,146,31))
    if i==12:
        for y in range(28,230,27):
            d.rounded_rectangle((25,y,230,y+12),radius=5,fill=(9,14,19));d.line((30,y+14,225,y+14),fill=(85,94,99),width=2)
        for x in (14,241):
            for y in (14,241):d.ellipse((x-4,y-4,x+4,y+4),fill=(116,124,126))
    if i==13:
        d.rectangle((30,62,225,196),fill=(180,177,160))
        for x in range(47,205,5):d.rectangle((x,82,x+rng.randrange(1,4),163),fill=(38,42,44))
    if i==15:
        d.rectangle((114,53,142,146),fill=(215,154,38));d.polygon([(75,133),(181,133),(128,204)],fill=(215,154,38))
    x,y=i%4*S,i//4*S;atlas.paste(tile,(x,y))
    o=Image.new('RGB',(S,S),(255,int(rough[i]*255),int(metal[i]*255)))
    # Surface roughness varies with the same scratches as the albedo.
    od=ImageDraw.Draw(o)
    for j in range(500):
        px,py=rng.randrange(S),rng.randrange(S);od.point((px,py),(255,max(0,min(255,int(rough[i]*255)+rng.randrange(-12,13))),int(metal[i]*255)))
    orm.paste(o,(x,y))
    e=Image.new('RGB',(S,S))
    if i in [6,7]:e=Image.new('RGB',(S,S),c)
    if i==10:
        pix=tile.load();ep=e.load()
        for py in range(S):
            for px in range(S):
                r,g,b=pix[px,py]
                if b>110 and g>85:ep[px,py]=(int(r*.65),int(g*.65),int(b*.65))
    emission.paste(e,(x,y))
atlas.save(ROOT+'/textures/Stations_BaseColor.png',optimize=True)
orm.save(ROOT+'/textures/Stations_ORM.png',optimize=True)
emission.save(ROOT+'/textures/Stations_Emission.png',optimize=True)
# A full-range repeating belt map permits texture scrolling without moving the frame.
belt=atlas.crop((256,256,512,512));belt.save(ROOT+'/textures/Belt_BaseColor.png')
Image.new('RGB',(256,256),(255,215,0)).save(ROOT+'/textures/Belt_ORM.png')
print(ROOT)
