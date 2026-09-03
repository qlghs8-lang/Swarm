using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEngine;

namespace Swarm.EditorTools
{
    /// <summary>
    /// Builds the impact effect prefab for the boss ground slam out of bossslam.aseprite and hands
    /// it to <c>BossSlamAttack</c> on Enemy_Boss.
    ///
    /// The strip is three frames of one event: the burst at the moment of contact, the shockwave
    /// spread at its widest, and the dust fading out. A single <c>StrikeFlash</c> steps them once
    /// and releases itself, the same shape <c>BossRockSetup</c> uses for the rockfall impact.
    ///
    /// Like BossRockSetup this exists because the Aseprite importer assigns the sprite sub-asset ids
    /// at import time, so the references cannot be written into the prefab YAML from outside Unity.
    /// It is idempotent and safe to leave in the project.
    /// </summary>
    public static class BossSlamSetup
    {
        private const string AsepritePath = "Assets/_Project/Textures/Effects/bossslam.aseprite";
        private const string ImpactPrefabPath = "Assets/_Project/Prefabs/Effect_BossSlam_Impact.prefab";
        private const string BossPrefabPath = "Assets/_Project/Prefabs/Enemy_Boss.prefab";

        private const float CanvasPixels = 512f;
        private const float PixelsPerUnit = 100f;

        // The art is drawn centred on the point of impact, so the prefab needs no offset of its own
        // -- but only because bossslam.aseprite is imported with Pivot Alignment = Center in Canvas
        // space. The importer's own default is Bottom Center, which is what the rockfall art wants
        // (its ground line sits on the canvas bottom edge) and what this strip must not have: with
        // a bottom pivot the whole 16.9 unit canvas is drawn upwards out of the spawn point and the
        // dust lands above the boss's head instead of on the telegraph circle. If the .meta is ever
        // regenerated, put the alignment back to Center.
        //
        // The widest frame covers 485 of the canvas's 512 pixels, so a 16.9 unit canvas puts that
        // frame at 16 units across — the 8 unit damage radius of BossSlamAttack doubled. The
        // telegraph circle and the dust therefore teach the player the same rule; if the radius
        // moves, move this with it.
        private const float ImpactCanvasWorldSize = 16.9f;

        // The dust disc is not centred on the canvas: measured across the three frames, its centre
        // of mass sits about 32 of the 512 pixels below the middle (the disc also settles downward
        // as it spreads, which is what sells the perspective). The pivot is the canvas centre, so
        // spawning the sprite on the impact point drew the disc a full unit low -- the telegraph
        // circle and the dust no longer described the same patch of ground, and the slam read as
        // landing in front of the boss's feet rather than under it. The effect is spawned that
        // much higher instead of re-pivoting the art, which would move every frame independently.
        private const float ImpactArtCenterOffsetPixels = 32f;
        private const float ImpactFrameDuration = 0.09f;

        // The strip already carries a 100/85/45% fade of its own, baked into the art so the dust
        // dissolves rather than popping out. This is the overall level on top of that: the effect
        // covers a 16 unit circle of floor, and at full opacity a slab that size reads as a sticker
        // laid over the arena instead of dust thrown up from it. StrikeFlash restores this colour
        // on every reuse, so lowering it here is enough.
        private const float ImpactOpacity = 0.7f;

        [MenuItem("Swarm/Setup/Build Boss Slam Effect")]
        public static void RunFromMenu() => Execute(true);

        [InitializeOnLoadMethod]
        private static void AutoRun() => EditorApplication.delayCall += () => Execute(false);

        private static void Execute(bool verbose)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(AsepritePath)
                                       .OfType<Sprite>()
                                       .OrderBy(s => s.name, System.StringComparer.Ordinal)
                                       .ToArray();

            if (sprites.Length < 3)
            {
                if (verbose)
                {
                    Debug.LogError($"BossSlamSetup: {AsepritePath} imported {sprites.Length} sprites, " +
                                   "expected 3 (Frame_0 burst, Frame_1 spread, Frame_2 fade).");
                }

                return;
            }

