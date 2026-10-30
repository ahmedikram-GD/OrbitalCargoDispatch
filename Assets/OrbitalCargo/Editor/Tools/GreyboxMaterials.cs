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
    }
}
