using System.Collections.Generic;
using System.IO;
using System.Threading;
using HarborRacer.Inputs;
using HarborRacer.Items;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HarborRacer
{
    /// <summary>
    /// Opt-in trailer recorder: start a built player with -trailer [outDir]. Every kart is computer-driven, a director cuts between
    /// cinematic camera setups (chase, front, side tracking, wheel, orbit, trackside telephoto, overhead, crane, ramp, glide, impact,
    /// finish), hands out items to the karts near the shot, slows time for jumps and hits, and writes every frame at a fixed 30 fps
    /// to outDir/frames plus a shot log (shots.txt) for editing. The game itself is unchanged without the switch.
    /// </summary>
    public class TrailerDirector : MonoBehaviour
    {
        enum Shot { Aerial, GridOrbit, GridSweep, DriverClose, GridFront, ChaseLow, ChaseHigh, Front, Side, Wheel, Orbit, Trackside, Overhead, Crane, Ramp, Glide, Impact, Finish, Celebrate }

        const int W = 1920, H = 1080, Fps = 30;
        static readonly int CameraMask = (1 << Layers.Wall) | (1 << Layers.Drivable) | (1 << Layers.Scenery);

        string outDir;
        Camera cam;
        RenderTexture rt;
        Volume volume;
        StreamWriter log;
        int frame, pendingWrites, pendingReads;
        bool ready, stopping;

        RaceManager rm;
        readonly List<RacerProgress> racers = new List<RacerProgress>();
        RacerProgress hero;
        readonly List<Transform> ramps = new List<Transform>();
        readonly HashSet<Transform> rampUsed = new HashSet<Transform>();
        Transform lapLine;
        Vector3 gridPos, gridFwd;

        Shot shot = Shot.Aerial, lastShot;
        RacerProgress subj;
        float shotT, shotDur, shotAge;
        int side = 1, introStep;
        Vector3 anchor, camPos, lookPt, smFwd;
        float fov = 60f, avoidUp, orbitAng, airT, rampLanded = -1f;
        bool wasAir, cutThisFrame;
        float slowTarget = 1f, slowLeft, powerShotCooldown;
        float itemTimer = 4f, heroItemTimer = 8f, impactCooldown, blueCooldown = 30f, heroFinishedAt = -1f;
        RacerProgress pendingImpact;
        float pendingImpactAt = -10f;
        readonly System.Random rng = new System.Random(7);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-trailer");
            if (i < 0) return;
            string dir = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : Path.Combine(Application.dataPath, "..", "Trailer");
            var race = FindAnyObjectByType<RaceManager>();
            if (race != null) race.introSeconds = 15f;                 // room for the establishing shots before the countdown
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Time.captureFramerate = Fps;                               // fixed game time per frame, however long a frame takes to render and save
            var go = new GameObject("TrailerDirector");
            go.AddComponent<TrailerDirector>().outDir = dir;
        }

        void Start()
        {
            Directory.CreateDirectory(Path.Combine(outDir, "frames"));
            log = new StreamWriter(Path.Combine(outDir, "shots.txt")) { AutoFlush = true };
            var go = new GameObject("TrailerCam");
            cam = go.AddComponent<Camera>();
            cam.nearClipPlane = 0.2f; cam.farClipPlane = 3000f; cam.allowMSAA = true; cam.depth = 50;
            var urp = cam.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = true; urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; urp.antialiasingQuality = AntialiasingQuality.High;
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            rt.Create();
            cam.targetTexture = rt;

            var vgo = new GameObject("TrailerVolume");
            volume = vgo.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 50f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var mb = profile.Add<MotionBlur>(true); mb.intensity.Override(0.2f); mb.quality.Override(MotionBlurQuality.High); mb.clamp.Override(0.06f);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.3f); vig.smoothness.Override(0.45f);
            var ca = profile.Add<ColorAdjustments>(true); ca.contrast.Override(10f); ca.saturation.Override(12f);
            volume.sharedProfile = profile;
        }

        void Init()
        {
            rm = RaceManager.Instance;
            if (rm == null || rm.Racers.Count == 0) return;
            racers.AddRange(rm.Racers);
            hero = rm.Player != null ? rm.Player : racers[0];
            foreach (var r in racers)
            {
                var rr = r;
                if (r.Kart.HitReaction != null) r.Kart.HitReaction.Hit += t => OnHit(rr, t);
                var h = r.GetComponent<ItemHolder>(); if (h != null) h.ItemUsed += id => Log($"ITEM {rr.racerName} {id}");
            }
            // the player kart drives itself along the racing line
            var pk = hero.GetComponent<PlayerKartInput>(); if (pk) pk.enabled = false;
            var pd = hero.gameObject.AddComponent<PathDriverInput>(); pd.kart = hero.Kart;
            pd.path = GameObject.Find("Markers/Paths/AI/ComputerDriverPATH Toad").GetComponent<TrackPath>(); pd.SnapToPath();
            pd.racerAI = true; pd.profile = AIProfile.ForName("Standin"); pd.hopMinDistanceFromHuman = 0f; hero.Kart.Input = pd;

            foreach (var z in FindObjectsByType<JumpPanelZone>()) ramps.Add(z.transform);
            foreach (var z in FindObjectsByType<GliderRampZone>()) ramps.Add(z.transform);
            var ll = FindAnyObjectByType<LapLineZone>(); if (ll != null) lapLine = ll.transform;
            gridPos = Vector3.zero; foreach (var r in racers) gridPos += r.transform.position; gridPos /= racers.Count;
            gridFwd = Flat(hero.transform.forward);

            foreach (var c in FindObjectsByType<Camera>()) if (c != cam) c.enabled = false;
            rm.StateChanged += s => Log($"STATE {s}");
            rm.CountdownTick += v => Log($"COUNT {v}");
            subj = hero;
            StartShot(Shot.Aerial, hero, 5.5f);
            ready = true;
        }

        void Log(string s) => log.WriteLine($"{frame} {Time.time:F2} {s}");

        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude < 1e-4f ? Vector3.forward : v.normalized; }

        // ---------------------------------------------------------------- events

        void OnHit(RacerProgress victim, HitType t)
        {
            if (t == HitType.Bump) return;
            Log($"HIT {victim.racerName} {t}");
            if (!ready || rm.State != RaceState.Racing) return;
            if (cam == null || (victim.transform.position - cam.transform.position).magnitude > 70f) return;
            pendingImpact = victim; pendingImpactAt = Time.time;
        }

        // ---------------------------------------------------------------- shot selection

        void StartShot(Shot s, RacerProgress who, float dur)
        {
            lastShot = shot; shot = s; subj = who; shotT = 0f; shotDur = dur; side = rng.NextDouble() < 0.5 ? -1 : 1;
            cutThisFrame = true; wasAir = false; airT = 0f; rampLanded = -1f;
            smFwd = Flat(who.transform.forward);
            orbitAng = (float)rng.NextDouble() * 360f;
            Log($"SHOT {s} {who.racerName} dur {dur:F1}");
        }

        RacerProgress PackSubject()
        {
            if (rng.NextDouble() < 0.4 && Usable(hero)) return hero;
            RacerProgress best = hero; int bestN = -1;
            foreach (var a in racers)
            {
                if (!Usable(a)) continue;
                int n = 0; foreach (var b in racers) if (b != a && (b.transform.position - a.transform.position).sqrMagnitude < 30f * 30f) n++;
                n = n * 10 + rng.Next(5);
                if (n > bestN) { bestN = n; best = a; }
            }
            return best;
        }

        static bool Usable(RacerProgress r) => r != null && !r.Kart.Respawn.Respawning && r.transform.position.y > -250f;

        void ChooseRacingShot()
        {
            var who = PackSubject();
            var pool = new List<(Shot s, float w)> { (Shot.ChaseLow, 3f), (Shot.Front, 3f), (Shot.Side, 3f), (Shot.Wheel, 2f), (Shot.Orbit, 1.5f),
                (Shot.Trackside, 3f), (Shot.Overhead, 1f), (Shot.Crane, 1.2f), (Shot.ChaseHigh, 1.5f), (Shot.DriverClose, 1f) };
            float sum = 0f; foreach (var p in pool) if (p.s != shot) sum += p.w;
            float pick = (float)rng.NextDouble() * sum; Shot chosen = Shot.ChaseLow;
            foreach (var p in pool) { if (p.s == shot) continue; pick -= p.w; if (pick <= 0f) { chosen = p.s; break; } }
            float dur = chosen == Shot.DriverClose ? 2.4f : chosen == Shot.Trackside ? 5f : 3.2f + (float)rng.NextDouble() * 2f;
            StartShot(chosen, who, dur);
            if (chosen == Shot.Trackside) PlaceTrackside();
        }

        void PlaceTrackside()
        {
            var t = subj.transform; Vector3 v = subj.Kart.Body.linearVelocity; float speed = Mathf.Max(20f, v.magnitude);
            Vector3 f = Flat(v.sqrMagnitude > 25f ? v : t.forward);
            for (int tries = 0; tries < 6; tries++)
            {
                Vector3 ahead = t.position + f * speed * (1.4f - tries * 0.15f);
                if (!Physics.Raycast(ahead + Vector3.up * 12f, Vector3.down, out var g, 40f, Layers.GroundMask, QueryTriggerInteraction.Ignore)) continue;
                Vector3 c = g.point + Vector3.Cross(Vector3.up, f) * side * (7f + tries) + Vector3.up * (1.2f + tries * 0.4f);
                if (Physics.CheckSphere(c, 0.6f, CameraMask, QueryTriggerInteraction.Ignore)) { side = -side; continue; }
                if (Physics.Linecast(c, g.point + Vector3.up * 1f, CameraMask, QueryTriggerInteraction.Ignore)) { side = -side; continue; }
                anchor = c; return;
            }
            shot = Shot.Side;                                              // no clear spot ahead: track from the side instead
        }

        Transform RampAhead()
        {
            var p = subj.transform.position; Vector3 f = Flat(subj.Kart.Body.linearVelocity);
            foreach (var r in ramps)
            {
                if (rampUsed.Contains(r) && rng.NextDouble() < 0.5) continue;
                Vector3 d = r.position - p; float dist = d.magnitude;
                if (dist < 25f || dist > 75f || Mathf.Abs(d.y) > 12f) continue;
                if (Vector3.Dot(Flat(d), f) < 0.85f) continue;
                return r;
            }
            return null;
        }

        /// <summary>A spot beside and beyond the ramp with a clear view of both the lip and the landing.</summary>
        Vector3 RampAnchor(Vector3 lip, Vector3 f)
        {
            Vector3 r = Vector3.Cross(Vector3.up, f), landing = lip + f * 40f + Vector3.up * 2f;
            foreach (float along in new[] { 26f, 14f, 38f })
                foreach (int sd in new[] { side, -side })
                    foreach (float h in new[] { 5f, 9f, 14f })
                        foreach (float off in new[] { 15f, 10f, 22f })
                        {
                            Vector3 c = lip + f * along + r * sd * off + Vector3.up * h;
                            if (Physics.CheckSphere(c, 0.8f, CameraMask, QueryTriggerInteraction.Ignore)) continue;
                            if (Physics.Linecast(c, lip + Vector3.up * 2f, CameraMask, QueryTriggerInteraction.Ignore)) continue;
                            if (Physics.Linecast(c, landing, CameraMask, QueryTriggerInteraction.Ignore)) continue;
                            side = sd; return c;
                        }
            return lip + f * 26f + r * side * 15f + Vector3.up * 14f;
        }

        void Direct(float dt)
        {
            var st = rm.State;
            if (st == RaceState.Intro || st == RaceState.Countdown) { DirectIntro(st); return; }

            if (heroFinishedAt < 0f && hero.Finished) { heroFinishedAt = Time.time; Log("HERO FINISHED"); }
            if (heroFinishedAt > 0f)
            {
                if (shot != Shot.Finish && shot != Shot.Celebrate) StartShot(Shot.Celebrate, hero, 6f);
                else if (shot == Shot.Finish && Time.time - heroFinishedAt > 1.8f) StartShot(Shot.Celebrate, hero, 6f);
                if (shot == Shot.Celebrate && shotT > shotDur) stopping = true;
                return;
            }

            // a hit near the camera: cut to it in slow motion
            if (pendingImpact != null && Time.time - pendingImpactAt < 0.3f && impactCooldown <= 0f && shot != Shot.Ramp && shot != Shot.Finish && shotT > 0.5f && Usable(pendingImpact))
            {
                StartShot(Shot.Impact, pendingImpact, 2.0f);
                impactCooldown = 5f; pendingImpact = null;
                slowTarget = 0.35f; slowLeft = 1.1f;
                return;
            }
            // the finish line on the hero's last lap
            if (lapLine != null && hero.Lap >= rm.totalLaps && shot != Shot.Finish && Usable(hero))
            {
                Vector3 d = lapLine.position - hero.transform.position;
                if (d.magnitude < 110f && Vector3.Dot(Flat(d), Flat(hero.Kart.Body.linearVelocity)) > 0.6f)
                {
                    StartShot(Shot.Finish, hero, 30f);
                    Vector3 f = Flat(hero.Kart.Body.linearVelocity);
                    anchor = lapLine.position + f * 10f + Vector3.Cross(Vector3.up, f) * side * 9f + Vector3.up * 1.6f;
                    if (Physics.Raycast(anchor + Vector3.up * 10f, Vector3.down, out var g, 30f, Layers.GroundMask, QueryTriggerInteraction.Ignore)) anchor.y = g.point.y + 1.6f;
                    return;
                }
            }
            // a jump or glider ramp coming up
            if (shot != Shot.Ramp && shot != Shot.Glide && shot != Shot.Impact && shot != Shot.Finish && shotT > 1.2f)
            {
                var ramp = RampAhead();
                if (ramp != null)
                {
                    rampUsed.Add(ramp);
                    StartShot(Shot.Ramp, subj, 7f);
                    Vector3 f = Flat(ramp.forward);
                    if (Vector3.Dot(f, Flat(subj.Kart.Body.linearVelocity)) < 0f) f = -f;
                    anchor = RampAnchor(ramp.position, f);
                    return;
                }
            }
            // a kart under a star or turned into a bullet near the camera
            if (powerShotCooldown <= 0f && shotT > 1.5f && shot != Shot.Ramp && shot != Shot.Glide && shot != Shot.Impact)
                foreach (var r in racers)
                {
                    if (!Usable(r) || !(r.Kart.State.Star || r.Kart.State.Bullet)) continue;
                    if ((r.transform.position - cam.transform.position).magnitude > 90f) continue;
                    powerShotCooldown = 9f;
                    StartShot(rng.NextDouble() < 0.5 ? Shot.Front : Shot.Side, r, 3.5f);
                    Log($"POWER {r.racerName} {(r.Kart.State.Star ? "Star" : "Bullet")}");
                    return;
                }
            if (subj != null && subj.Kart.State.Gliding && shot != Shot.Glide && shot != Shot.Impact && (shot != Shot.Ramp || shotT > 2.5f))
            {
                StartShot(Shot.Glide, subj, 5f); slowTarget = 0.55f; slowLeft = 1.2f; return;
            }
            bool done = shotT > shotDur || !Usable(subj);
            if (shot == Shot.Ramp && rampLanded > 0f && shotT - rampLanded > 0.8f) done = true;
            if (shot == Shot.Trackside && shotT > 1.2f && Vector3.Dot(subj.transform.position - anchor, Flat(subj.Kart.Body.linearVelocity)) > 14f) done = true;
            if (done) ChooseRacingShot();
        }

        void DirectIntro(RaceState st)
        {
            // intro (15 s): aerial descent 5.5 s, grid orbit 4 s, driver close-ups 2 x 2 s, grid sweep; countdown: low front shot through GO
            if (st == RaceState.Countdown)
            {
                if (shot != Shot.GridFront) StartShot(Shot.GridFront, hero, 99f);
                return;
            }
            if (shotT < shotDur) return;
            introStep++;
            switch (introStep)
            {
                case 1: StartShot(Shot.GridOrbit, hero, 3.5f); break;
                case 2: StartShot(Shot.DriverClose, hero, 2f); side = 1; break;
                case 3: StartShot(Shot.DriverClose, racers[(racers.IndexOf(hero) + 3) % racers.Count], 2f); break;
                default: StartShot(Shot.GridSweep, hero, 6f); break;
            }
        }

        // ---------------------------------------------------------------- items

        static readonly (ItemId id, float w)[] ItemPool =
        {
            (ItemId.GreenShell, 3f), (ItemId.TripleGreen, 2f), (ItemId.RedShell, 3f), (ItemId.TripleRed, 2f), (ItemId.Banana, 1.5f),
            (ItemId.BobOmb, 3.5f), (ItemId.Mushroom, 1.5f), (ItemId.TripleMushroom, 1f), (ItemId.Star, 1.5f), (ItemId.BulletBill, 1f), (ItemId.GoldenMushroom, 0.8f),
        };

        ItemId RandomItem(bool offensiveOnly)
        {
            float sum = 0f; foreach (var p in ItemPool) if (!offensiveOnly || IsOffensive(p.id)) sum += p.w;
            float pick = (float)rng.NextDouble() * sum;
            foreach (var p in ItemPool) { if (offensiveOnly && !IsOffensive(p.id)) continue; pick -= p.w; if (pick <= 0f) return p.id; }
            return ItemId.GreenShell;
        }

        static bool IsOffensive(ItemId id) => id == ItemId.GreenShell || id == ItemId.TripleGreen || id == ItemId.RedShell || id == ItemId.TripleRed || id == ItemId.BobOmb;

        void Items(float dt)
        {
            if (rm.State != RaceState.Racing || rm.RaceTime < 4f) return;
            itemTimer -= dt; heroItemTimer -= dt; blueCooldown -= dt;
            if (itemTimer <= 0f)
            {
                itemTimer = 1.8f + (float)rng.NextDouble() * 1.4f;
                RacerProgress best = null; float bestD = 90f;
                Vector3 c = subj != null ? subj.transform.position : hero.transform.position;
                foreach (var r in racers)
                {
                    if (r == hero || r.Finished || !Usable(r)) continue;
                    var h = r.GetComponent<ItemHolder>(); if (h == null || h.State != HolderState.Empty) continue;
                    float d = (r.transform.position - c).magnitude + (float)rng.NextDouble() * 25f;
                    if (d < bestD) { bestD = d; best = r; }
                }
                if (best != null)
                {
                    var id = blueCooldown <= 0f && rng.NextDouble() < 0.25 ? ItemId.BlueShell : RandomItem(false);
                    if (id == ItemId.BlueShell) blueCooldown = 45f;
                    best.GetComponent<ItemHolder>().Give(id);
                    Log($"GIVE {best.racerName} {id}");
                }
            }
            if (heroItemTimer <= 0f && Usable(hero) && !hero.Kart.State.Stunned)
            {
                heroItemTimer = 6f + (float)rng.NextDouble() * 3f;
                var h = hero.GetComponent<ItemHolder>();
                var id = RandomItem(rng.NextDouble() < 0.7);
                h.Give(id); h.Step(Time.fixedDeltaTime, true, false);
                Log($"HERO {id}");
            }
        }

        // ---------------------------------------------------------------- camera

        void PlaceCamera(float dt)
        {
            var t = subj.transform;
            Vector3 p = t.position;
            Vector3 v = subj.Kart.Body.linearVelocity;
            Vector3 heading = Flat(v.sqrMagnitude > 16f ? v : t.forward);
            smFwd = cutThisFrame ? heading : Vector3.Slerp(smFwd, heading, 1f - Mathf.Exp(-5f * dt));
            Vector3 f = smFwd, r = Vector3.Cross(Vector3.up, f), up = Vector3.up;
            float head = 1.2f;
            bool air = !subj.Kart.State.Grounded || subj.Kart.State.OnJumpPanel;
            Vector3 want, look; float wantFov; bool smooth = true, collide = true;
            float u01 = Mathf.Clamp01(shotT / Mathf.Max(0.01f, shotDur));

            switch (shot)
            {
                case Shot.Aerial:
                {
                    Vector3 a = gridPos - gridFwd * 150f + up * 75f + Vector3.Cross(up, gridFwd) * 40f, b = gridPos - gridFwd * 26f + up * 9f + Vector3.Cross(up, gridFwd) * 6f;
                    float e = Mathf.SmoothStep(0f, 1f, u01);
                    want = Vector3.Lerp(a, b, e); look = Vector3.Lerp(gridPos + gridFwd * 60f, gridPos + up * 1f, e); wantFov = 55f; smooth = false; collide = false; break;
                }
                case Shot.GridOrbit:
                    orbitAng += 14f * dt;
                    want = gridPos + Quaternion.AngleAxis(orbitAng, up) * (gridFwd * 30f) + up * 7f; look = gridPos + up * 1f; wantFov = 50f; smooth = false; break;
                case Shot.GridSweep:
                {
                    Vector3 gr = Vector3.Cross(up, gridFwd);
                    want = gridPos - gridFwd * Mathf.Lerp(28f, -6f, u01) + gr * 9f + up * 1.2f; look = gridPos - gridFwd * Mathf.Lerp(20f, -12f, u01) + up * 0.9f; wantFov = 45f; smooth = false; break;
                }
                case Shot.GridFront:
                    want = gridPos + gridFwd * 30f + up * 1.3f; look = gridPos + up * 1.0f; wantFov = 38f; smooth = false; collide = false;
                    if (rm.State == RaceState.Racing) { want = Vector3.Lerp(want, want + up * 5f, Mathf.Clamp01(rm.RaceTime / 2f)); if (rm.RaceTime > 3.2f) shotDur = 0f; }
                    if (rm.State == RaceState.Racing && rm.RaceTime > 3.2f) { ChooseRacingShot(); return; }
                    break;
                case Shot.DriverClose:
                    want = p + f * 3.6f + up * (head + 0.15f) + r * side * 1.1f; look = p + up * (head - 0.2f); wantFov = 30f; break;
                case Shot.ChaseLow:
                    want = p - f * 6f + up * 1.5f; look = p + f * 10f + up * 0.7f; wantFov = 64f; break;
                case Shot.ChaseHigh:
                    want = p - f * 13f + up * 6.5f; look = p + f * 12f; wantFov = 58f; break;
                case Shot.Front:
                    want = p + f * 8f + up * 1.2f + r * side * 1.6f; look = p + up * 0.9f - f * 2f; wantFov = 42f; break;
                case Shot.Side:
                    want = p + r * side * 6.5f + up * 1.1f + f * 1f; look = p + up * 0.8f + f * 1.5f; wantFov = 48f; break;
                case Shot.Wheel:
                    want = p + r * side * 1.9f + up * 0.45f - f * 1.4f; look = p + f * 14f + up * 0.6f; wantFov = 78f; break;
                case Shot.Orbit:
                    orbitAng += 38f * side * dt;
                    want = p + Quaternion.AngleAxis(orbitAng, up) * (f * -7f) + up * 2f; look = p + up * 0.9f; wantFov = 52f; break;
                case Shot.Overhead:
                    want = p + up * 24f - f * 7f; look = p + f * 4f; wantFov = 50f; break;
                case Shot.Crane:
                    want = p - f * Mathf.Lerp(5f, 15f, u01) + up * Mathf.Lerp(1.2f, 13f, u01); look = p + f * 6f; wantFov = 60f; break;
                case Shot.Trackside:
                case Shot.Ramp:
                case Shot.Finish:
                {
                    want = anchor; look = p + up * 0.9f; smooth = false; collide = false;
                    float dist = (p - anchor).magnitude;
                    // telephoto that keeps the kart about the same size in frame as it approaches
                    wantFov = shot == Shot.Ramp ? Mathf.Clamp(2f * Mathf.Atan(9f / dist) * Mathf.Rad2Deg, 22f, 70f)
                                                : Mathf.Clamp(2f * Mathf.Atan(6.5f / dist) * Mathf.Rad2Deg, 14f, 62f);
                    if (shot == Shot.Ramp)
                    {
                        if (air) { airT += dt; if (!wasAir) { slowTarget = 0.45f; slowLeft = 1.6f; Log("AIR"); } }
                        else if (wasAir && airT > 0.25f && rampLanded < 0f) { rampLanded = shotT; slowLeft = Mathf.Min(slowLeft, 0.15f); Log("LAND"); }
                    }
                    if (shot == Shot.Finish && lapLine != null && (lapLine.position - p).magnitude < 16f && slowTarget > 0.9f && !subj.Finished) { slowTarget = 0.4f; slowLeft = 0.9f; }
                    break;
                }
                case Shot.Glide:
                    want = p + r * side * 9f - f * 3f + up * 2f; look = p + f * 6f; wantFov = 55f; break;
                case Shot.Impact:
                    orbitAng += 22f * side * dt;
                    want = p + Quaternion.AngleAxis(orbitAng, up) * (r * side * 6f) - f * 1.5f + up * 2.4f; look = p + up * 1.2f; wantFov = 48f; break;
                case Shot.Celebrate:
                    orbitAng += 20f * dt;
                    want = p + Quaternion.AngleAxis(orbitAng, up) * (f * 6.5f) + up * 2.2f; look = p + up * 1.2f; wantFov = 45f; break;
                default:
                    want = p - f * 6f + up * 1.5f; look = p + f * 8f; wantFov = 60f; break;
            }
            wasAir = air;

            if (collide)
            {
                Vector3 from = p + up * 1.2f, d = want - from;
                if (Physics.SphereCast(from, 0.4f, d.normalized, out var hit, d.magnitude, CameraMask, QueryTriggerInteraction.Ignore))
                    want = from + d.normalized * Mathf.Max(1.2f, hit.distance - 0.1f);
            }
            if (cutThisFrame || !smooth) { camPos = want; lookPt = look; fov = wantFov; }
            else
            {
                float k = 1f - Mathf.Exp(-10f * dt);
                camPos = Vector3.Lerp(camPos, want, k); lookPt = Vector3.Lerp(lookPt, look, 1f - Mathf.Exp(-14f * dt)); fov = Mathf.Lerp(fov, wantFov, k);
            }
            if (!smooth) fov = Mathf.Lerp(fov, wantFov, cutThisFrame ? 1f : 1f - Mathf.Exp(-6f * dt));
            // never sit inside another kart or a thrown item: rise over it
            float needUp = 0f;
            foreach (var c in Physics.OverlapSphere(camPos + Vector3.up * avoidUp, 2f, (1 << Layers.Kart) | (1 << Layers.Item), QueryTriggerInteraction.Collide))
            {
                var owner = c.GetComponentInParent<RacerProgress>();
                if (owner == subj && shot != Shot.Impact && shot != Shot.Orbit) continue;
                needUp = Mathf.Max(needUp, 2.2f);
            }
            avoidUp = Mathf.Lerp(avoidUp, needUp, 1f - Mathf.Exp(-(needUp > avoidUp ? 14f : 3f) * dt));
            Vector3 finalPos = camPos + Vector3.up * avoidUp;
            cam.transform.SetPositionAndRotation(finalPos, Quaternion.LookRotation((lookPt - finalPos).normalized, Vector3.up));
            cam.fieldOfView = fov;
        }

        // ---------------------------------------------------------------- loop

        void LateUpdate()
        {
            if (!ready) { Init(); if (!ready) return; }
            float dt = Time.deltaTime;
            shotT += dt; impactCooldown -= dt; powerShotCooldown -= dt;
            if (slowLeft > 0f) { slowLeft -= dt; if (slowLeft <= 0f) slowTarget = 1f; }
            Time.timeScale = Mathf.MoveTowards(Time.timeScale, slowTarget, Time.unscaledDeltaTime / 0.25f);

            if (!stopping)
            {
                Direct(dt);
                Items(dt);
                if (Usable(subj)) PlaceCamera(dt);
                volume.weight = cutThisFrame ? 0f : 1f;       // no motion blur smear across a cut
                cutThisFrame = false;
                if (rm.RaceTime > 260f) stopping = true;
            }
            Capture();
            if (stopping && pendingReads == 0 && Interlocked.CompareExchange(ref pendingWrites, 0, 0) == 0)
            {
                Log("END");
                log.Close();
                Application.Quit();
                enabled = false;
            }
        }

        void Capture()
        {
            if (stopping) return;
            // keep the queues bounded: the frame rate of game time is fixed, so waiting here only slows the recording down
            while (Interlocked.CompareExchange(ref pendingWrites, 0, 0) > 24) Thread.Sleep(2);
            if (pendingReads > 6) AsyncGPUReadback.WaitAllRequests();
            int index = frame++;
            pendingReads++;
            AsyncGPUReadback.Request(rt, 0, TextureFormat.RGBA32, req =>
            {
                pendingReads--;
                if (req.hasError) return;
                var bytes = req.GetData<byte>().ToArray();
                Interlocked.Increment(ref pendingWrites);
                string path = Path.Combine(outDir, "frames", $"f_{index:000000}.jpg");
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { File.WriteAllBytes(path, ImageConversion.EncodeArrayToJPG(bytes, GraphicsFormat.R8G8B8A8_SRGB, W, H, 0, 93)); }
                    finally { Interlocked.Decrement(ref pendingWrites); }
                });
            });
        }
    }
}
