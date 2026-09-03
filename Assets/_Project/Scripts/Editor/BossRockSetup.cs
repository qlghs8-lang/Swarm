using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEngine;

namespace Swarm.EditorTools
{
    /// <summary>
    /// Builds the two effect prefabs for the boss rockfall out of bossrock.aseprite and hands them
    /// to <c>BossMeteorAttack</c> on Enemy_Boss.
    ///
    /// The strip is one five frame sequence but two separate effects: frame 0 is the rock still in
    /// the air with its motion streaks, and frames 1-4 are the landing. The falling rock therefore
    /// gets a plain SpriteRenderer that the attack moves down itself, while the impact gets a
    /// <c>StrikeFlash</c> stepping the remaining four frames once and then releasing itself.
    ///
    /// Like BossWalkSetup this exists because the Aseprite importer assigns the sprite sub-asset ids
    /// at import time, so the references cannot be written into the prefab YAML from outside Unity.
    /// It is idempotent and safe to leave in the project.
    /// </summary>
    public static class BossRockSetup
    {
        private const string AsepritePath = "Assets/_Project/Textures/Effects/bossrock.aseprite";
        private const string FallPrefabPath = "Assets/_Project/Prefabs/Effect_BossRock_Fall.prefab";
        private const string ImpactPrefabPath = "Assets/_Project/Prefabs/Effect_BossRock_Impact.prefab";
        private const string BossPrefabPath = "Assets/_Project/Prefabs/Enemy_Boss.prefab";

        // The art is drawn on a 256px square canvas with the ground line sitting on its bottom edge,
        // so "how big is the effect" is a question about the canvas, not about one trimmed frame —
        // using a frame's own height would make the sequence grow and shrink as the dust changes
        // shape. Both numbers below are the world size that canvas covers.
        private const float CanvasPixels = 256f;
        private const float PixelsPerUnit = 100f;

        // The Aseprite import puts every frame's pivot on the bottom edge of the canvas, which is
        // where the art's ground line sits, so an effect spawned at a point on the ground needs no
        // offset of its own — the dust already grows upwards out of that point.
        //
        // The canvas is wider than the dust because the frames are drawn inside it with air on both
        // sides: at 5.5 the widest impact frame measures 3.6 world units across, which is the 1.8
        // damage radius of BossMeteorAttack doubled. The telegraph circle and the explosion are
        // therefore the same size on screen, so either one teaches the player the same rule — if
        // this number moves, move the radius with it.
        private const float ImpactCanvasWorldSize = 5.5f;
        private const float FallCanvasWorldSize = 4.0f;

        // Frame 0 draws the boulder in the lower half of the canvas with its motion streaks above,
        // and the bottom-edge pivot would leave it hanging this far over the point the attack is
        // moving. The visual is pushed back down so the rock itself lands on the target.
        private const float FallBodyCenterPixels = 86f;

        private const float ImpactFrameDuration = 0.07f;

        [MenuItem("Swarm/Setup/Build Boss Rockfall Effects")]
        public static void RunFromMenu() => Execute(true);

        [InitializeOnLoadMethod]
        private static void AutoRun() => EditorApplication.delayCall += () => Execute(false);

        private static void Execute(bool verbose)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(AsepritePath)
                                       .OfType<Sprite>()
                                       .OrderBy(s => s.name, System.StringComparer.Ordinal)
                                       .ToArray();

            if (sprites.Length < 5)
            {
                if (verbose)
                {
                    Debug.LogError($"BossRockSetup: {AsepritePath} imported {sprites.Length} sprites, " +
                                   "expected 5 (Frame_0 falling, Frame_1..4 impact).");
                }

                return;
            }

