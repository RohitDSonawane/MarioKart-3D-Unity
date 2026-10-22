using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;

namespace HarborRacer.Inputs
{
    /// <summary>Per-racer AI personality.</summary>
    [System.Serializable]
    public class AIProfile
    {
        [Range(0.85f, 1.05f)] public float skill = 0.97f;          // multiplies the rubber-banded top speed
        [Range(0f, 1f)] public float aggression = 0.5f;             // item-use eagerness
        [Range(0f, 1f)] public float driftSkill = 0.8f;             // chance to drift through a marked corner
        public float lineOffset = 0f;                               // lateral offset from the racing line (units)
        [Range(0f, 1f)] public float rocketStartChance = 0.5f;

        /// <summary>Deterministic personality from a racer name so the field is stable between runs.</summary>
        public static AIProfile ForName(string name)
        {
            int h = 17; foreach (char c in name) h = h * 31 + c;
            var rnd = new System.Random(h);
            return new AIProfile
            {
                skill = 0.93f + (float)rnd.NextDouble() * 0.10f,
                aggression = 0.3f + (float)rnd.NextDouble() * 0.6f,
                driftSkill = 0.6f + (float)rnd.NextDouble() * 0.4f,
                lineOffset = ((float)rnd.NextDouble() - 0.5f) * 5f,
                rocketStartChance = 0.5f
            };
        }
    }

    /// <summary>
    /// Waypoint driver. With <see cref="racerAI"/> it is the opponent AI: pure pursuit along its racing line with a speed-based
    /// look-ahead and a personal lateral offset, corner speed limits, drifting through DriftLeft/DriftRight nodes (mini-turbos), tricks,
    /// avoidance of karts / items / hazards ahead, stuck recovery, a new line each lap and rubber-banding against the player.
    /// Without it, it is the plain follower used for the finish autopilot and the Bullet Bill.
    /// </summary>
    public class PathDriverInput : MonoBehaviour, IKartInput, IDrivenInput
    {
        public KartController kart;
        public TrackPath path;
        public int nodeIndex;
        public float reachRadius = 14f;
        public float cruiseSpeed = 85f;        // upper limit for the throttle logic
        public float steerAngleForFull = 20f;  // degrees to the target for full steer

        [Header("Opponent AI")]
        public bool racerAI;
        public AIProfile profile = new AIProfile();
        public TrackPath[] altPaths;           // lines to pick from at the start of each lap
        public bool rubberBand = true;

        public Vector3 AimPoint { get; private set; }     // lookahead point the kart is steering at
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }
        public float Pitch => 0f;
        public bool DriftHeld { get; private set; }
        public bool DriftPressed => driftPressed;
        public bool TrickPressed => trickPressed;
        public bool ItemPressed => false;
        public bool ItemBackPressed => false;
        public void EndFixedStep() { driftPressed = false; trickPressed = false; }

        public float forcedSpeed;               // > 0: Bullet Bill mode, full throttle with no corner slow-downs
        public float lookahead = 28f;
        public float panelApproachSpeed = 200f;      // no slow-down: aimed panels need the full run-up
        public float skipAfterSeconds = 4f;
        public float hopMinDistanceFromHuman = 90f;
        public float respawnAfterSeconds = 12f;

        JumpPanelZone[] jumpPanels = new JumpPanelZone[0];
        float stuckTimer, reverseTimer, noProgressTimer, bestNodeDist = float.MaxValue;
        int lastNode = -1;
        KartVisuals visuals;
        RacerProgress racer;

        // drift / trick
        bool driftPressed, trickPressed;
        int driftDir, driftHintNode = -1;
        float driftTime, trickDelay = -1f;
        bool trickHandled;
        System.Random rng = new System.Random(1);

        // avoidance / rubber band
        float avoidOffset, avoidTarget, avoidTimer, rubberTimer;

        void Awake()
        {
            if (kart == null) kart = GetComponent<KartController>();
            visuals = GetComponent<KartVisuals>();
            racer = GetComponent<RacerProgress>();
            jumpPanels = FindObjectsByType<JumpPanelZone>();
            racerAI = racer != null && !racer.isPlayer;
            if (racerAI)
            {
                if (profile == null || Mathf.Approximately(profile.lineOffset, 0f)) profile = AIProfile.ForName(racer.racerName);
                rng = new System.Random(racer.racerName.GetHashCode());
            }
            else rng = new System.Random(1);
        }

