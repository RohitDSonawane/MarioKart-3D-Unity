import sys, numpy as np
from scipy.io import wavfile
from scipy.signal import find_peaks
for name in sys.argv[1:]:
    sr, x = wavfile.read(name); x = x.astype(np.float32) / 32768.0
    hop = 512; n = len(x) // hop
    frames = x[:n*hop].reshape(n, hop)
    rms = np.sqrt((frames**2).mean(1))
    # spectral flux onset envelope
    win = 2048; spec = []
    for i in range(0, len(x) - win, hop):
        spec.append(np.abs(np.fft.rfft(x[i:i+win] * np.hanning(win)))[:400])
    spec = np.log1p(np.array(spec) * 10)
    flux = np.maximum(0, np.diff(spec, axis=0)).sum(1); flux = (flux - flux.mean()) / (flux.std() + 1e-9)
    fps = sr / hop
    ac = np.correlate(flux, flux, 'full')[len(flux)-1:]
    lo, hi = int(fps * 60 / 200), int(fps * 60 / 70)
    lag = lo + np.argmax(ac[lo:hi]); bpm = 60 * fps / lag
    # beat phase: best offset of a pulse train
    period = lag; best = max(range(period), key=lambda o: flux[o::period].sum())
    beats = np.arange(best, len(flux), period) / fps
    secs = [f"{rms[int(s*fps):int((s+4)*fps)].mean():.3f}" for s in range(0, int(len(x)/sr) - 4, 4)]
    print(name, f"bpm {bpm:.1f} first beat {beats[0]:.3f}s period {period/fps:.3f}s")
    print("  loudness per 4 s:", " ".join(secs))
