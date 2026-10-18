using UnityEngine;

namespace HarborRacer.Environment
{
    /// <summary>
    /// Plays the first state of an Animator forever at a random phase, even when the clip itself was imported as non-looping
    /// (the palm tree "Wave" clip, boat bobbing). Culls itself when off screen via the Animator's culling mode.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class LoopAnimator : MonoBehaviour
    {
        public float speed = 1f;
        Animator anim;

        void Start()
        {
            anim = GetComponent<Animator>();
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            anim.speed = speed * Random.Range(0.9f, 1.1f);
            anim.Play(0, 0, Random.value);
        }

        void Update()
        {
            if (anim == null || anim.runtimeAnimatorController == null) return;
            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (st.normalizedTime >= 0.99f) anim.Play(0, 0, 0f);
        }
    }
}
