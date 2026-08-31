using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class ArcherAnimatorSetup
    {
        private const string ArcherAsepritePath = "Assets/_Project/Textures/Player/Archer.aseprite";
        private const string ControllerDirectory = "Assets/_Project/Animation";
        private const string ControllerPath = ControllerDirectory + "/Archer.controller";

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Setup Archer Idle-Walk-Roll Animator (Both Scenes)")]
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

            Debug.Log("Archer Animator setup complete for Game.unity and TestStage.unity. Enter Play Mode and move the player to check the Idle/Walk loop; trigger \"Roll\" from the Animator window to preview the roll.");
        }

        private static AnimatorController BuildController()
        {
            var asepriteAssets = AssetDatabase.LoadAllAssetsAtPath(ArcherAsepritePath);
            var idleClip = asepriteAssets.OfType<AnimationClip>().FirstOrDefault(c => c.name == "Idle");
            var walkClip = asepriteAssets.OfType<AnimationClip>().FirstOrDefault(c => c.name == "Walk");
            var rollClip = asepriteAssets.OfType<AnimationClip>().FirstOrDefault(c => c.name == "Roll");

            if (idleClip == null || walkClip == null || rollClip == null)
            {
                var foundNames = string.Join(", ", asepriteAssets.OfType<AnimationClip>().Select(c => c.name));
                Debug.LogError($"Could not find 'Idle', 'Walk' and 'Roll' AnimationClip sub-assets in {ArcherAsepritePath}. Found clips: [{foundNames}]. Right-click the file in the Project window and choose Reimport, then run this again.");
                return null;
            }

            SetLoop(idleClip, true);
            SetLoop(walkClip, true);
            SetLoop(rollClip, false);

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
            controller.AddParameter("Roll", AnimatorControllerParameterType.Trigger);

            var rootStateMachine = controller.layers[0].stateMachine;
            rootStateMachine.states = System.Array.Empty<ChildAnimatorState>();
            rootStateMachine.anyStateTransitions = System.Array.Empty<AnimatorStateTransition>();

            var idleState = rootStateMachine.AddState("Idle");
            idleState.motion = idleClip;

            var walkState = rootStateMachine.AddState("Walk");
            walkState.motion = walkClip;

            var rollState = rootStateMachine.AddState("Roll");
            rollState.motion = rollClip;

            rootStateMachine.defaultState = idleState;

            var toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");

            var toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

            var anyToRoll = rootStateMachine.AddAnyStateTransition(rollState);
            anyToRoll.hasExitTime = false;
            anyToRoll.duration = 0f;
            anyToRoll.canTransitionToSelf = false;
            anyToRoll.AddCondition(AnimatorConditionMode.If, 0f, "Roll");

            var rollToIdle = rollState.AddTransition(idleState);
            rollToIdle.hasExitTime = true;
            rollToIdle.exitTime = 1f;
            rollToIdle.duration = 0f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            return controller;
        }

        private static void SetLoop(AnimationClip clip, bool loop)
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
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
                var frame0 = AssetDatabase.LoadAllAssetsAtPath(ArcherAsepritePath)
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