            var impactPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactPrefabPath);
            var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);

            // Re-authoring the .aseprite gives every frame a new sub-asset id, so a prefab that is
            // still wired to the boss can be holding sprite references that no longer resolve.
            // Comparing the frames as well as the wiring is what makes the art rebuild itself.
            var upToDate = impactPrefab != null && IsBossWired(boss, impactPrefab) &&
                           HasFrames(impactPrefab, sprites.Take(3).ToArray()) &&
                           HasScale(impactPrefab) && HasOpacity(impactPrefab);
            if (!verbose && upToDate) return;

            impactPrefab = BuildImpactPrefab(sprites.Take(3).ToArray());
            WireBoss(impactPrefab, verbose);

            AssetDatabase.SaveAssets();
            Debug.Log("BossSlamSetup: built Effect_BossSlam_Impact and wired it into Enemy_Boss's " +
                      "BossSlamAttack.");
        }

        private static GameObject BuildImpactPrefab(Sprite[] frames)
        {
            var root = new GameObject("Effect_BossSlam_Impact");
            try
            {
                var scale = ImpactScale;
                root.transform.localScale = new Vector3(scale, scale, 1f);

                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = frames[0];
                renderer.color = new Color(1f, 1f, 1f, ImpactOpacity);
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

        private static void WireBoss(GameObject impactPrefab, bool verbose)
        {
            var root = PrefabUtility.LoadPrefabContents(BossPrefabPath);
            try
            {
                var attack = root.GetComponent<Swarm.Enemy.BossSlamAttack>();
                if (attack == null)
                {
                    if (verbose) Debug.LogError("BossSlamSetup: Enemy_Boss has no BossSlamAttack.");
                    return;
                }

                var so = new SerializedObject(attack);
                so.FindProperty("impactEffectPrefab").objectReferenceValue = impactPrefab;
                // The strip finishes in 0.27s and StrikeFlash releases itself; this is only the
                // safety net that clears the empty root the effect leaves behind.
                so.FindProperty("impactEffectLifetime").floatValue = 0.5f;
                so.FindProperty("impactEffectOffset").vector2Value = ImpactSpawnOffset;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, BossPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // The canvas grew from 256 to 512 pixels and the prefab kept the scale computed for the old
        // one, so the dust was drawn at twice the width of the telegraph it is supposed to match.
        // The scale is checked here rather than trusted, because nothing else in the prefab changes
        // when the art's resolution does.
        private static bool HasScale(GameObject impactPrefab) =>
            Mathf.Approximately(impactPrefab.transform.localScale.x, ImpactScale);

        private static float ImpactScale => ImpactCanvasWorldSize / (CanvasPixels / PixelsPerUnit);

        private static Vector2 ImpactSpawnOffset =>
            new(0f, ImpactArtCenterOffsetPixels * ImpactCanvasWorldSize / CanvasPixels);

        private static bool HasOpacity(GameObject impactPrefab)
        {
            var renderer = impactPrefab.GetComponent<SpriteRenderer>();
            return renderer != null && Mathf.Approximately(renderer.color.a, ImpactOpacity);
        }

        private static bool HasFrames(GameObject impactPrefab, Sprite[] expected)
        {
            var flash = impactPrefab.GetComponent<StrikeFlash>();
            if (flash == null) return false;

            var frames = new SerializedObject(flash).FindProperty("frames");
            if (frames.arraySize != expected.Length) return false;

            for (var i = 0; i < expected.Length; i++)
            {
                if (frames.GetArrayElementAtIndex(i).objectReferenceValue != expected[i]) return false;
            }

            return true;
        }

        private static bool IsBossWired(GameObject boss, GameObject impactPrefab)
        {
            if (boss == null) return false;

            var attack = boss.GetComponent<Swarm.Enemy.BossSlamAttack>();
            if (attack == null) return false;

            var so = new SerializedObject(attack);
            if (so.FindProperty("impactEffectPrefab").objectReferenceValue != impactPrefab) return false;

            // Checked rather than trusted for the same reason the scale is: a boss prefab authored
            // before the offset existed carries a zero here and would otherwise never be corrected.
            var offset = so.FindProperty("impactEffectOffset").vector2Value;
            return (offset - ImpactSpawnOffset).sqrMagnitude < 0.0001f;
        }
    }
}
