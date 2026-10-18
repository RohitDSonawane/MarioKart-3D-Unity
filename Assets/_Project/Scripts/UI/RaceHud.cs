using System.Collections.Generic;
using HarborRacer.Items;
using HarborRacer.Kart;
using HarborRacer.Race;
using HarborRacer.Track;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HarborRacer.UI
{
    /// <summary>
    /// The whole race HUD, built from code on a 1920x1080 target canvas: item slot with roulette, coins, lap,
    /// position, race timer, minimap, countdown, FINAL LAP banner, warnings, wrong way, respawn fade, finish banner, results table and
    /// the pause menu. Replaces the IMGUI RaceDebugHUD overlay.
    /// </summary>
    public class RaceHud : MonoBehaviour
    {
        public static RaceHud Instance { get; private set; }

        static readonly int[] Points = { 15, 12, 10, 9, 8, 7, 6, 5 };
        const float MapSize = 300f;

        Canvas canvas;
        RaceManager rm;
        RacerProgress player;
        KartController kart;
        ItemHolder holder;
        CoinWallet wallet;

        // widgets
        Image itemFrame, itemIcon, coinIcon, hudPlate, flagIcon; Text itemName, itemPips, itemTimer, itemBehind;
        Text coinText, lapText, posNumber, posSuffix, timerText, splitText, countdownText, wrongWay, warning, finishText, titleText, finalLapText, skipHint;
        Image fade;
        RectTransform mapRoot; RawImage mapImage;
        readonly Dictionary<RacerProgress, RectTransform> dots = new Dictionary<RacerProgress, RectTransform>();
        RectTransform blueDot;
        GameObject resultsPanel, pausePanel;

        // sprites
        static readonly Dictionary<ItemId, Sprite> ItemSprites = new Dictionary<ItemId, Sprite>();
        static Sprite CoinSprite;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { ItemSprites.Clear(); CoinSprite = null; }      // statics survive Play when domain reload is off

        static Sprite GetItemSprite(ItemId id)
        {
            if (id == ItemId.None) return null;
            if (ItemSprites.TryGetValue(id, out var s)) return s;
            string name = id switch
            {
                ItemId.Banana => "Banana",
                ItemId.TripleBanana => "TripleBananas",
                ItemId.GreenShell => "GreenShell",
                ItemId.TripleGreen => "TripleGreenShell",
                ItemId.RedShell => "RedShell",
                ItemId.TripleRed => "TripleRedShell",
                ItemId.BlueShell => "BlueShell",
                ItemId.BobOmb => "Bobomb",
                ItemId.BulletBill => "Bullet",
                ItemId.Coin => "Coin",
                ItemId.Mushroom => "Mushroom",
                ItemId.TripleMushroom => "TripleMushroom",
                ItemId.GoldenMushroom => "MushroomKing",
                ItemId.Star => "Star",
                _ => null
            };
            if (name != null)
            {
                s = Resources.Load<Sprite>($"UI/Items/{name}");
                if (s == null)
                {
                    var tex = Resources.Load<Texture2D>($"UI/Items/{name}");
                    if (tex != null) s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
            }
            if (s != null) ItemSprites[id] = s;          // never cache a miss: a failed lookup must be retried
            return s;
        }

        static Sprite GetCoinSprite()
        {
            if (CoinSprite != null) return CoinSprite;
            CoinSprite = Resources.Load<Sprite>("UI/CoinIcon");
            if (CoinSprite == null)
            {
                var tex = Resources.Load<Texture2D>("UI/CoinIcon");
                if (tex != null) CoinSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
            return CoinSprite;
        }

        // map
        Vector2 mapMin, mapMax; bool mapReady;

        // animation state
        float countdownT, goT, posPunch, coinPunch, itemPunch, finalLapT = -1f, splitT, finishT, resultsT, spinT, paused_;
        int lastPosition, lastCoins = -1, lastCountdown = -1;
        bool improved, resultsBuilt, isPaused;
        float lapStart; int lastLap = 1;
        readonly List<RectTransform> resultRows = new List<RectTransform>();
        readonly List<Text> pointTexts = new List<Text>();

        void Awake() { Instance = this; }
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (isPaused) { Time.timeScale = 1f; AudioListener.pause = false; }
            Unhook();
        }

        void Start()
        {
            BuildCanvas();
            EnsureEventSystem();
            BuildWidgets();
        }

        // ---------------------------------------------------------------- build

        void BuildCanvas()
        {
            var go = new GameObject("RaceHudCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
        }

        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>(); es.AddComponent<InputSystemUIInputModule>();
        }

        void BuildWidgets()
        {
            var c = canvas.transform;
            Vector2 TL = new Vector2(0, 1), TR = new Vector2(1, 1), BL = new Vector2(0, 0), BR = new Vector2(1, 0), TC = new Vector2(0.5f, 1), MC = new Vector2(0.5f, 0.5f), MR = new Vector2(1, 0.5f);

            // item slot (top-left)
            itemFrame = UiKit.Panel("ItemFrame", c, TL, TL, new Vector2(40, -40), new Vector2(170, 170), new Color(0f, 0f, 0f, 0.55f));
            itemIcon = UiKit.Panel("ItemIcon", itemFrame.transform, MC, MC, Vector2.zero, new Vector2(130, 130), new Color(1, 1, 1, 0f));
            itemName = UiKit.Label("ItemName", itemFrame.transform, MC, MC, Vector2.zero, new Vector2(160, 130), "", 26, Color.white);
            itemPips = UiKit.Label("ItemPips", itemFrame.transform, new Vector2(0.5f, 0), MC, new Vector2(0, -18), new Vector2(170, 30), "", 30, Color.white);
            itemTimer = UiKit.Label("ItemTimer", itemFrame.transform, new Vector2(1, 1), MC, new Vector2(-6, -6), new Vector2(100, 30), "", 24, new Color(1f, 0.85f, 0.2f), TextAnchor.UpperRight);
            itemBehind = UiKit.Label("ItemBehind", itemFrame.transform, new Vector2(0.5f, 1), MC, new Vector2(0, 16), new Vector2(170, 30), "", 22, new Color(0.6f, 0.9f, 1f));

            // timer + split (top-right)
            timerText = UiKit.Label("Timer", c, TR, TR, new Vector2(-40, -36), new Vector2(420, 70), "00:00.00", 64, Color.white, TextAnchor.MiddleRight, true, UiKit.DigitFont);
            splitText = UiKit.Label("Split", c, TR, TR, new Vector2(-40, -92), new Vector2(420, 40), "", 30, new Color(1f, 0.9f, 0.3f), TextAnchor.MiddleRight);

            // lap + coins (bottom-left)
            // target-style plate: coin + count, checkered flag + lap (parallelogram sprite, DS-DIGIT numerals)
            hudPlate = UiKit.Panel("HudPlate", c, BL, BL, new Vector2(30, 28), new Vector2(580, 124), new Color(0f, 0f, 0f, 0.62f));
            var plateSprite = UiKit.LoadSprite("UI/Plates/Parallelogram"); if (plateSprite != null) hudPlate.sprite = plateSprite;
            coinIcon = UiKit.Panel("CoinIcon", c, BL, BL, new Vector2(66, 48), new Vector2(72, 72), Color.white);
            var cs = GetCoinSprite();
            if (cs != null) coinIcon.sprite = cs; else coinIcon.color = new Color(1f, 0.85f, 0.15f);
            coinText = UiKit.Label("Coins", c, BL, BL, new Vector2(150, 44), new Vector2(150, 80), "00", 76, Color.white, TextAnchor.MiddleLeft, true, UiKit.DigitFont);
            flagIcon = UiKit.Panel("FlagIcon", c, BL, BL, new Vector2(318, 48), new Vector2(72, 72), Color.white);
            var fs = UiKit.LoadSprite("UI/Plates/Flag"); if (fs != null) flagIcon.sprite = fs; else flagIcon.color = new Color(1, 1, 1, 0f);
            lapText = UiKit.Label("Lap", c, BL, BL, new Vector2(402, 44), new Vector2(190, 80), "1/3", 76, Color.white, TextAnchor.MiddleLeft, true, UiKit.DigitFont);

            // position (bottom-right)
            posNumber = UiKit.Label("PosNumber", c, BR, BR, new Vector2(-110, 30), new Vector2(200, 200), "1", 190, Color.white, TextAnchor.LowerRight);
            posSuffix = UiKit.Label("PosSuffix", c, BR, BR, new Vector2(-40, 120), new Vector2(120, 80), "st", 56, Color.white, TextAnchor.LowerLeft);

            // minimap (right)
            var mapBack = UiKit.Panel("MapBack", c, MR, MR, new Vector2(-30, 20), new Vector2(MapSize + 16, MapSize + 16), new Color(0f, 0f, 0f, 0.5f));
            mapRoot = UiKit.Rect("MapRoot", mapBack.transform, MC, MC, Vector2.zero, new Vector2(MapSize, MapSize));
            mapImage = mapRoot.gameObject.AddComponent<RawImage>(); mapImage.raycastTarget = false; mapImage.color = new Color(1, 1, 1, 0);
            blueDot = UiKit.Panel("BlueShellDot", mapRoot, MC, MC, Vector2.zero, new Vector2(16, 16), new Color(0.2f, 0.4f, 1f)).rectTransform;
            blueDot.gameObject.SetActive(false);

            // centre messages
            countdownText = UiKit.Label("Countdown", c, new Vector2(0.5f, 0.62f), MC, Vector2.zero, new Vector2(800, 300), "", 260, Color.white);
            skipHint = UiKit.Label("SkipHint", c, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(700, 40), "Press Enter / Start to skip", 26, new Color(1f, 1f, 1f, 0.8f));
            titleText = UiKit.Label("Title", c, new Vector2(0.5f, 0.7f), MC, Vector2.zero, new Vector2(1400, 160), "TOAD HARBOR", 130, Color.white);
            finalLapText = UiKit.Label("FinalLap", c, new Vector2(0.5f, 0.75f), MC, new Vector2(2400, 0), new Vector2(1400, 160), "FINAL LAP", 140, new Color(1f, 0.85f, 0.15f));
            wrongWay = UiKit.Label("WrongWay", c, new Vector2(0.5f, 0.55f), MC, Vector2.zero, new Vector2(1200, 160), "WRONG WAY", 120, new Color(1f, 0.15f, 0.1f));
            warning = UiKit.Label("Warning", c, TC, TC, new Vector2(0, -130), new Vector2(1200, 80), "", 56, Color.red);
            finishText = UiKit.Label("Finish", c, new Vector2(0.5f, 0.62f), MC, Vector2.zero, new Vector2(1200, 300), "FINISH!", 220, new Color(1f, 0.9f, 0.2f));
            wrongWay.gameObject.SetActive(false); finishText.gameObject.SetActive(false); finalLapText.gameObject.SetActive(false);

            // respawn fade
            fade = UiKit.Stretch("Fade", c).gameObject.AddComponent<Image>();
            fade.color = new Color(0, 0, 0, 0); fade.raycastTarget = false;

            BuildPause(c);
        }

        void BuildPause(Transform c)
        {
            var p = UiKit.Stretch("Pause", c).gameObject.AddComponent<Image>();
            p.color = new Color(0, 0, 0, 0.7f);
            pausePanel = p.gameObject;
            UiKit.Label("PauseTitle", p.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 240), new Vector2(800, 120), "PAUSED", 110, Color.white);
            UiKit.MakeButton("Resume", p.transform, new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(460, 90), "Resume", () => SetPaused(false));
            UiKit.MakeButton("Restart", p.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(460, 90), "Restart", () => { SetPaused(false); if (rm != null) rm.Restart(); });
            UiKit.MakeButton("Quit", p.transform, new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(460, 90), "Quit", QuitGame);
            pausePanel.SetActive(false);
        }

        static void QuitGame()
        {
            Time.timeScale = 1f; AudioListener.pause = false;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void SetPaused(bool value)
        {
            isPaused = value;
            Time.timeScale = value ? 0f : 1f;
            AudioListener.pause = value;
            if (pausePanel != null) pausePanel.SetActive(value);
        }

        // ---------------------------------------------------------------- binding

        void Bind()
        {
            player = rm.Player;
            kart = player.Kart;
            holder = player.GetComponent<ItemHolder>();
            wallet = player.GetComponent<CoinWallet>();
            rm.CountdownTick += OnCountdown;
            rm.FinalLapStarted += OnFinalLap;
            rm.LapCompleted += OnLapCompleted;
            rm.RaceCompleted += OnRaceCompleted;
            player.OnFinished += OnPlayerFinished;
            if (kart.HitReaction != null) kart.HitReaction.Hit += OnHit;
            if (holder != null) holder.ItemChanged += OnItemChanged;
            BakeMap();
            foreach (var r in rm.Racers) MakeDot(r);
            if (player != null && dots.TryGetValue(player, out var pd)) pd.SetAsLastSibling();
            lastPosition = player.Position;
        }

        void Unhook()
        {
            if (rm == null || player == null) return;
            rm.CountdownTick -= OnCountdown; rm.FinalLapStarted -= OnFinalLap; rm.LapCompleted -= OnLapCompleted; rm.RaceCompleted -= OnRaceCompleted;
            player.OnFinished -= OnPlayerFinished;
            if (kart != null && kart.HitReaction != null) kart.HitReaction.Hit -= OnHit;
            if (holder != null) holder.ItemChanged -= OnItemChanged;
        }

        void OnCountdown(int v)
        {
            lastCountdown = v; countdownT = 0f;
            countdownText.text = v > 0 ? v.ToString() : "GO!";
            countdownText.color = v > 0 ? Color.white : new Color(0.3f, 1f, 0.3f);
            if (v == 0) goT = 1f;
        }
        void OnFinalLap() { finalLapT = 0f; finalLapText.gameObject.SetActive(true); }
        void OnLapCompleted(RacerProgress r, int lap)
        {
            if (r != player) return;
            float lapTime = rm.RaceTime - lapStart; lapStart = rm.RaceTime;
            splitText.text = $"LAP {lap}  {UiKit.Time(lapTime)}"; splitT = 3f;
        }
        void OnRaceCompleted() { resultsT = 0f; }
        void OnPlayerFinished() { finishT = 2.8f; finishText.gameObject.SetActive(true); }
        void OnHit(HitType t) { if (t != HitType.Bump) spinT = 0.7f; }
        void OnItemChanged() { itemPunch = 0.35f; }

        // ---------------------------------------------------------------- minimap

        void BakeMap()
        {
            var markers = GameObject.Find("Markers/Paths");
            if (markers == null) return;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            float maxY = -1e9f;
            foreach (var p in markers.GetComponentsInChildren<Transform>())
            {
                var pos = p.position;
                min = Vector2.Min(min, new Vector2(pos.x, pos.z)); max = Vector2.Max(max, new Vector2(pos.x, pos.z)); maxY = Mathf.Max(maxY, pos.y);
            }
            float pad = 60f;
            min -= Vector2.one * pad; max += Vector2.one * pad;
            // keep the map square so the road is not distorted
            Vector2 size = max - min; float side = Mathf.Max(size.x, size.y); Vector2 center = (min + max) * 0.5f;
            mapMin = center - Vector2.one * side * 0.5f; mapMax = center + Vector2.one * side * 0.5f;

            const int N = 192;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            float rayTop = maxY + 250f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float wx = Mathf.Lerp(mapMin.x, mapMax.x, (x + 0.5f) / N), wz = Mathf.Lerp(mapMin.y, mapMax.y, (y + 0.5f) / N);
                    Color32 col = new Color32(0, 0, 0, 0);
                    if (Physics.Raycast(new Vector3(wx, rayTop, wz), Vector3.down, out var h, 900f, Layers.GroundMask, QueryTriggerInteraction.Ignore))
                        col = h.collider.gameObject.layer == Layers.Drivable ? new Color32(225, 225, 235, 255) : new Color32(70, 120, 70, 200);
                    px[y * N + x] = col;
                }
            tex.SetPixels32(px); tex.Apply();
            mapImage.texture = tex; mapImage.color = Color.white;
            mapReady = true;
        }

        Vector2 MapPoint(Vector3 world)
        {
            float u = Mathf.InverseLerp(mapMin.x, mapMax.x, world.x), v = Mathf.InverseLerp(mapMin.y, mapMax.y, world.z);
            return new Vector2((u - 0.5f) * MapSize, (v - 0.5f) * MapSize);
        }

        static string MapIcon(string racer) => racer switch
        {
            "Mario" => "MarioMap", "Luigi" => "LuigiMap", "Peach" => "PeachMap", "Toad" => "Toad", "Waluigi" => "Waluigi Map",
            "Shy Guy" => "shyguyMap", "Baby Mario" => "Baby Mario Map", "Lemmy" => "LemmyMap", _ => racer
        };

        void MakeDot(RacerProgress r)
        {
            int h = 7; foreach (char ch in r.racerName) h = h * 31 + ch;
            bool me = r.isPlayer;
            Color col = me ? new Color(1f, 0.9f, 0.1f) : Color.HSVToRGB(Mathf.Repeat(h * 0.0137f, 1f), 0.65f, 1f);
            var dot = UiKit.Panel("Dot_" + r.racerName, mapRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * (me ? 24f : 16f), col);
            var head = UiKit.LoadSprite("UI/Map/" + MapIcon(r.racerName));      // target character head icons (Maps/MapPlayer)
            if (head != null) { dot.sprite = head; dot.color = Color.white; dot.rectTransform.sizeDelta = Vector2.one * (me ? 40f : 30f); }
            else { var o = dot.gameObject.AddComponent<Outline>(); o.effectColor = Color.black; o.effectDistance = new Vector2(2f, -2f); }
            dots[r] = dot.rectTransform;
        }

        // ---------------------------------------------------------------- per frame

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (rm == null) { rm = RaceManager.Instance; }
            if (rm == null) return;
            if (player == null)
            {
                if (rm.Player == null) return;
                Bind();
            }

            var kb = Keyboard.current;
            var pad = Gamepad.current;
            bool pausePressed = (kb != null && kb.escapeKey.wasPressedThisFrame) || (pad != null && pad.startButton.wasPressedThisFrame);
            if (pausePressed && (rm.State == RaceState.Racing || rm.State == RaceState.PlayerFinished || isPaused)) SetPaused(!isPaused);
            if (isPaused) return;

            UpdateInfo(dt);
            UpdateMessages(dt);
            UpdateMap(dt);
            UpdateFade(dt);
            if (rm.State == RaceState.Results) UpdateResults(dt);
        }

        void UpdateInfo(float dt)
        {
            bool racing = rm.State == RaceState.Racing || rm.State == RaceState.PlayerFinished || rm.State == RaceState.Countdown;
            timerText.text = UiKit.Time(rm.RaceTime);
            lapText.text = $"{Mathf.Min(player.Lap, rm.totalLaps)}/{rm.totalLaps}";
            if (player.Lap != lastLap) { lastLap = player.Lap; }

            // position with a punch + colour on change
            int pos = player.Position;
            if (pos != lastPosition) { improved = pos < lastPosition; posPunch = 0.6f; lastPosition = pos; }
            posNumber.text = pos.ToString(); posSuffix.text = UiKit.Ordinal(pos);
            if (posPunch > 0f)
            {
                posPunch -= dt;
                float k = Mathf.Clamp01(posPunch / 0.6f);
                posNumber.transform.localScale = Vector3.one * (1f + 0.5f * k);
                posNumber.color = Color.Lerp(Color.white, improved ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.35f, 0.3f), k);
            }
            else { posNumber.transform.localScale = Vector3.one; posNumber.color = Color.white; }

            // coins
            int coins = wallet != null ? wallet.Count : 0;
            if (coins != lastCoins) { if (lastCoins >= 0) coinPunch = 0.3f; lastCoins = coins; }
            coinText.text = coins.ToString("00");
            float cScale = 1f + (coinPunch > 0f ? coinPunch : 0f);
            coinText.transform.localScale = Vector3.one * cScale;
            if (coinIcon != null) coinIcon.transform.localScale = Vector3.one * cScale;
            if (coinPunch > 0f) coinPunch -= dt;

            // item slot
            UpdateItem(dt);

            if (splitT > 0f) { splitT -= dt; if (splitT <= 0f) splitText.text = ""; }

            // hide race info before the countdown
            timerText.gameObject.SetActive(racing);
            lapText.gameObject.SetActive(racing); posNumber.gameObject.SetActive(racing); posSuffix.gameObject.SetActive(racing);
            if (coinIcon != null) coinIcon.gameObject.SetActive(racing);
            if (hudPlate != null) hudPlate.gameObject.SetActive(racing);
            if (flagIcon != null) flagIcon.gameObject.SetActive(racing);
        }

        void UpdateItem(float dt)
        {
            if (holder == null) return;
            string name = ""; Color tint = new Color(1, 1, 1, 0f); string pips = ""; string behind = ""; string timer = "";
            ItemId displayId = ItemId.None;
            switch (holder.State)
            {
                case HolderState.Rolling:
                    displayId = holder.RollDisplay;
                    name = ItemInfo.Name(displayId); tint = ItemInfo.Tint(displayId);
                    break;
                case HolderState.Holding:
                    displayId = holder.Current;
                    name = ItemInfo.Name(displayId); tint = ItemInfo.Tint(displayId);
                    if (holder.UsesLeft > 1) for (int i = 0; i < holder.UsesLeft; i++) pips += "● ";
                    if (holder.HoldingBehind) behind = "HELD BEHIND";
                    if (holder.GoldenLeft > 0f) timer = holder.GoldenLeft.ToString("0.0") + "s";
                    break;
            }

            var sprite = GetItemSprite(displayId);
            if (sprite != null)
            {
                itemIcon.sprite = sprite;
                itemIcon.color = Color.white;
                itemName.text = "";
            }
            else
            {
                itemIcon.sprite = null;
                itemIcon.color = new Color(tint.r, tint.g, tint.b, name.Length > 0 ? 0.85f : 0f);
                itemName.text = name;
            }

            itemPips.text = pips; itemBehind.text = behind; itemTimer.text = timer;
            if (itemPunch > 0f) itemPunch -= dt;
            itemFrame.transform.localScale = Vector3.one * (1f + Mathf.Max(0f, itemPunch) * 0.8f);
            bool rolling = holder.State == HolderState.Rolling;
            itemFrame.color = rolling ? new Color(0.2f, 0.2f, 0.35f, 0.7f) : new Color(0, 0, 0, 0.55f);
            // star / bullet timers
            if (holder.StarLeft > 0f) itemTimer.text = "STAR " + holder.StarLeft.ToString("0.0");
            if (holder.BulletLeft > 0f) itemTimer.text = "BULLET " + holder.BulletLeft.ToString("0.0");
        }

        void UpdateMessages(float dt)
        {
            // intro title
            bool intro = rm.State == RaceState.Intro;
            titleText.gameObject.SetActive(intro);
            skipHint.gameObject.SetActive(intro);
            if (intro)
            {
                var kbd = Keyboard.current; var gp = Gamepad.current;
                if ((kbd != null && (kbd.enterKey.wasPressedThisFrame || kbd.numpadEnterKey.wasPressedThisFrame)) || (gp != null && gp.startButton.wasPressedThisFrame)) rm.SkipIntro();
            }

            // countdown "3 2 1 GO!" with a scale-in
            bool showCount = rm.State == RaceState.Countdown || goT > 0f;
            countdownText.gameObject.SetActive(showCount && lastCountdown >= 0);
            if (showCount)
            {
                countdownT += dt;
                float k = Mathf.Clamp01(countdownT / 0.25f);
                countdownText.transform.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, 1f - (1f - k) * (1f - k));
                if (goT > 0f) goT -= dt;
            }

            // final lap banner slides across
            if (finalLapT >= 0f)
            {
                finalLapT += dt;
                float t = finalLapT / 2.6f;
                ((RectTransform)finalLapText.transform).anchoredPosition = new Vector2(Mathf.Lerp(1500f, -1500f, t), 0f);
                if (t >= 1f) { finalLapT = -1f; finalLapText.gameObject.SetActive(false); }
            }

            // wrong way (flashing)
            bool wrong = player.WrongWay && rm.State == RaceState.Racing;
            wrongWay.gameObject.SetActive(wrong && Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f);

            // finish banner
            if (finishT > 0f)
            {
                finishT -= dt;
                finishText.transform.localScale = Vector3.one * (1f + Mathf.Max(0f, finishT - 2.3f) * 1.2f);
                if (finishT <= 0f) finishText.gameObject.SetActive(false);
            }

            UpdateWarning();
        }

        void UpdateWarning()
        {
            string msg = ""; Color col = Color.red;
            Vector3 me = kart.transform.position;
            foreach (var p in ItemProjectile.All)
            {
                if (p == null || p.Dead) continue;
                if (p is RedShell rs && rs.Target == kart && Vector3.Distance(rs.transform.position, me) < 60f) { msg = "RED SHELL!"; col = new Color(1f, 0.2f, 0.15f); }
            }
            if (BlueShell.IsActive)
            {
                if (player.Position == 1) { msg = "BLUE SHELL INCOMING!"; col = new Color(0.35f, 0.55f, 1f); }
                else if (msg == "") { msg = "Blue shell launched"; col = new Color(0.5f, 0.7f, 1f); }
            }
            if (msg == "")
                foreach (var r in rm.Racers)
                {
                    if (r == player || r.Kart == null) continue;
                    Vector3 to = r.transform.position - me;
                    if (to.magnitude > 40f || Vector3.Dot(to.normalized, kart.transform.forward) > -0.4f) continue;
                    if (r.Kart.State.Bullet) { msg = "BULLET BILL BEHIND!"; col = new Color(0.8f, 0.8f, 0.8f); break; }
                    if (r.Kart.State.Star) { msg = "STAR BEHIND!"; col = new Color(1f, 0.9f, 0.2f); }
                }
            warning.text = msg;
            float a = msg == "" ? 0f : 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 14f);
            warning.color = new Color(col.r, col.g, col.b, a);
        }

        void UpdateMap(float dt)
        {
            bool on = mapReady && (rm.State == RaceState.Racing || rm.State == RaceState.PlayerFinished || rm.State == RaceState.Countdown);
            mapRoot.parent.gameObject.SetActive(on);
            if (!on) return;
            foreach (var kv in dots)
            {
                if (kv.Key == null) continue;
                kv.Value.anchoredPosition = MapPoint(kv.Key.transform.position);
            }
            if (dots.TryGetValue(player, out var pd))
            {
                if (spinT > 0f) { spinT -= dt; pd.localRotation = Quaternion.Euler(0f, 0f, 360f * (1f - spinT / 0.7f)); }
                else pd.localRotation = Quaternion.identity;
            }
            BlueShell blue = null;
            foreach (var p in ItemProjectile.All) if (p is BlueShell b && !b.Dead) { blue = b; break; }
            blueDot.gameObject.SetActive(blue != null);
            if (blue != null) blueDot.anchoredPosition = MapPoint(blue.transform.position);
        }

        void UpdateFade(float dt)
        {
            var r = kart != null ? kart.Respawn : null;
            float target = r != null && r.Current == KartRespawn.Phase.Falling ? 1f : 0f;
            float rate = target > fade.color.a ? 1f / 0.3f : 1f / 0.4f;
            var c = fade.color; c.a = Mathf.MoveTowards(c.a, target, rate * dt); fade.color = c;
        }

        // ---------------------------------------------------------------- results

        void UpdateResults(float dt)
        {
            if (!resultsBuilt) BuildResults();
            resultsT += dt;
            for (int i = 0; i < resultRows.Count; i++)
            {
                float t = Mathf.Clamp01((resultsT - i * 0.1f) / 0.35f);
                float ease = 1f - (1f - t) * (1f - t);
                resultRows[i].anchoredPosition = new Vector2(Mathf.Lerp(1000f, 0f, ease), resultRows[i].anchoredPosition.y);
            }
            // points tally after the rows are in
            float tally = Mathf.Clamp01((resultsT - 2f) / 1.2f);
            for (int i = 0; i < pointTexts.Count; i++)
                pointTexts[i].text = Mathf.RoundToInt(Points[Mathf.Min(i, Points.Length - 1)] * tally).ToString();
        }

        void BuildResults()
        {
            resultsBuilt = true;
            var c = canvas.transform;
            var panel = UiKit.Panel("Results", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100, 880), new Color(0f, 0f, 0.05f, 0.82f));
            resultsPanel = panel.gameObject;
            UiKit.Label("Header", panel.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(900, 90), "RESULTS", 80, Color.white);
            for (int i = 0; i < rm.Results.Count; i++)
            {
                var e = rm.Results[i];
                bool me = e.Racer != null && e.Racer.isPlayer;
                var rowImg = UiKit.Panel("Row" + i, panel.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(1000, -125 - i * 74), new Vector2(1000, 66), me ? new Color(1f, 0.85f, 0.1f, 0.35f) : new Color(1f, 1f, 1f, 0.08f));
                var col = me ? new Color(1f, 0.9f, 0.2f) : Color.white;
                UiKit.Label("Place", rowImg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(120, 60), e.Place + UiKit.Ordinal(e.Place), 44, col, TextAnchor.MiddleLeft);
                var headSprite = UiKit.LoadSprite("UI/Map/" + MapIcon(e.Name));          // target character head icon
                if (headSprite != null)
                {
                    var head = UiKit.Panel("Head", rowImg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(190, 0), new Vector2(56, 56), Color.white);
                    head.sprite = headSprite; head.preserveAspect = true;
                }
                UiKit.Label("Name", rowImg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(250, 0), new Vector2(320, 60), e.Name, 42, col, TextAnchor.MiddleLeft);
                UiKit.Label("Time", rowImg.transform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(560, 0), new Vector2(260, 60), e.Finished ? UiKit.Time(e.Time) : "--:--.--", 40, col, TextAnchor.MiddleLeft);
                pointTexts.Add(UiKit.Label("Points", rowImg.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, 0), new Vector2(120, 60), "0", 44, col, TextAnchor.MiddleRight));
                resultRows.Add(rowImg.rectTransform);
            }
            UiKit.MakeButton("Retry", panel.transform, new Vector2(0.3f, 0), new Vector2(0, 70), new Vector2(380, 90), "Retry", () => rm.Restart());
            UiKit.MakeButton("QuitBtn", panel.transform, new Vector2(0.7f, 0), new Vector2(0, 70), new Vector2(380, 90), "Quit", QuitGame);
            // hide the racing widgets behind the results
            mapRoot.parent.gameObject.SetActive(false);
        }
    }
}
