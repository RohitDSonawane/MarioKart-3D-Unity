using System;
using UnityEngine;

namespace HarborRacer.Items
{
    /// <summary>
    /// Coin count (0..10). Each coin adds +1 to the kart's normal max speed (read by KartController.ResolveMaxSpeed); a hit costs 3 coins.
    /// </summary>
    public class CoinWallet : MonoBehaviour
    {
        public const int Max = 10;
        public const int HitLoss = 3;

        public int Count { get; private set; }
        public event Action<int> Changed;

        public void Add(int n = 1) { Set(Count + n); }
        public void Lose(int n = HitLoss) { Set(Count - n); }
        public void Clear() { Set(0); }

        void Set(int v)
        {
            v = Mathf.Clamp(v, 0, Max);
            if (v == Count) return;
            Count = v;
            Changed?.Invoke(Count);
        }
    }
}
