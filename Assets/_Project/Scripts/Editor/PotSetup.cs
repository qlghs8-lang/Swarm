using System.Linq;
using Swarm.Game;
using UnityEditor;
using UnityEngine;

namespace Swarm.EditorTools
{
    /// <summary>
    /// Builds the breakable pot and its two new drop prefabs out of Pot.aseprite and
    /// HealthPack/Magnet.png, and wires the drop table's prefabs onto the pot.
    ///
    /// Same reason as BossRockSetup: the Aseprite importer assigns sprite sub-asset ids at import
    /// time, so those references cannot be written into prefab YAML from outside Unity. It is
    /// idempotent and safe to leave in the project.
    ///
    /// The pot prefab lands in Resources because PotScatterer instantiates it at scene load
    /// without anything in a scene pointing at it.
    /// </summary>
    public static class PotSetup
    {
        private const string PotAsepritePath = "Assets/_Project/Textures/Props/Pot.aseprite";
        private const string HealthPackTexturePath = "Assets/_Project/Textures/Pickups/HealthPack.png";
        private const string MagnetTexturePath = "Assets/_Project/Textures/Pickups/Magnet.png";

        private const string PotPrefabPath = "Assets/_Project/Resources/Props/Prop_Pot.prefab";
        private const string HealthPackPrefabPath = "Assets/_Project/Prefabs/HealthPackPickup.prefab";
        private const string MagnetPrefabPath = "Assets/_Project/Prefabs/MagnetPickup.prefab";
        private const string GoldPrefabPath = "Assets/_Project/Prefabs/GoldPickup.prefab";
        private const string ExperiencePrefabPath = "Assets/_Project/Prefabs/ExperiencePickup.prefab";

        private const int EnemyLayer = 7;
        private const int PickupLayer = 8;

        // The canvas is 48px tall at 32 PPU with the ground line on its bottom edge, so the pot
        // body sits between 0.1 and 0.97 units above the transform. The trigger covers the body
        // and nothing else: a collider as tall as the canvas would let a stray arrow break a pot
        // it visibly flew over.
        // Written onto the prefab rather than left to the component's own default: the prefab
        // serialises whatever the field held the day it was built, so a later change to the
        // default would never reach a HealthPackPickup.prefab that already exists on disk.
        private const float HealPercent = 0.1f;

        private const float BodyRadius = 0.34f;
        private const float BodyCentreHeight = 0.5f;

        [MenuItem("Swarm/Setup/Build Breakable Pot")]
        public static void RunFromMenu() => Execute(true);

        [InitializeOnLoadMethod]
        private static void AutoRun() => EditorApplication.delayCall += () => Execute(false);

        private static void Execute(bool verbose)
        {
            var potSprites = AssetDatabase.LoadAllAssetsAtPath(PotAsepritePath)
                                          .OfType<Sprite>()
                                          .OrderBy(s => s.name, System.StringComparer.Ordinal)
                                          .ToArray();

            if (potSprites.Length < 6)
            {
                if (verbose)
                {
                    Debug.LogError($"PotSetup: {PotAsepritePath} imported {potSprites.Length} sprites, " +
                                   "expected 6 (Frame_0 idle, Frame_1..5 break).");
                }

                return;
            }

            var healthSprite = AssetDatabase.LoadAssetAtPath<Sprite>(HealthPackTexturePath);
            var magnetSprite = AssetDatabase.LoadAssetAtPath<Sprite>(MagnetTexturePath);
            var goldPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoldPrefabPath);
            var experiencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ExperiencePrefabPath);

            if (healthSprite == null || magnetSprite == null)
            {
                if (verbose) Debug.LogError("PotSetup: HealthPack.png or Magnet.png is missing.");
                return;
            }

            var upToDate = IsPotWired(AssetDatabase.LoadAssetAtPath<GameObject>(PotPrefabPath)) &&
                           IsHealthPackTuned(AssetDatabase.LoadAssetAtPath<GameObject>(HealthPackPrefabPath)) &&
                           AssetDatabase.LoadAssetAtPath<GameObject>(MagnetPrefabPath) != null;
            if (!verbose && upToDate) return;

