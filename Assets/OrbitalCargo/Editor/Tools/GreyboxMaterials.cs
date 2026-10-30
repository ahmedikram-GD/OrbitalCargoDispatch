using System.IO;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Room.EditorTools
{
    /// <summary>
    /// Creates (once) and returns flat-colour URP Lit materials for greybox geometry, saved under
    /// <c>Assets/OrbitalCargo/Materials/Greybox/M_&lt;Role&gt;.mat</c>. Existing assets are reused and
    /// never recoloured, so a designer can tweak a material without the builder overwriting it.
    /// </summary>
    public static class GreyboxMaterials
    {
        private const string Folder = "Assets/OrbitalCargo/Materials/Greybox";
        private const string LitShader = "Universal Render Pipeline/Lit";

        public static Material Get(string role, Color color, float emission = 0f)
        {
            string path = $"{Folder}/M_{role}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(Folder);
            var shader = Shader.Find(LitShader);
            if (shader == null)
            {
                Debug.LogError($"[GreyboxMaterials] Shader '{LitShader}' not found — is URP the active pipeline?");
                return null;
            }

            var mat = new Material(shader) { name = $"M_{role}" };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.25f);
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>Soft alpha-blended URP particle material using Unity's built-in round particle texture.</summary>
        public static Material GetParticle(string role, Color tint)
        {
            string path = $"{Folder}/M_{role}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(Folder);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("[GreyboxMaterials] URP Particles/Unlit shader not found.");
                return null;
            }

            var mat = new Material(shader) { name = $"M_{role}" };
            mat.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        /// <summary>
        /// Alpha-blended URP Unlit material (ghosts, x-ray casing previews). Alpha comes from
        /// <paramref name="color"/>.a. Created once like <see cref="Get"/>.
        /// </summary>
        public static Material GetTransparent(string role, Color color)
        {
            string path = $"{Folder}/M_{role}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Directory.CreateDirectory(Folder);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("[GreyboxMaterials] URP Unlit shader not found — is URP the active pipeline?");
                return null;
            }

            var mat = new Material(shader) { name = $"M_{role}" };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 1f); // Transparent
            mat.SetFloat("_Blend", 0f);   // Alpha
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
