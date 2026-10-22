# Edit decision list. Each clip: (take, first frame, length in frames or beats "Nb", label, flags)
BEAT = 0.30180  # final-lap track beat (198.8 bpm)
FPS = 30
def bf(n): return round(n * BEAT * FPS)
INTRO = [   # over the course-intro sting; must total 11.0 s before the countdown frame
    ("A", 20, 135, "aerial"),            # 4.5 s
    ("B", 168, 90, "grid orbit"),        # 3.0 s
    ("B", 273, 54, "Mario close"),       # 1.8 s
    ("B", 334, 51, "Shy Guy close"),     # 1.7 s
    ("A", 449, 151, "grid front countdown + GO"),   # 5.0 s: counts at +0,+1,+2, GO at +3.07
]
MONTAGE = [
    # speed
    ("B", 822, bf(4), "wheel Peach pack"),
    ("A", 6050, bf(8), "crane Waluigi vista"),
    ("A", 3380, bf(8), "wheel Shy Guy harbor"),
    ("A", 4900, bf(8), "side Waluigi crowd"),
    ("B", 5078, bf(8), "front Mario tram"),
    ("B", 5590, bf(8), "chase high Mario vista"),
    ("B", 3125, bf(8), "wheel Waluigi harbor"),
    ("B", 6500, bf(8), "chase Mario red shells"),
    ("TITLE", "HARBOR RACER", bf(8), ""),
    # air
    ("TEXT", "TAKE FLIGHT", bf(4), ""),
    ("A", 8782, bf(12), "ramp Waluigi vista"),
    ("B", 5672, bf(12), "ramp Mario 3"),
    # chaos
    ("TEXT", "NO MERCY", bf(4), ""),
    ("A", 3905, bf(4), "chase red shells"),
    ("A", 3790, bf(8), "impact Shy Guy red shells"),
    ("B", 6352, bf(4), "crane red shells"),
    ("A", 6457, bf(8), "impact Baby Mario shells"),
    ("B", 8268, bf(4), "chase Mario shells tram"),
    ("B", 7281, bf(8), "impact Mario green shells"),
    ("A", 5010, bf(4), "Waluigi bomb"),
    ("B", 3919, bf(8), "impact Toad flip"),
    ("B", 4671, bf(8), "impact Mario hurt"),
    ("A", 8339, bf(8), "impact Shy Guy flip"),
    ("B", 1882, bf(8), "impact Toad shells"),
    # faces
    ("A", 7520, bf(4), "front Waluigi"),
    ("A", 8010, bf(4), "front Shy Guy"),
    ("A", 5125, bf(4), "front Toad"),
    ("B", 2070, bf(4), "front Luigi"),
    ("A", 3170, bf(4), "front Lemmy"),
    ("B", 2660, bf(4), "front Mario"),
    # finish
    ("TEXT", "ONE WINNER", bf(4), ""),
    ("B", 9188, bf(16), "finish Mario"),
]