            var fallPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FallPrefabPath);
            var impactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath);
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);

            // The child counts are a shape check, not decoration: an older build of these prefabs
            // had the offset on the wrong one, and comparing structure is what makes this script
            // rebuild them after a change here instead of trusting whatever is on disk.
            var upToDate = fallPrefab != null && impactPrefab != null &&
                           fallPrefab.transform.childCount == 1 &&
                           impactPrefab.transform.childCount == 0 &&
                           IsBossWired(boss, fallPrefab, impactPrefab);
            if (!verbose && upToDate) return;

            fallPrefab = BuildFallPrefab(sprites[0]);
            impactPrefab = BuildImpactPrefab(sprites.Skip(1).Take(4).ToArray());
            WireBoss(fallPrefab, impactPrefab, verbose);

            AssetDatabase.SaveAssets();
            Debug.Log("BossRockSetup: built Effect_BossRock_Fall / Effect_BossRock_Impact and wired " +
                      "them into Enemy_Boss's BossMeteorAttack.");
        }

        private static float ScaleFor(float canvasWorldSize) =>
            canvasWorldSize / (CanvasPixels / PixelsPerUnit);

        private static GameObject BuildFallPrefab(Sprite sprite)
        {
            var root = new GameObject("Effect_BossRock_Fall");
            try
            {
                var scale = ScaleFor(FallCanvasWorldSize);

                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform);
                visual.transform.localPosition = new Vector3(0f, -FallBodyCenterPixels / PixelsPerUnit * scale, 0f);
                visual.transform.localScale = new Vector3(scale, scale, 1f);

                var renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingLayerName = SortingLayers.EFFECT;
                renderer.sortingOrder = 6;

                return PrefabUtility.SaveAsPrefabAsset(root, FallPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildImpactPrefab(Sprite[] frames)
        {
            var root = new GameObject("Effect_BossRock_Impact");
            try
            {
                var scale = ScaleFor(ImpactCanvasWorldSize);
                root.transform.localScale = new Vector3(scale, scale, 1f);

                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.sortingLayerName = SortingLayers.EFFECT;
                renderer.sortingOrder = 5;

                var flash = root.AddComponent<StrikeFlash>();
                var so = new SerializedObject(flash);
                var frameArray = so.FindProperty("frames");
                frameArray.arraySize = frames.Length;
                for (var i = 0; i < frames.Length; i++)
                {
                    frameArray.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                }

                so.FindProperty("frameDuration").floatValue = ImpactFrameDuration;
                // Only used by the fade path StrikeFlash takes when it has no frames; kept in step
                // with the strip so the component reads consistently in the inspector.
                so.FindProperty("duration").floatValue = frames.Length * ImpactFrameDuration;
                so.ApplyModifiedPropertiesWithoutUndo();

                return PrefabUtility.SaveAsPrefabAsset(root, ImpactPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void WireBoss(GameObject fallPrefab, GameObject impactPrefab, bool verbose)
        {
            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                var attack = root.GetComponent<Swarm.Enemy.BossMeteorAttack>();
                if (attack == null)
                {
                    if (verbose) Debug.LogError("BossRockSetup: Enemy_Boss has no BossMeteorAttack.");
                    return;
                }

                var so = new SerializedObject(attack);
                so.FindProperty("fallingRockPrefab").objectReferenceValue = fallPrefab;
                so.FindProperty("impactEffectPrefab").objectReferenceValue = impactPrefab;
                // The strip finishes in 0.28s and StrikeFlash releases itself; this is only the
                // safety net that clears the empty root the effect leaves behind.
                so.FindProperty("impactEffectLifetime").floatValue = 0.5f;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool IsBossWired(GameObject boss, GameObject fallPrefab, GameObject impactPrefab)
        {
            if (boss == null) return false;

            var attack = boss.GetComponent<Swarm.Enemy.BossMeteorAttack>();
            if (attack == null) return false;

            var so = new SerializedObject(attack);
            return so.FindProperty("fallingRockPrefab").objectReferenceValue == fallPrefab &&
                   so.FindProperty("impactEffectPrefab").objectReferenceValue == impactPrefab;
        }
    }
}
