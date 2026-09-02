using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Swarm.EditorTools
{
    /// <summary>
    /// Wires the imported Boss.aseprite walk animation onto Enemy_Boss.prefab.
    ///
    /// The Aseprite importer decides the sub-asset ids for the sprites and the generated
    /// AnimationClip at import time, so they cannot be written into the .prefab / .controller
    /// YAML by hand from outside Unity. This runs once inside the editor instead and lets
    /// Unity resolve the references itself. It is idempotent: after the first successful run
    /// it detects the wiring is in place and does nothing, so it is safe to leave in the
    /// project (or delete it once the boss looks right).
    /// </summary>
    public static class BossWalkSetup
    {
        private const string AsepritePath = "Assets/_Project/Textures/Enemy/Boss.aseprite";
        private const string ControllerPath = "Assets/_Project/Animation/Boss.controller";
        private const string PrefabPath = "Assets/_Project/Prefabs/Enemy_Boss.prefab";
        private const string StateName = "Walk";

        // The boss used to be a 92px grunt sprite scaled 2.4x, i.e. roughly 3.1 world units of
        // visible body. The new art is a 462px canvas, so the scale is derived from the sprite
        // instead of hard-coded, keeping the on-screen size of the encounter unchanged.
        private const float TargetWorldHeight = 3.1f;

        // Body collider radius in world units, unchanged from the previous setup (0.5 * 2.4).
        private const float ColliderWorldRadius = 1.2f;

        [MenuItem("Swarm/Setup/Attach Boss Walk Animation")]
        public static void RunFromMenu() => Execute(true);

        [InitializeOnLoadMethod]
        private static void AutoRun() => EditorApplication.delayCall += () => Execute(false);

        private static void Execute(bool verbose)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(AsepritePath);
            if (assets == null || assets.Length == 0)
            {
                if (verbose) Debug.LogError($"BossWalkSetup: nothing imported at {AsepritePath}.");
                return;
            }

            var sprites = assets.OfType<Sprite>().OrderBy(s => s.name, System.StringComparer.Ordinal).ToArray();
            var clip = assets.OfType<AnimationClip>().FirstOrDefault();
            if (sprites.Length == 0 || clip == null)
            {
                if (verbose)
                {
                    Debug.LogError("BossWalkSetup: the Aseprite import produced " +
                                   $"{sprites.Length} sprites and {(clip == null ? "no" : "a")} clip. " +
                                   "Check that 'Generate Animation Clips' is on in the importer.");
                }

                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                if (verbose) Debug.LogError($"BossWalkSetup: {PrefabPath} not found.");
                return;
            }

            var existingController = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!verbose && IsAlreadyWired(prefab, existingController, clip)) return;

            var controller = existingController != null
                ? existingController
                : AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            var stateMachine = controller.layers[0].stateMachine;
            var state = stateMachine.states.Select(c => c.state).FirstOrDefault(s => s.name == StateName);
            if (state == null)
            {
                state = stateMachine.AddState(StateName);
                stateMachine.defaultState = state;
            }

            state.motion = clip;
            EditorUtility.SetDirty(controller);

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null) renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[0];
                // The old placeholder was tinted dark red to read as "boss". The real art carries
                // its own colour, so the tint goes back to white.
                renderer.color = Color.white;

                var animator = root.GetComponent<Animator>();
                if (animator == null) animator = root.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                var tallest = sprites.Max(s => s.rect.height / s.pixelsPerUnit);
                var scale = TargetWorldHeight / tallest;
                root.transform.localScale = new Vector3(scale, scale, 1f);

                // Collider values are authored in local space, so they are divided by the new
                // scale to keep the same world-space hitbox the boss had before.
                var collider = root.GetComponent<CircleCollider2D>();
                if (collider != null)
                {
                    collider.radius = ColliderWorldRadius / scale;
                    // The Aseprite pivot sits at the bottom of the canvas, so the body is above
                    // the transform origin; the collider is lifted to sit on the boss itself.
                    collider.offset = new Vector2(0f, ColliderWorldRadius / scale);
                }

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"BossWalkSetup: attached '{clip.name}' ({sprites.Length} frames) to Enemy_Boss " +
                      $"via {ControllerPath}.");
        }

        private static bool IsAlreadyWired(GameObject prefab, AnimatorController controller, AnimationClip clip)
        {
            if (controller == null) return false;

            var animator = prefab.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController != controller) return false;

            var renderer = prefab.GetComponent<SpriteRenderer>();
            if (renderer == null || renderer.sprite == null) return false;
            if (AssetDatabase.GetAssetPath(renderer.sprite) != AsepritePath) return false;

            return controller.layers[0].stateMachine.states
                .Select(c => c.state)
                .Any(s => s.name == StateName && s.motion == clip);
        }
    }
}
