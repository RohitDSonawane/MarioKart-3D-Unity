using HarborRacer.Environment;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HarborRacer.EditorTools
{
    /// <summary>Gives an instantiated PalmTree.fbx the "Wave" animation (looped at runtime by LoopAnimator).</summary>
    public static class PalmSway
    {
        const string FbxPath = "Assets/_Project/Art/Track/Props/PalmTree.fbx";
        const string ControllerPath = "Assets/_Project/Art/Shared/Animators/PalmWave.controller";

        public static void Attach(GameObject palm, int index)
        {
            foreach (var t in palm.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = LayerMask.NameToLayer("Scenery");
            // the converted FBX has no textures: target Trunk / PalmLeaves materials (URP Lit, alpha-clipped leaves)
            var trunk = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Shared/Palm/Trunk.mat");
            var leaves = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Shared/Palm/PalmLeaves.mat");
            foreach (var r in palm.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.name.ToLower();
                if (n.Contains("branch") && trunk != null) r.sharedMaterial = trunk;
                else if (n.Contains("reaf") && leaves != null) r.sharedMaterial = leaves;
            }
            var controller = GetController();
            if (controller == null) return;
            var anim = palm.GetComponent<Animator>();
            if (anim == null) anim = palm.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            if (palm.GetComponent<LoopAnimator>() == null) palm.AddComponent<LoopAnimator>();
        }

        static AnimatorController GetController()
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (c != null) return c;
            AnimationClip clip = null;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(FbxPath))
                if (o is AnimationClip ac && !ac.name.StartsWith("__preview__")) { clip = ac; break; }
            if (clip == null) { Debug.LogWarning("PalmTree wave clip not found in " + FbxPath); return null; }
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Shared/Animators"))
                AssetDatabase.CreateFolder("Assets/_Project/Art/Shared", "Animators");
            return AnimatorController.CreateAnimatorControllerAtPathWithClip(ControllerPath, clip);
        }
    }
}
