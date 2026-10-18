using UnityEngine;
using UnityEngine.InputSystem;

namespace HarborRacer.Kart
{
    /// <summary>F1 toggles an on-screen readout of the kart's runtime numbers (dev only). F2 grants a 2 s boost.</summary>
    public class KartDebugHUD : MonoBehaviour
    {
        public KartController kart;
        bool show = true;

        void Update()
        {
#if UNITY_EDITOR || DEBUG
            if (Keyboard.current == null) return;
            if (Keyboard.current.f1Key.wasPressedThisFrame) show = !show;
            if (Keyboard.current.f2Key.wasPressedThisFrame && kart != null && kart.Boost != null)
                kart.Boost.AddBoost(2f, BoostSource.Debug);
#endif
        }

        void OnGUI()
        {
#if UNITY_EDITOR || DEBUG
            if (!show || kart == null) return;
            var s = kart.State;
            var d = kart.Drift;
            string drift = d == null ? "-" : $"dir={d.DriftDir} drifting={d.IsDrifting} landed={d.Landed} hop={d.Hopping} t={d.DriftTime:F1}s tier={d.Tier}";
            string boost = kart.Boost == null ? "-" : $"{kart.Boost.Remaining:F2}s";
            GUI.Label(new Rect(12, 12, 640, 200),
                $"speed cmd {kart.CurrentSpeed:F1}  real {kart.ForwardSpeed:F1}  max {kart.MaxSpeedNow:F0}  turnStr {kart.TurnStrength:F1}\n" +
                $"grounded {s.Grounded}  dirt {s.OnDirt}  d={s.GroundDistance:F2} n.y={s.GroundNormal.y:F2}\n" +
                $"drift {drift}\nboost {boost}   (F2 = debug boost, F1 = hide)");
#endif
        }
    }
}
