using HarborRacer.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HarborRacer.Race
{
    /// <summary>
    /// Minimal on-screen race info (countdown, lap, position, time, wrong way, results table with restart).
    /// Development placeholder; Phase 10 replaces it with the real HUD, results screen and minimap.
    /// </summary>
    public class RaceDebugHUD : MonoBehaviour
    {
        GUIStyle big, mid, small;

        void Update()
        {
            var rm = RaceManager.Instance;
            if (rm == null || Keyboard.current == null) return;
            if (rm.State == RaceState.Results && Keyboard.current.rKey.wasPressedThisFrame) rm.Restart();
#if UNITY_EDITOR || DEBUG
            DebugItemKeys(rm, Keyboard.current);
#endif
        }

        // dev only: 1-0 mushrooms/star/bananas/shells, F5 blue shell, F6 bob-omb, F7 bullet bill, F8 coin, F9 empty
        static void DebugItemKeys(RaceManager rm, Keyboard kb)
        {
#if UNITY_EDITOR || DEBUG
            var holder = rm.Player != null ? rm.Player.GetComponent<ItemHolder>() : null;
            if (holder == null) return;
            if (kb.digit1Key.wasPressedThisFrame) holder.Give(ItemId.Mushroom);
            else if (kb.digit2Key.wasPressedThisFrame) holder.Give(ItemId.TripleMushroom);
            else if (kb.digit3Key.wasPressedThisFrame) holder.Give(ItemId.GoldenMushroom);
            else if (kb.digit4Key.wasPressedThisFrame) holder.Give(ItemId.Star);
            else if (kb.digit5Key.wasPressedThisFrame) holder.Give(ItemId.Banana);
            else if (kb.digit6Key.wasPressedThisFrame) holder.Give(ItemId.TripleBanana);
            else if (kb.digit7Key.wasPressedThisFrame) holder.Give(ItemId.GreenShell);
            else if (kb.digit8Key.wasPressedThisFrame) holder.Give(ItemId.TripleGreen);
            else if (kb.digit9Key.wasPressedThisFrame) holder.Give(ItemId.RedShell);
            else if (kb.digit0Key.wasPressedThisFrame) holder.Give(ItemId.TripleRed);
            else if (kb.f5Key.wasPressedThisFrame) holder.Give(ItemId.BlueShell);
            else if (kb.f6Key.wasPressedThisFrame) holder.Give(ItemId.BobOmb);
            else if (kb.f7Key.wasPressedThisFrame) holder.Give(ItemId.BulletBill);
            else if (kb.f8Key.wasPressedThisFrame) holder.Give(ItemId.Coin);
            else if (kb.f9Key.wasPressedThisFrame) holder.Give(ItemId.None);
#endif
        }

        void EnsureStyles()
        {
            if (big != null) return;
            big = new GUIStyle(GUI.skin.label) { fontSize = 96, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            mid = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold };
            small = new GUIStyle(GUI.skin.label) { fontSize = 18 };
        }

        static string Ordinal(int n) => n == 1 ? "1st" : n == 2 ? "2nd" : n == 3 ? "3rd" : n + "th";

        static string Time(float t) => $"{(int)(t / 60):00}:{t % 60:00.00}";

        void OnGUI()
        {
            if (HarborRacer.UI.RaceHud.Instance != null) return;     // the real HUD replaces this overlay
            var rm = RaceManager.Instance;
            if (rm == null) return;
            EnsureStyles();
            float w = Screen.width, h = Screen.height;

            switch (rm.State)
            {
                case RaceState.Intro:
                    GUI.Label(new Rect(0, h * 0.25f, w, 100), "TOAD HARBOR", big);
                    break;
                case RaceState.Countdown:
                    GUI.Label(new Rect(0, h * 0.25f, w, 140), rm.CountdownValue.ToString(), big);
                    break;
            }
            if (rm.GoDisplayTimer > 0f) GUI.Label(new Rect(0, h * 0.25f, w, 140), "GO!", big);

            var p = rm.Player;
            if (p != null && (rm.State == RaceState.Racing || rm.State == RaceState.PlayerFinished || rm.State == RaceState.Countdown))
            {
                GUI.Label(new Rect(w - 320, 12, 300, 40), Time(rm.RaceTime), mid);
                GUI.Label(new Rect(20, h - 120, 400, 40), $"LAP {Mathf.Min(p.Lap, rm.totalLaps)}/{rm.totalLaps}", mid);
                GUI.Label(new Rect(w - 220, h - 120, 200, 50), Ordinal(p.Position) + "/" + rm.Racers.Count, mid);
                if (rm.FinalLap && p.Lap == rm.totalLaps && !p.Finished) GUI.Label(new Rect(0, 70, w, 50), "FINAL LAP!", new GUIStyle(mid) { alignment = TextAnchor.MiddleCenter });
                if (p.WrongWay) GUI.Label(new Rect(0, h * 0.4f, w, 60), "WRONG WAY", new GUIStyle(mid) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.red } });
                if (p.Finished) GUI.Label(new Rect(0, h * 0.3f, w, 120), "FINISH!", big);
                DrawItemHud(p, w, h);
            }

            if (rm.State == RaceState.Results)
            {
                GUI.Box(new Rect(w * 0.25f, h * 0.12f, w * 0.5f, h * 0.7f), "RESULTS");
                for (int i = 0; i < rm.Results.Count; i++)
                {
                    var e = rm.Results[i];
                    string line = $"{e.Place,2}.  {e.Name,-12}  {(e.Finished ? Time(e.Time) : "--:--.--")}";
                    var style = new GUIStyle(small);
                    if (e.Racer != null && e.Racer.isPlayer) style.normal.textColor = Color.yellow;
                    GUI.Label(new Rect(w * 0.27f, h * 0.18f + i * 36, w * 0.46f, 32), line, style);
                }
                GUI.Label(new Rect(w * 0.27f, h * 0.18f + rm.Results.Count * 36 + 12, w * 0.46f, 32), "Press R to restart", small);
            }
        }

        void DrawItemHud(RacerProgress p, float w, float h)
        {
            var holder = p.GetComponent<ItemHolder>();
            var wallet = p.GetComponent<CoinWallet>();
            if (holder == null) return;
            string text;
            switch (holder.State)
            {
                case HolderState.Rolling: text = "ITEM: ... " + ItemInfo.Name(holder.RollDisplay); break;
                case HolderState.Holding:
                    text = "ITEM: " + ItemInfo.Name(holder.Current) + (holder.UsesLeft > 1 ? " x" + holder.UsesLeft : "") + (holder.HoldingBehind ? " (behind)" : "")
                           + (holder.GoldenLeft > 0f ? $"  {holder.GoldenLeft:0.0}s" : "");
                    break;
                default: text = "ITEM: -"; break;
            }
            GUI.Label(new Rect(20, 12, 520, 40), text, mid);
            if (wallet != null) GUI.Label(new Rect(20, 52, 300, 40), "COINS " + wallet.Count + "/" + CoinWallet.Max, mid);
            if (holder.StarLeft > 0f) GUI.Label(new Rect(20, 92, 400, 40), $"STAR {holder.StarLeft:0.0}s", mid);
            if (holder.BulletLeft > 0f) GUI.Label(new Rect(20, 92, 400, 40), $"BULLET BILL {holder.BulletLeft:0.0}s", mid);
            if (BlueShell.IsActive)
                GUI.Label(new Rect(0, 120, w, 50), p.Position == 1 ? "BLUE SHELL INCOMING!" : "Blue shell launched", new GUIStyle(mid) { alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.4f, 0.6f, 1f) } });
        }
    }
}
