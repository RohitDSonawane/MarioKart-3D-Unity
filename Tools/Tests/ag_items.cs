using System.Linq;
using System.Text;
using HarborRacer.Items;
using HarborRacer.Kart;
using UnityEngine;

public static class AgItems
{
    public static string Run()
    {
        var sb = new StringBuilder();
        Physics.SyncTransforms();
        Vector3 origin = new Vector3(5000f, 5000f, 5000f);          // far from the track
        foreach (float tilt in new[] { 0f, 20f, 45f, 90f })
        {
            var slab = new GameObject("TmpAgSlab"); slab.layer = HarborRacer.Layers.Drivable;
            var bc = slab.AddComponent<BoxCollider>(); bc.size = new Vector3(60f, 1f, 800f);
            slab.transform.position = origin; slab.transform.rotation = Quaternion.Euler(0f, 0f, tilt);   // banked around the forward axis
            Physics.SyncTransforms();
            Vector3 n = slab.transform.up;
            Vector3 start = origin + n * 1.2f;
            ItemProjectile.ResetStatics();
            var shell = GreenShell.Create(null, start, Vector3.forward);
            typeof(ItemProjectile).GetField("groundNormal", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(shell, n);
            float maxOff = 0, minOff = 99; int ticks = 0; bool alive = true;
            for (int i = 0; i < 150; i++)
            {
                shell.Tick(0.02f);
                if (shell == null) { alive = false; break; }
                float off = Vector3.Dot(shell.transform.position - origin, n);
                maxOff = Mathf.Max(maxOff, off); minOff = Mathf.Min(minOff, off); ticks++;
            }
            float along = alive ? shell.transform.position.z - origin.z : -1;
            sb.AppendLine($"tilt {tilt,3}: alive={alive} ticks={ticks} height over surface min {minOff:F2} max {maxOff:F2}, travelled {along:F0} u");
            if (shell != null) shell.Kill();
            Object.DestroyImmediate(slab);
        }
        return sb.ToString();
    }
}