        KartRespawn respawnHook;

        void OnEnable()
        {
            if (racer != null) racer.LapCompleted += OnLap;
            respawnHook = GetComponent<KartRespawn>();
            if (respawnHook != null) respawnHook.Teleported += OnTeleported;
        }

        void OnDisable()
        {
            if (racer != null) racer.LapCompleted -= OnLap;
            if (respawnHook != null) respawnHook.Teleported -= OnTeleported;
        }

        /// <summary>the design notes §4.3: after a respawn the target node must follow the kart, not the spot it fell from.</summary>
        void OnTeleported() { if (racerAI) { SnapToPath(); avoidOffset = avoidTarget = 0f; avoidTimer = 0f; noProgressTimer = 0f; bestNodeDist = float.MaxValue; } }

        /// <summary>the design notes §9.2.8: every lap the AI picks a random racing line.</summary>
        void OnLap(int lap)
        {
            if (!racerAI || altPaths == null || altPaths.Length == 0 || forcedSpeed > 0f) return;
            path = altPaths[rng.Next(altPaths.Length)];
            SnapToPath();
        }

        /// <summary>Snap the target to the path node nearest ahead of the kart.</summary>
        public void SnapToPath()
        {
            if (path == null || path.Count == 0) return;
            float best = float.MaxValue; int bi = 0;
            for (int i = 0; i < path.Count; i++)
            {
                float d = (path.GetNode(i).position - transform.position).sqrMagnitude;
                if (d < best) { best = d; bi = i; }
            }
            nodeIndex = (bi + 1) % path.Count;
        }

        /// <summary>True when drivable ground lies within a few units below the point (no gap or edge there).</summary>
        static bool RoadUnder(Vector3 p, Vector3 up)
        {
            return Physics.Raycast(p + up * 3f, -up, 9f, Layers.GroundMask, QueryTriggerInteraction.Ignore);
        }

        DriftHint HintAt(int i)
        {
            var wp = path.GetNode(i).GetComponent<Waypoint>();
            return wp != null ? wp.driftHint : DriftHint.None;
        }

