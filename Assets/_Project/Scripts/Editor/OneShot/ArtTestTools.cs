using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Swarm.EditorTools
{
    public static class ArtTestTools
    {
        private const string WarriorAsepritePath = "Assets/_Project/Textures/Player/Warrior.aseprite";

        [MenuItem("Swarm/Art Test/Swap Player To Warrior Idle Frame")]
        private static void SwapPlayerToWarriorIdle()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(WarriorAsepritePath);
            Sprite idleSprite = null;
            foreach (var asset in assets)
            {
                if (asset is Sprite sprite && sprite.name == "Frame_0")
                {
                    idleSprite = sprite;
                    break;
                }
            }

            if (idleSprite == null)
            {
                Debug.LogError("Frame_0 sprite not found in Warrior.aseprite. Check the import.");
                return;
            }

            var player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogError("Player GameObject not found in the active scene. Open Game.unity first.");
                return;
            }

            var spriteRenderer = player.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                Debug.LogError("Player has no SpriteRenderer.");
                return;
            }

            Undo.RecordObject(spriteRenderer, "Swap Player Sprite To Warrior");
            spriteRenderer.sprite = idleSprite;
            spriteRenderer.color = Color.white;

            EditorUtility.SetDirty(spriteRenderer);
            EditorSceneManager.MarkSceneDirty(spriteRenderer.gameObject.scene);

            Debug.Log("Player sprite swapped to Warrior Idle Frame_0. Save the scene (Ctrl+S) to persist.");
        }
    }
}
