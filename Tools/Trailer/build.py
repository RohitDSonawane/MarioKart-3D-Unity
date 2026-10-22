import os, subprocess, sys
sys.path.insert(0, '.')
from edl import INTRO, MONTAGE, BEAT, FPS

os.makedirs('clips', exist_ok=True)
MUSIC = '../Assets/_Project/Audio'
W, H, BAR = 1920, 1080, 140          # 1920x800 picture inside black bars: 2.4:1
GRADE = "eq=contrast=1.06:saturation=1.1:gamma=0.98,curves=r='0/0 0.5/0.52 1/1':b='0/0.02 0.5/0.49 1/0.97',unsharp=5:5:0.4"
def letterbox(y=BAR): return f"crop={W}:{H - 2 * BAR}:0:{y},pad={W}:{H}:0:{BAR}:black"
GRAIN = "noise=alls=5:allf=t"
ENC = ['-c:v', 'libx264', '-preset', 'medium', '-crf', '15', '-pix_fmt', 'yuv420p']


def fade_alpha(a, b, f=0.2):
    return (f"if(lt(t\\,{a})\\,0\\,if(lt(t\\,{a + f})\\,(t-{a})/{f}\\,"
            f"if(lt(t\\,{b - f})\\,1\\,if(lt(t\\,{b})\\,({b}-t)/{f}\\,0))))")


def text(t, a, b, size=96, font='impact', y='(h-text_h)/2', color='white'):
    return (f"drawtext=fontfile=fonts/{font}.ttf:text='{t}':fontsize={size}:fontcolor={color}:x=(w-text_w)/2:y={y}"
            f":shadowcolor=black@0.6:shadowx=4:shadowy=4:alpha='{fade_alpha(a, b)}'")


def run(args):
    r = subprocess.run(['ffmpeg', '-v', 'error', '-y'] + args, capture_output=True, text=True)
    if r.returncode:
        print(' '.join(args)[:2000]); print(r.stderr); raise SystemExit(1)


clips = []          # (path, seconds)


def footage(i, take, start, n, extra=None, flash=False, fadein=0.0, cropy=BAR):
    out = f'clips/c{i:02d}.mp4'
    vf = [GRADE, letterbox(cropy)]
    if flash: vf.append("fade=t=in:st=0:d=0.17:color=white")
    if fadein: vf.append(f"fade=t=in:st=0:d={fadein}")
    vf += (extra or []) + [GRAIN]
    run(['-framerate', str(FPS), '-start_number', str(start), '-i', f'take{take}/frames/f_%06d.jpg', '-frames:v', str(n),
         '-vf', ','.join(vf)] + ENC + ['-r', str(FPS), out])
    clips.append((out, n / FPS))


def card(i, n, lines, slam=False):
    out = f'clips/c{i:02d}.mp4'; d = n / FPS
    vf = []
    for (t, size, font, y) in lines:
        if slam:   # punches in from 1.5x and settles
            vf.append(f"drawtext=fontfile=fonts/{font}.ttf:text='{t}':fontcolor=white:x=(w-text_w)/2:y={y}"
                      f":fontsize='{size}*(1+0.5*max(0\\,1-t/0.18))':alpha='{fade_alpha(0, d, 0.12)}'")
        else:
            vf.append(text(t, 0.3, d - 0.2, size, font, y))
    run(['-f', 'lavfi', '-i', f'color=c=black:s={W}x{H}:r={FPS}:d={d:.4f}', '-vf', ','.join(vf + [GRAIN])] + ENC + [out])
    clips.append((out, d))


i = 0
intro_text = ["ONE HARBOR.", "EIGHT RACERS.", "THREE LAPS.", "", ""]
for k, (take, start, n, label) in enumerate(INTRO):
    extra = []
    close = 'close' in label
    if intro_text[k]: extra.append(text(intro_text[k], 0.35, n / FPS - 0.1, 110, 'impact', 'h*0.17' if close else 'h*0.70'))
    if k == 4:   # countdown: counts at 0, 1, 2 s, GO at 3.07 s
        for v, a, b in (("3", 0.0, 0.95), ("2", 1.0, 1.95), ("1", 2.03, 3.0)):
            extra.append(text(v, a, b, 260))
        extra.append(text("GO!", 3.07, 4.4, 300, color='0xFFD21F'))
    footage(i, take, start, n, extra, fadein=0.9 if k == 0 else 0.0, cropy=250 if close else BAR); i += 1

