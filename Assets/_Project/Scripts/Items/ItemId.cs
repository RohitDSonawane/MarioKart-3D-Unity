using System.Collections.Generic;
using UnityEngine;

namespace HarborRacer.Items
{
    public enum ItemId
    {
        None, Banana, TripleBanana, GreenShell, TripleGreen, RedShell, TripleRed, BlueShell, BobOmb,
        Mushroom, TripleMushroom, GoldenMushroom, Star, BulletBill, Coin
    }

    public static class ItemInfo
    {
        public static string Name(ItemId id) => id switch
        {
            ItemId.TripleBanana => "Triple Banana", ItemId.GreenShell => "Green Shell", ItemId.TripleGreen => "Triple Green Shells",
            ItemId.RedShell => "Red Shell", ItemId.TripleRed => "Triple Red Shells", ItemId.BlueShell => "Blue Shell",
            ItemId.BobOmb => "Bob-omb", ItemId.TripleMushroom => "Triple Mushroom", ItemId.GoldenMushroom => "Golden Mushroom",
            ItemId.BulletBill => "Bullet Bill", ItemId.None => "-", _ => id.ToString()
        };

        public static int Uses(ItemId id) => id == ItemId.TripleBanana || id == ItemId.TripleGreen || id == ItemId.TripleRed || id == ItemId.TripleMushroom ? 3 : 1;

        /// <summary>Items that can be dragged behind the kart as a shield (bananas, shells, bomb).</summary>
        public static bool CanHoldBehind(ItemId id) => id == ItemId.Banana || id == ItemId.TripleBanana || id == ItemId.GreenShell || id == ItemId.TripleGreen
                                                       || id == ItemId.RedShell || id == ItemId.TripleRed || id == ItemId.BobOmb;

        public static bool IsBanana(ItemId id) => id == ItemId.Banana || id == ItemId.TripleBanana;
        public static bool IsGreen(ItemId id) => id == ItemId.GreenShell || id == ItemId.TripleGreen;
        public static bool IsRed(ItemId id) => id == ItemId.RedShell || id == ItemId.TripleRed;

        /// <summary>Colour used for the placeholder visuals / HUD.</summary>
        public static Color Tint(ItemId id) => id switch
        {
            ItemId.Banana or ItemId.TripleBanana => new Color(1f, 0.9f, 0.1f),
            ItemId.GreenShell or ItemId.TripleGreen => new Color(0.15f, 0.8f, 0.2f),
            ItemId.RedShell or ItemId.TripleRed => new Color(0.9f, 0.1f, 0.1f),
            ItemId.BlueShell => new Color(0.15f, 0.35f, 1f),
            ItemId.BobOmb => new Color(0.1f, 0.1f, 0.12f),
            ItemId.Star => new Color(1f, 0.95f, 0.3f),
            ItemId.BulletBill => new Color(0.1f, 0.1f, 0.1f),
            ItemId.Coin => new Color(1f, 0.8f, 0.1f),
            _ => new Color(1f, 0.4f, 0.1f)
        };
    }

    /// <summary>
    /// Position-weighted item table. Player and AI share it.
    /// Rules: only one Blue Shell on the track at a time, no Bullet Bill where the section forbids it (both re-roll).
    /// </summary>
    public static class ItemDistribution
    {
        static readonly (ItemId id, int weight)[][] Rows =
        {
            /* 1 */ new[] { (ItemId.Coin, 2), (ItemId.Banana, 2), (ItemId.GreenShell, 2) },
            /* 2 */ new[] { (ItemId.Banana, 1), (ItemId.GreenShell, 2), (ItemId.RedShell, 2) },
            /* 3 */ new[] { (ItemId.Mushroom, 2), (ItemId.RedShell, 3), (ItemId.BobOmb, 1) },
            /* 4 */ new[] { (ItemId.RedShell, 1), (ItemId.BobOmb, 1), (ItemId.Mushroom, 1), (ItemId.TripleGreen, 1), (ItemId.TripleBanana, 2) },
            /* 5 */ new[] { (ItemId.TripleGreen, 1), (ItemId.Mushroom, 1), (ItemId.TripleRed, 2), (ItemId.TripleMushroom, 2) },
            /* 6 */ new[] { (ItemId.TripleMushroom, 2), (ItemId.TripleRed, 3), (ItemId.BlueShell, 2) },
            /* 7 */ new[] { (ItemId.BlueShell, 2), (ItemId.GoldenMushroom, 3), (ItemId.Star, 3) },
            /* 8 */ new[] { (ItemId.Star, 2), (ItemId.GoldenMushroom, 3), (ItemId.BulletBill, 3) },
        };

        public static IReadOnlyList<(ItemId id, int weight)> Row(int position) => Rows[Mathf.Clamp(position, 1, Rows.Length) - 1];

        public static ItemId Roll(int position, System.Random rng, bool canBullet = true, bool blueShellActive = false)
        {
            var row = Row(position);
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int total = 0; foreach (var e in row) total += e.weight;
                int pick = rng.Next(total);
                foreach (var e in row)
                {
                    if (pick < e.weight)
                    {
                        if (e.id == ItemId.BlueShell && blueShellActive) break;
                        if (e.id == ItemId.BulletBill && !canBullet) break;
                        return e.id;
                    }
                    pick -= e.weight;
                }
            }
            return ItemId.Mushroom;   // every row can fall back to a plain mushroom if re-rolls keep failing
        }
    }
}