        void SkipStuckNode()
        {
            // The hop goes to the point of the racing line just ahead of the kart, never to a far-away node (the nodes can be 150-300 u apart,
            // which made a wedged kart vanish and reappear far down the track). The node is only skipped when the kart is already next to it.
            int n = path.Count;
            if ((path.GetNode(nodeIndex).position - transform.position).magnitude < 25f) nodeIndex = (nodeIndex + 1) % n;
            Vector3 segEnd = path.GetNode(nodeIndex).position, segStart = path.GetNode((nodeIndex - 1 + n) % n).position;
            Vector3 segDir = Vector3.ProjectOnPlane(segEnd - segStart, Vector3.up);
            float segLen = segDir.magnitude;
            segDir = segLen > 0.01f ? segDir / segLen : transform.forward;
            float along = Mathf.Clamp(Vector3.Dot(Vector3.ProjectOnPlane(transform.position - segStart, Vector3.up), segDir), 0f, segLen);
            Vector3 p = segStart + segDir * Mathf.Min(along + 10f, segLen);
            p = transform.position + Vector3.ClampMagnitude(p - transform.position, 30f);   // a recovery hop is a short nudge, not a jump down the track
            bool nearPlayer = false;
            foreach (var r in Object.FindObjectsByType<RacerProgress>())
            {
                if (r.isPlayer && (r.transform.position - p).magnitude < hopMinDistanceFromHuman)
                {
                    nearPlayer = true;
                    break;
                }
            }
            if (nearPlayer)
            {
                // If near player, nudge forward in the path direction to unwedge cleanly
                Vector3 nudgeDir = Vector3.ProjectOnPlane(p - transform.position, Vector3.up).normalized;
                kart.Body.linearVelocity = nudgeDir * 25f;
                kart.CurrentSpeed = 25f;
                return;
            }
            if (!Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore)) return;
            Vector3 dir = segDir;
            kart.Body.position = hit.point + hit.normal * 1.2f;
            kart.Body.rotation = Quaternion.LookRotation(dir, hit.normal);
            kart.transform.SetPositionAndRotation(kart.Body.position, kart.Body.rotation);
            kart.Body.linearVelocity = dir * 30f; kart.CurrentSpeed = 30f;
        }

        public void Tick(float dt)
        {
            if (path == null || path.Count == 0 || kart == null) { Throttle = Brake = Steer = 0f; return; }

            Vector3 pos = transform.position, fwd = transform.forward, up = transform.up;
            float speed = kart.ForwardSpeed;
            bool ai = racerAI && forcedSpeed <= 0f;

            // advance when close, or when the node is behind us and reasonably near
            for (int guard = 0; guard < 4; guard++)
            {
                Vector3 to = path.GetNode(nodeIndex).position - pos;
                if (to.magnitude < reachRadius || (Vector3.Dot(to, fwd) < 0f && to.magnitude < reachRadius * 3f))
                    nodeIndex = (nodeIndex + 1) % path.Count;
                else break;
            }

            // pure pursuit along the segment: aim a speed-based distance ahead of the kart's projection on the line
            Vector3 target = path.GetNode(nodeIndex).position;
            Vector3 prevNode = path.GetNode((nodeIndex - 1 + path.Count) % path.Count).position;
            Vector3 seg = target - prevNode; float segLen = seg.magnitude;
            Vector3 right = Vector3.Cross(up, fwd);
            if (segLen > 1f)
            {
                Vector3 sd = seg / segLen;
                float along = Mathf.Clamp(Vector3.Dot(pos - prevNode, sd), 0f, segLen);
                float lead = Mathf.Min(along + (ai ? Mathf.Clamp(speed * 0.4f, 14f, 34f) : lookahead), segLen);
                target = prevNode + sd * lead;
                right = Vector3.Cross(Vector3.up, sd);
            }
            if (ai)
            {
                float lateral = profile.lineOffset + UpdateAvoidance(dt, pos, fwd, up, speed);
                // lane offsets and sidesteps must never aim at a spot with no road under it (the narrow entrance of the overpass ramp,
                // bridge edges): the kart would be steered off the deck. Fall back to the centre of the line there.
                if (Mathf.Abs(lateral) > 0.01f && !RoadUnder(target + right * lateral, up) && !RoadUnder(target + right * lateral * 0.5f, up)) lateral = 0f;
                target += right * lateral;
            }
            AimPoint = target;

            float ang = Vector3.SignedAngle(fwd, Vector3.ProjectOnPlane(target - pos, up), up);
            float steer = Mathf.Clamp(ang / steerAngleForFull, -1f, 1f);

            // corner anticipation: look at the turn angle at each of the next nodes and brake early enough to take it at a safe speed
            float desired = cruiseSpeed;
            Vector3 from = pos; float along2 = 0f;
            for (int i = 0; i < 5; i++)
            {
                int ni = (nodeIndex + i) % path.Count;
                Vector3 np = path.GetNode(ni).position;
                along2 += Vector3.ProjectOnPlane(np - from, up).magnitude;
                Vector3 inDir = Vector3.ProjectOnPlane(np - path.GetNode((ni - 1 + path.Count) % path.Count).position, up);
                Vector3 outDir = Vector3.ProjectOnPlane(path.GetNode((ni + 1) % path.Count).position - np, up);
                float turn = Vector3.Angle(inDir, outDir);
                float limit = turn > 85f ? 26f : turn > 65f ? 38f : turn > 48f ? 50f : turn > 32f ? 64f : cruiseSpeed;
                float brakeDist = limit >= cruiseSpeed ? 0f : Mathf.Max(0f, (speed * speed - limit * limit) / (2f * 55f)) + 10f;
                if (along2 < brakeDist || (i == 0 && limit < desired && along2 < 14f)) desired = Mathf.Min(desired, limit);
                from = np;
            }
            foreach (var jp in jumpPanels)
            {
                if (jp == null) continue;
                Vector3 toPanel = jp.transform.position - pos;
                if (toPanel.magnitude < 60f && Vector3.Dot(toPanel, fwd) > -5f) { desired = Mathf.Min(desired, panelApproachSpeed); break; }
            }
            // Harbor ramp descent corner (nodes 16-20 near 196,-22,-79): limit speed to prevent overshooting seawall
            if (ai && nodeIndex >= 16 && nodeIndex <= 20)
            {
                desired = Mathf.Min(desired, 42f);
            }
            if (forcedSpeed > 0f) desired = forcedSpeed;
            // a vertical face dead ahead (a building corner the line cuts across): ease off so the turn can still clear it
            if (ai && speed > 30f && Physics.SphereCast(pos + up * 1.2f, 0.9f, fwd, out var faceHit, 8f + speed * 0.3f, (1 << Layers.Wall) | (1 << Layers.Drivable), QueryTriggerInteraction.Ignore)
                && faceHit.normal.y < 0.55f && faceHit.collider.GetComponentInParent<KartController>() == null)
                desired = Mathf.Min(desired, Mathf.Max(30f, speed - 25f));
            bool drifting = ai && DriftHeld;
            if (drifting) desired = Mathf.Max(desired, 60f);               // keep the speed up through a drift (throttle stays on below)
            float throttle = (speed < desired || drifting) ? 1f : 0f;     // lifting off cancels a drift
            float brake = forcedSpeed > 0f || drifting ? 0f : (speed > desired + 10f ? 1f : 0f);

            if (ai)
            {
                StepRubberBand(dt);
                steer = StepDrift(dt, steer, speed, ang);
                StepTrick(dt);
            }

            // stuck recovery: reverse out for a moment
            // never back up toward a drop: with no road behind the kart, stay put and let the progress check nudge it instead
            if (reverseTimer > 0f && !(RoadUnder(pos - fwd * 5f, up) && RoadUnder(pos - fwd * 9f, up))) reverseTimer = 0f;
            if (reverseTimer > 0f)
            {
                reverseTimer -= dt;
                throttle = 0f; brake = 1f;
                // reversing turns the nose the same way the steering says, so back away while swinging it toward the node
                steer = Mathf.Abs(ang) > 3f ? Mathf.Sign(ang) : -Mathf.Sign(steer == 0f ? 1f : steer);
                DriftHeld = false;
            }
            else if (speed < 3f && !kart.controlsLocked)
            {
                stuckTimer += dt;
                if (stuckTimer > 1.5f) { reverseTimer = 1.2f; stuckTimer = 0f; }
            }
            else stuckTimer = 0f;

            // no node progress for a long time (wedged on a ramp, wrong geometry): skip the node, and if nobody can see it, hop there
            // "no progress" means the kart stopped getting closer to its node. A slow start or a slow ramp is not stuck: the first nodes
            // of a line are about 150 u apart, so a standing start needs 4-5 s to reach one and used to be hopped forward at race time 4 s.
            float nodeDist = (path.GetNode(nodeIndex).position - pos).magnitude;
            if (nodeIndex != lastNode) { lastNode = nodeIndex; noProgressTimer = 0f; bestNodeDist = nodeDist; }
            else if (nodeDist < bestNodeDist - 3f) { bestNodeDist = nodeDist; noProgressTimer = 0f; }
            else if (!kart.controlsLocked && !kart.Respawn.Respawning)
            {
                noProgressTimer += dt;
                if (noProgressTimer > skipAfterSeconds) { SkipStuckNode(); noProgressTimer = skipAfterSeconds * 0.5f; bestNodeDist = float.MaxValue; }
                if (ai && noProgressTimer > respawnAfterSeconds) { kart.Respawn.Begin(RespawnReason.OutOfBounds); noProgressTimer = 0f; }
            }

            if (forcedSpeed > 0f) { steer = 0f; reverseTimer = 0f; }   // the bullet rotates itself (ItemHolder)
            Throttle = throttle; Brake = brake; Steer = steer;
            if (visuals != null) visuals.SetSteer(steer);
        }


        // ---- opponent behaviours ----

        /// <summary>the design notes §9.4: a forward sphere-cast over karts, hazards and items; on a hit shift sideways for ~0.6 s.</summary>
        float UpdateAvoidance(float dt, Vector3 pos, Vector3 fwd, Vector3 up, float speed)
        {
            if (avoidTimer > 0f) avoidTimer -= dt; else avoidTarget = 0f;
            int mask = (1 << Layers.Kart) | (1 << Layers.Hazard) | (1 << Layers.Item);
            if (avoidTimer <= 0f && speed > 15f &&
                Physics.SphereCast(pos + up * 0.6f, 1.4f, fwd, out var hit, 16f + speed * 0.15f, mask, QueryTriggerInteraction.Ignore))
            {
                var trolley = hit.collider.GetComponentInParent<HarborRacer.Hazards.Trolley>();
                if (trolley != null)
                {
                    float side = Vector3.Dot(trolley.transform.position - pos, Vector3.Cross(up, fwd));
                    float away = side >= 0f ? -1f : 1f;
                    avoidTarget = away * 7.5f;
                    avoidTimer = 1.6f;
                }
                else
                {
                    var other = hit.collider.GetComponentInParent<KartController>();
                    if (other != kart)
                    {
                        float side = Vector3.Dot(hit.collider.bounds.center - pos, Vector3.Cross(up, fwd));
                        float away = side > 0.3f ? -1f : side < -0.3f ? 1f : (rng.Next(2) == 0 ? -1f : 1f);
                        avoidTarget = away * 3.2f; avoidTimer = 0.6f;
                    }
                }
            }
            // static obstacles (curbs, rails, building corners the sparse target nodes cut across): slide away from a vertical face in the lane
            if (avoidTimer <= 0f && speed > 20f)
            {
                float range = 10f + speed * 0.35f;
                var hits = Physics.SphereCastAll(pos + up * 1.7f, 1.0f, fwd, range, (1 << Layers.Wall) | (1 << Layers.Drivable), QueryTriggerInteraction.Ignore);
                RaycastHit best = default; float bestD = float.MaxValue;
                foreach (var h in hits)
                    if (h.distance > 0.01f && h.normal.y < 0.55f && h.distance < bestD && h.collider.GetComponentInParent<KartController>() == null) { best = h; bestD = h.distance; }
                if (bestD < float.MaxValue)
                {
                    float side = Vector3.Dot(best.point - pos, Vector3.Cross(up, fwd));
                    float away = side >= 0f ? -1f : 1f;
                    avoidTarget = away * Mathf.Lerp(8f, 4f, bestD / range);
                    avoidTimer = 0.5f;
                }
            }
            avoidOffset = Mathf.MoveTowards(avoidOffset, avoidTarget, 14f * dt);
            return avoidOffset;
        }

        /// <summary>the design notes §9.3: scale the top speed by how far ahead of / behind the player this racer is (checkpoints).</summary>
        // top speed 90 normally (about 1.06 x the player's 85), 55 (0.65) once 15+ checkpoints ahead of the player.
        // Lower baseScale to make the AI easier, e.g. 0.96 keeps it a little slower than the player.
        public float baseScale = 1.04f, aheadScale = 0.9f, farAheadScale = 0.65f;

        void StepRubberBand(float dt)
        {
            rubberTimer -= dt;
            if (rubberTimer > 0f) return;
            rubberTimer = 0.5f;
            float scale = 1f;
            var rm = RaceManager.Instance;
            if (rubberBand && rm != null && rm.Player != null && racer != null && !rm.Player.Finished)
            {
                int gap = racer.CheckpointsPassed - rm.Player.CheckpointsPassed;
                scale = gap >= 15 ? farAheadScale : gap >= 5 ? aheadScale : baseScale;
            }
            kart.AiSpeedScale = Mathf.Lerp(kart.AiSpeedScale, scale * profile.skill, 0.5f);
        }

        /// <summary>the design notes §9.2.4: start a drift on the approach to a DriftLeft / DriftRight node, release after the last one.</summary>
        float StepDrift(float dt, float steer, float speed, float angToAim)
        {
            var st = kart.State;
            if (DriftHeld)
            {
                driftTime += dt;
                bool passed = HintAt(nodeIndex) == DriftHint.None && Vector3.Distance(path.GetNode((nodeIndex - 1 + path.Count) % path.Count).position, transform.position) < 40f
                              || driftTime > 5f;
                bool broken = speed < 35f || st.Gliding || st.OnJumpPanel || st.Stunned || kart.Respawn.Respawning;
                // the line turns away from the drift: the slide is carrying the kart past it (into the wall at the end of the corner)
                bool overshoot = driftDir != 0 && angToAim * driftDir < -12f && driftTime > 0.4f;
                // a vertical face dead ahead (building corner the line cuts): give up the slide while there is still room to turn
                bool wallAhead = speed > 20f && Physics.SphereCast(transform.position + transform.up * 1.2f, 0.9f, transform.forward, out var wallHit, 7f + speed * 0.22f,
                    (1 << Layers.Wall) | (1 << Layers.Drivable), QueryTriggerInteraction.Ignore) && wallHit.normal.y < 0.55f && wallHit.collider.GetComponentInParent<KartController>() == null;
                if ((passed && driftTime > 1.6f) || broken || overshoot || wallAhead) { DriftHeld = false; driftDir = 0; driftTime = 0f; }
                else if (driftDir != 0 && steer * driftDir < 0.3f) steer = driftDir * 0.3f;     // always lean into the drift
                return steer;
            }

            var hint = HintAt(nodeIndex);
            if (hint != DriftHint.None && driftHintNode != nodeIndex && speed > 48f && st.Grounded && !st.Stunned && !st.Gliding && !st.OnJumpPanel)
            {
                driftHintNode = nodeIndex;                                       // decide once per node
                float dist = Vector3.Distance(path.GetNode(nodeIndex).position, transform.position);
                if (dist < 45f && SafeToDrift(path.GetNode(nodeIndex).position) && rng.NextDouble() < profile.driftSkill)
                {
                    // the label says Left / Right; the geometry decides (the labels do not always agree with the turn the line really makes)
                    int ni = nodeIndex, n = path.Count;
                    float turn = Vector3.SignedAngle(
                        Vector3.ProjectOnPlane(path.GetNode(ni).position - path.GetNode((ni - 1 + n) % n).position, Vector3.up),
                        Vector3.ProjectOnPlane(path.GetNode((ni + 1) % n).position - path.GetNode(ni).position, Vector3.up), Vector3.up);
                    if (Mathf.Abs(turn) < 20f) return steer;
                    DriftHeld = true; driftPressed = true; driftTime = 0f;
                    driftDir = turn > 0f ? 1 : -1;
                    steer = driftDir * Mathf.Max(0.6f, Mathf.Abs(steer));
                }
            }
            return steer;
        }

        /// <summary>No drifting on raised or narrow structures (overpass, ramps): the slide and the boost on release carry the kart off the edge.</summary>
        bool SafeToDrift(Vector3 nodePos)
        {
            if (nodeIndex >= 16 && nodeIndex <= 20) return false;
            if (!Physics.Raycast(nodePos + Vector3.up * 2f, Vector3.down, out var h, 40f, Layers.GroundMask, QueryTriggerInteraction.Ignore)) return false;
            if (nodePos.y - h.point.y > 4.5f) return false;                        // node floats above the road: a jump / ramp
            // lower road close below the line = we are on a bridge
            Vector3 below = h.point + Vector3.down * 1.5f;
            if (Physics.Raycast(below, Vector3.down, out var h2, 25f, Layers.GroundMask, QueryTriggerInteraction.Ignore)) return false;
            return true;
        }

        /// <summary>the design notes §9.2.5: inside a trick zone press Trick after a short reaction delay, 70 % of the time.</summary>
        void StepTrick(float dt)
        {
            if (!kart.State.TrickAvailable) { trickDelay = -1f; trickHandled = false; return; }
            if (trickHandled) return;
            if (trickDelay < 0f) trickDelay = 0.05f + (float)rng.NextDouble() * 0.2f;
            trickDelay -= dt;
            if (trickDelay <= 0f)
            {
                trickHandled = true;
                if (rng.NextDouble() < 0.7) trickPressed = true;
            }
        }
    }
}
