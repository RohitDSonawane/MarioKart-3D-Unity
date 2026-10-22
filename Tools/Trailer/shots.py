import re, json, sys
from PIL import Image, ImageDraw, ImageFont
def load(take):
    lines = open(f"take{take}/shots.txt").read().splitlines()
    shots = []
    for l in lines:
        m = re.match(r"(\d+) ([\d.]+) SHOT (\w+) (.+?) dur", l)
        if m: shots.append(dict(take=take, start=int(m.group(1)), t=float(m.group(2)), type=m.group(3), subj=m.group(4), events=[]))
        else:
            m2 = re.match(r"(\d+) ([\d.]+) (.*)", l)
            if m2 and shots: shots[-1]["events"].append(m2.group(1) + ":" + m2.group(3))
    end = int(lines[-1].split()[0])
    for i, s in enumerate(shots): s["end"] = shots[i+1]["start"] if i + 1 < len(shots) else end
    return shots
allshots = load("A") + load("B")
json.dump(allshots, open("shots.json", "w"), indent=0)
font = ImageFont.truetype("fonts/arialbd.ttf", 22)
per = 20; tw, th = 480, 270
for page in range(0, len(allshots), per):
    sheet = Image.new("RGB", (tw * 5, th * 4))
    for k, s in enumerate(allshots[page:page + per]):
        n = s["end"] - s["start"]
        im = Image.new("RGB", (tw, th))
        for j, f in enumerate([0.2, 0.55, 0.9]):
            fr = s["start"] + int(n * f)
            t = Image.open(f"take{s['take']}/frames/f_{fr:06d}.jpg").resize((tw, th))
            if j == 1: im = t
        d = ImageDraw.Draw(im)
        idx = page + k
        d.text((6, 4), f"#{idx} {s['take']}{s['start']} {s['type']} {s['subj']} {n/30:.1f}s", font=font, fill=(255, 255, 0), stroke_width=2, stroke_fill=(0, 0, 0))
        sheet.paste(im, ((k % 5) * tw, (k // 5) * th))
    sheet.save(f"sheet_{page // per:02d}.jpg", quality=85)
print(len(allshots), "shots")
