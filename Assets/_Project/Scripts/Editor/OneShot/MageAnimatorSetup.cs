using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class MageAnimatorSetup
    {
        private const string MageAsepritePath = "Assets/_Project/Textures/Player/Mage.aseprite";
        private const string ControllerDirectory = "Assets/_Project/Animation";
        private const string ControllerPath = ControllerDirectory + "/Mage.controller";

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Setup Mage Idle-Walk Animator (Both Scenes)")]
        private static void Setup()
        {
            var controller = BuildController();
            if (controller == null)
            {
                return;
            }

            foreach (var scenePath in ScenePaths)
            {
                ApplyToScene(scenePath, controller);
            }

            Debug.Log("Mage Animator setup complete for Game.unity and TestStage.unity. Enter Play Mode and move the player to check the Idle/Walk loop.");
        }

        private static AnimatorController BuildController()
        {
            var asepriteAssets = AssetDatabase.LoadAllAssetsAtPath(MageAsepritePath);
            var idleClip = asepriteAssets.OfType<AnimationClip>().FirstOrDefault(c => c.name == "Idle");
            var walkClip = asepriteAssets.OfType<AnimationClip>().FirstOrDefault(c => c.name == "Walk");

            if (idleClip == null || walkClip == null)
            {
                var foundNames = string.Join(", ", asepriteAssets.OfType<AnimationClip>().Select(c => c.name));
                Debug.LogError($"Could not find both 'Idle' and 'Walk' AnimationClip sub-assets in {MageAsepritePath}. Found clips: [{foundNames}]. Right-click the file in the Project window and choose Reimport, then run this again.");
                return null;
            }

            SetLoop(idleClip);
            SetLoop(walkClip);

            if (!Directory.Exists(ControllerDirectory))
            {
                Directory.CreateDirectory(ControllerDirectory);
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            controller.parameters = System.Array.Empty<AnimatorControllerParameter>();
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);

            var rootStateMachine = controller.layers[0].stateMachine;
            rootStateMachine.states = System.Array.Empty<ChildAnimatorState>();

            var idleState = rootStateMachine.AddState("Idle");
            idleState.motion = idleClip;

            var walkState = rootStateMachine.AddState("Walk");
            walkState.motion = walkClip;

            rootStateMachine.defaultState = idleState;

            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void SetLoop(AnimationClip clip)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        private static void ApplyToScene(string scenePath, AnimatorController controller)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError($"Player GameObject not found in {scenePath}. Skipped.");
                return;
            }

            var spriteRenderer = player.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                var frame0 = AssetDatabase.LoadAllAssetsAtPath(MageAsepritePath)
                    .OfType<Sprite>()
                    .FirstOrDefault(s => s.name == "Frame_0");
                if (frame0 != null)
                {
                    spriteRenderer.sprite = frame0;
                    spriteRenderer.color = Color.white;
                }
            }

            var animator = player.GetComponent<Animator>();
            if (animator == null)
            {
                animator = player.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
