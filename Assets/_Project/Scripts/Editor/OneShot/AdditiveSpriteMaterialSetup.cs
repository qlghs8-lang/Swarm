using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Swarm.EditorTools
{
    public static class AdditiveSpriteMaterialSetup
    {
        private const string MaterialDirectory = "Assets/_Project/Materials";
        private const string MaterialPath = MaterialDirectory + "/SpriteAdditive.mat";

        [MenuItem("Swarm/Art Test/Create Additive Sprite Material")]
        private static void Create()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("Could not find shader 'Universal Render Pipeline/Unlit'. Is URP installed?");
                return;
            }

            var material = new Material(shader) { name = "SpriteAdditive" };

            // Transparent surface, Additive blend (One + One), no depth write, no alpha clip -
            // set both the high-level surface/blend properties AND the raw blend-factor
            // properties directly, since URP's Lit/Unlit ShaderGUI normally keeps these in sync
            // and a script-created material won't have had that GUI pass run on it.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)BlendMode.One);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHATEST_ON");

            if (!Directory.Exists(MaterialDirectory))
            {
                Directory.CreateDirectory(MaterialDirectory);
            }

            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(material, existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
                Debug.Log($"Updated existing additive sprite material at {MaterialPath}.");
            }
            else
            {
                AssetDatabase.CreateAsset(material, MaterialPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"Created additive sprite material at {MaterialPath}.");
            }
        }
    }
}
