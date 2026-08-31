using System.Linq;
using Swarm.Weapon;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class MeteorEffectLinker
    {
        private const string MeteorAsepritePath = "Assets/_Project/Textures/Effects/meteor.aseprite";
        private const string MeteorFloorAsepritePath = "Assets/_Project/Textures/Effects/meteorfloor.aseprite";
        private const string AdditiveMaterialPath = "Assets/_Project/Materials/SpriteAdditive.mat";
        private const int TravelFrameCount = 3;
        private const float MeteorVisualScale = 2.5f;
        private const float MeteorImpactEffectScale = 2.5f;

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Game.unity",
            "Assets/_Project/Scenes/TestStage.unity",
        };

        [MenuItem("Swarm/Art Test/Link Mage Explosion (Meteor + Fire Patch) (Both Scenes)")]
        private static void Link()
        {
            var meteorFrames = AssetDatabase.LoadAllAssetsAtPath(MeteorAsepritePath)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            if (meteorFrames.Length == 0)
            {
                Debug.LogError($"No Sprite sub-assets found in {MeteorAsepritePath}.");
                return;
            }

            var firePatchFrames = AssetDatabase.LoadAllAssetsAtPath(MeteorFloorAsepritePath)
                .OfType<Sprite>()
                .OrderBy(s => s.name)
                .ToArray();

            if (firePatchFrames.Length == 0)
            {
                Debug.LogError($"No Sprite sub-assets found in {MeteorFloorAsepritePath}.");
                return;
            }

            var travelFrames = meteorFrames.Take(TravelFrameCount).ToArray();
            var impactFrames = meteorFrames.Skip(TravelFrameCount).ToArray();
            var additiveMaterial = AssetDatabase.LoadAssetAtPath<Material>(AdditiveMaterialPath);

            foreach (var scenePath in ScenePaths)
            {
                LinkInScene(scenePath, travelFrames, impactFrames, firePatchFrames, additiveMaterial);
            }

            Debug.Log($"Linked {travelFrames.Length} meteor travel frames + {impactFrames.Length} impact frames + " +
                      $"{firePatchFrames.Length} fire patch frames to MageExplosionWeapon in both scenes. " +
                      "Import 설정(PPU 50 / Pivot Space Canvas+Center / Sprite Mesh Type Full Rect)은 art-plan.md 3장 기준대로 수동 확인 필요.");
        }

        private static void LinkInScene(string scenePath, Sprite[] travelFrames, Sprite[] impactFrames, Sprite[] firePatchFrames, Material additiveMaterial)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError($"Player GameObject not found in {scenePath}. Skipped.");
                return;
            }

            var weapon = player.GetComponent<MageExplosionWeapon>();
            if (weapon == null)
            {
                Debug.LogError($"MageExplosionWeapon component not found on Player in {scenePath}. Skipped.");
                return;
            }

            var serializedObject = new SerializedObject(weapon);

            AssignFrames(serializedObject.FindProperty("meteorTravelFrames"), travelFrames);
            AssignFrames(serializedObject.FindProperty("meteorImpactEffectFrames"), impactFrames);
            AssignFrames(serializedObject.FindProperty("firePatchFrames"), firePatchFrames);
            serializedObject.FindProperty("meteorVisualScale").floatValue = MeteorVisualScale;
            serializedObject.FindProperty("meteorImpactEffectScale").floatValue = MeteorImpactEffectScale;
            if (additiveMaterial != null)
            {
                serializedObject.FindProperty("effectMaterial").objectReferenceValue = additiveMaterial;
            }

            serializedObject.ApplyModifiedProperties();

            EditorUtility.SetDirty(weapon);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static void AssignFrames(SerializedProperty property, Sprite[] frames)
        {
            property.arraySize = frames.Length;
            for (var i = 0; i < frames.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }
        }
    }
}