            var healthPrefab = BuildPickup("HealthPackPickup", HealthPackPrefabPath, healthSprite,
                                           typeof(HealthPackPickup), pickup =>
                                           {
                                               var serialized = new SerializedObject(pickup);
                                               serialized.FindProperty("healPercent").floatValue = HealPercent;
                                               serialized.ApplyModifiedPropertiesWithoutUndo();
                                           });
            var magnetPrefab = BuildPickup("MagnetPickup", MagnetPrefabPath, magnetSprite,
                                           typeof(MagnetPickup));
            BuildPot(potSprites, experiencePrefab, goldPrefab, healthPrefab, magnetPrefab);

            AssetDatabase.SaveAssets();
            Debug.Log("PotSetup: built Prop_Pot, HealthPackPickup and MagnetPickup.");
        }

        /// <summary>A pot built before a drop was added to the table still exists on disk with a
        /// hole in it, and only a field check catches that — the prefab being present does not
        /// mean it is current.</summary>
        private static bool IsPotWired(GameObject prefab)
        {
            if (prefab == null || !prefab.TryGetComponent<BreakablePot>(out var pot)) return false;

            var serialized = new SerializedObject(pot);
            return serialized.FindProperty("experiencePickupPrefab").objectReferenceValue != null &&
                   serialized.FindProperty("goldPickupPrefab").objectReferenceValue != null &&
                   serialized.FindProperty("healthPackPrefab").objectReferenceValue != null &&
                   serialized.FindProperty("magnetPickupPrefab").objectReferenceValue != null;
        }

        private static bool IsHealthPackTuned(GameObject prefab)
        {
            if (prefab == null || !prefab.TryGetComponent<HealthPackPickup>(out var pickup)) return false;

            var serialized = new SerializedObject(pickup);
            return Mathf.Approximately(serialized.FindProperty("healPercent").floatValue, HealPercent);
        }

        private static GameObject BuildPickup(string name, string path, Sprite sprite, System.Type pickupType,
                                              System.Action<Component> configure = null)
        {
            var root = new GameObject(name) { layer = PickupLayer };
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingLayerName = SortingLayers.PICKUP;

                var body = root.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;

                var collider = root.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.3f;

                var pickup = root.AddComponent(pickupType);
                root.AddComponent<MagnetAttractable>();
                configure?.Invoke(pickup);

                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void BuildPot(Sprite[] sprites, GameObject experiencePrefab, GameObject goldPrefab,
                                     GameObject healthPrefab, GameObject magnetPrefab)
        {
            var root = new GameObject("Prop_Pot") { layer = EnemyLayer };
            try
            {
                // Enemy layer and Enemy tag: every weapon finds what it can hit with an
                // Enemy-layer overlap and a tag check, so this is what makes all fourteen of them
                // break a pot with no change to any of them. BreakableRegistry keeps auto-aim off.
                root.tag = "Enemy";

                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = sprites[0];
                renderer.sortingLayerName = SortingLayers.PROP;
                renderer.sortingOrder = 1;

                var collider = root.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = BodyRadius;
                collider.offset = new Vector2(0f, BodyCentreHeight);

                var pot = root.AddComponent<BreakablePot>();
                var serialized = new SerializedObject(pot);
                serialized.FindProperty("idleSprite").objectReferenceValue = sprites[0];

                var frames = serialized.FindProperty("breakFrames");
                frames.arraySize = sprites.Length - 1;
                for (var i = 1; i < sprites.Length; i++)
                {
                    frames.GetArrayElementAtIndex(i - 1).objectReferenceValue = sprites[i];
                }

                serialized.FindProperty("experiencePickupPrefab").objectReferenceValue = experiencePrefab;
                serialized.FindProperty("goldPickupPrefab").objectReferenceValue = goldPrefab;
                serialized.FindProperty("healthPackPrefab").objectReferenceValue = healthPrefab;
                serialized.FindProperty("magnetPickupPrefab").objectReferenceValue = magnetPrefab;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PotPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