# montage: lengths snap to the beat grid counted from GO
beats_done = 0.0; frames_done = 0; title_beat = 0
for take, start, n, label in MONTAGE:
    beats = round(n / (BEAT * FPS))
    if take == "TITLE": title_beat = beats_done
    beats_done += beats
    target = round(beats_done * BEAT * FPS)
    n2 = target - frames_done; frames_done = target
    if take == "TITLE":
        card(i, n2, [(start, 190, 'impact', '(h-text_h)/2-30'), ("A RACING GAME BUILT IN UNITY", 40, 'bahnschrift', 'h/2+90')], slam=True)
    elif take == "TEXT":
        card(i, n2, [(start, 150, 'impact', '(h-text_h)/2')], slam=True)
    else:
        extra = []
        if label == "finish Mario": extra.append("fade=t=out:st=%.2f:d=0.6" % (n2 / FPS - 0.6))
        footage(i, take, start, n2, extra, flash=label.startswith("impact"))
    i += 1
end_d = 8.0
card(i, int(end_d * FPS), [("HARBOR RACER", 170, 'impact', '(h-text_h)/2-60'),
                           ("A learning project made with Unity", 44, 'bahnschrift', 'h/2+70'),
                           ("Models, textures and audio are free third-party downloads", 30, 'bahnschrift', 'h/2+130')]); i += 1

with open('clips/list.txt', 'w') as f:
    for p, _ in clips: f.write(f"file '{os.path.basename(p)}'\n")
run(['-f', 'concat', '-safe', '0', '-i', 'clips/list.txt', '-c', 'copy', 'video.mp4'])

# ---- audio: intro sting, final-lap music from GO, finish fanfare under the end card, countdown and title effects
go_t = 11.0 + 3.07
total = sum(d for _, d in clips)
end_t = total - end_d
title_t = go_t + title_beat * BEAT
fl_len = end_t - go_t
inputs = [f'{MUSIC}/Music/CourseIntro.mp3', f'{MUSIC}/Music/ToadHarborFinalLap.mp3', f'{MUSIC}/Music/FirstPlaceFinish.mp3',
          f'{MUSIC}/SFX/Race/Countdown321.wav', f'{MUSIC}/SFX/Race/CountdownGo.wav', f'{MUSIC}/SFX/Kart/BoostBurst.wav', f'{MUSIC}/SFX/Items/Explosion.mp3']
args = []
for p in inputs: args += ['-i', p]
ms = lambda s: f"{int(s * 1000)}|{int(s * 1000)}"
fmt = "aresample=44100,aformat=channel_layouts=stereo"
filt = [
    f"[0:a]{fmt},atrim=0:{go_t:.3f},afade=t=out:st={go_t - 0.6:.3f}:d=0.6,volume=0.9[ci]",
    f"[1:a]{fmt},atrim=start=0.163,asetpts=PTS-STARTPTS,atrim=0:{fl_len + 0.5:.3f},volume=2.2,afade=t=out:st={fl_len - 1.2:.3f}:d=1.4,adelay={ms(go_t)}[fl]",
    f"[2:a]{fmt},afade=t=out:st=6.5:d=1.5,adelay={ms(end_t)}[fp]",
]
sfx = []
for k, at in enumerate([11.0, 12.0, 13.03]):
    filt.append(f"[3:a]{fmt},volume=0.8,adelay={ms(at)}[cd{k}]"); sfx.append(f"[cd{k}]")
filt.append(f"[4:a]{fmt},volume=0.9,adelay={ms(go_t)}[go]"); sfx.append("[go]")
filt.append(f"[5:a]{fmt},volume=0.8,adelay={ms(go_t + 0.15)}[bb]"); sfx.append("[bb]")
filt.append(f"[6:a]{fmt},volume=0.9,adelay={ms(title_t)}[ex]"); sfx.append("[ex]")
filt.append(f"[ci][fl][fp]{''.join(sfx)}amix=inputs={3 + len(sfx)}:normalize=0,atrim=0:{total:.3f},"
            f"afade=t=out:st={total - 1.5:.3f}:d=1.5,loudnorm=I=-14:TP=-1.0:LRA=11[a]")
run(args + ['-filter_complex', ';'.join(filt), '-map', '[a]', '-ar', '48000', '-c:a', 'pcm_s16le', 'audio.wav'])
run(['-i', 'video.mp4', '-i', 'audio.wav', '-map', '0:v', '-map', '1:a', '-c:v', 'copy', '-c:a', 'aac', '-b:a', '256k',
     '-shortest', '-movflags', '+faststart', 'HarborRacer_Trailer.mp4'])
print(f"done: {len(clips)} clips, {total:.1f} s, GO at {go_t:.2f}s, title at {title_t:.2f}s, end card at {end_t:.2f}s")
