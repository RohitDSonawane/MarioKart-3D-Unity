using HarborRacer.Hazards;
using HarborRacer.Kart;
using UnityEngine;

namespace HarborRacer.Items
{
    /// <summary>Expanding fire ball, purely cosmetic.</summary>
    public class BlastVisual : MonoBehaviour
    {
        float t, radius; Transform ball;

        public static void Spawn(Vector3 pos, float radius)
        {
            var go = new GameObject("Blast");
            go.transform.position = pos;
            var b = go.AddComponent<BlastVisual>();
            b.radius = radius;
            b.ball = ItemVisuals.Make(PrimitiveType.Sphere, new Color(1f, 0.55f, 0.1f), Vector3.one, go.transform, emissive: true);
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.5f);
            ball.localScale = Vector3.one * radius * 2f * Mathf.Sqrt(k);
            if (t >= 0.5f) Destroy(gameObject);
        }
    }
}
