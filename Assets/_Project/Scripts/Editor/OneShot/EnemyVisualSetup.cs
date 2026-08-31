using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class EnemyVisualSetup
    {
        private const string ControllerDirectory = "Assets/_Project/Animation";

        private static readonly (string PrefabPath, string AsepritePath, string ControllerPath)[] Enemies =
        {
            ("Assets/_Project/Prefabs/Enemy_Basic.prefab", "Assets/_Project/Textures/Enemy/SwarmGrunt.aseprite", ControllerDirectory + "/SwarmGrunt.controller"),
            ("Assets/_Project/Prefabs/Enemy_Fast.prefab", "Assets/_Project/Textures/Enemy/SwarmGrunt_Fast.aseprite", ControllerDirectory + "/SwarmGrunt_Fast.controller"),
            ("Assets/_Project/Prefabs/Enemy_Tank.prefab", "Assets/_Project/Textures/Enemy/SwarmGrunt_Tank.aseprite", ControllerDirectory + "/SwarmGrunt_Tank.controller"),
        };

        [MenuItem("Swarm/Art Test/Setup Enemy Visuals (Basic + Fast + Tank)")]
        private static void Setup()
        {
            if (!Directory.Exists(ControllerDirectory))
            {
                Directory.CreateDirectory(ControllerDirectory);
            }

            foreach (var (prefabPath, asepritePath, controllerPath) in Enemies)
            {
                SetupOne(prefabPath, asepritePath, controllerPath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Enemy visuals set up for Basic, Fast, and Tank. Transform scale on each prefab already differs (0.7 / 0.5 / 1.1), so size stays as-is.");
        }

        private static void SetupOne(string prefabPath, string asepritePath, string controllerPath)
        {
            var asepriteAssets = AssetDatabase.LoadAllAssetsAtPath(asepritePath);
            var walkClip = asepriteAssets.OfType<AnimationClip>().FirstOrDefault(c => c.name == "Walk");
            var frame0 = asepriteAssets.OfType<Sprite>().FirstOrDefault(s => s.name == "Frame_0");

            if (walkClip == null || frame0 == null)
            {
                Debug.LogError($"Could not find Walk clip or Frame_0 sprite in {asepritePath}. Skipped {prefabPath}.");
                return;
            }

            var settings = AnimationUtility.GetAnimationClipSettings(walkClip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(walkClip, settings);
            EditorUtility.SetDirty(walkClip);

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }

            var rootStateMachine = controller.layers[0].stateMachine;
            rootStateMachine.states = System.Array.Empty<ChildAnimatorState>();
            var walkState = rootStateMachine.AddState("Walk");
            walkState.motion = walkClip;
            rootStateMachine.defaultState = walkState;

            EditorUtility.SetDirty(controller);

            var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var spriteRenderer = prefabRoot.GetComponent<SpriteRenderer>();
                if (spriteRenderer != null)
                {
                    spriteRenderer.sprite = frame0;
                    spriteRenderer.color = Color.white;
                }

                var animator = prefabRoot.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = prefabRoot.AddComponent<Animator>();
                }

                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            Debug.Log($"Linked {prefabPath} -> sprite={frame0.name}, controller={controller.name}");
        }
    }
}
