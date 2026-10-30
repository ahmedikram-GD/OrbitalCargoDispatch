using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OrbitalCargo.Interaction
{
    /// <summary>
    /// <see cref="IHighlightable"/> that outlines the whole item QuickOutline-style without the asset:
    /// it bakes smoothed normals into the mesh (UV3) so hard edges don't gap, combines submeshes so
    /// multi-material meshes outline fully, and appends a mask + fill material pair (stencil) whose
    /// view-space expansion gives an even, constant-screen-width band. Highlighting only appends or
    /// removes those two materials; base materials are never instanced. Ported from Silly-Justice.
    /// </summary>
    [AddComponentMenu("OrbitalCargo/Interaction/Highlightable Item")]
    public sealed class HighlightableItem : MonoBehaviour, IHighlightable
    {
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int ZTestId = Shader.PropertyToID("_ZTest");

        // Meshes only need their smoothed normals / combined submesh baked once, project-wide.
        private static readonly HashSet<Mesh> BakedMeshes = new HashSet<Mesh>();

        // Reused for every material add/remove, so toggling the outline allocates nothing.
        private static readonly List<Material> MaterialBuffer = new List<Material>(8);

        [Tooltip("Outline look asset (FP_HighlightConfig) — holds the mask/fill materials plus colour, width, mode, pulse.")]
        [SerializeField] private HighlightConfigSO config;

        private Renderer[] _renderers;
        private Material _mask;
        private Material _fill;
        private bool _on;

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);

            if (config == null || config.MaskMaterial == null || config.FillMaterial == null)
            {
                Debug.LogWarning($"[HighlightableItem] '{name}': no HighlightConfigSO with Mask/Fill materials — highlight disabled.", this);
                return;
            }

            _mask = new Material(config.MaskMaterial) { name = "OutlineMask (Instance)" };
            _fill = new Material(config.FillMaterial) { name = "OutlineFill (Instance)" };

            BakeMeshes();
            ApplyConfig(config.OutlineWidth);
        }

        private void Update()
        {
            if (!_on || _fill == null || config == null || !config.Pulse) return;
            float t = Mathf.Sin(Time.unscaledTime * config.PulseSpeed) * 0.5f + 0.5f;
            _fill.SetFloat(OutlineWidthId, Mathf.Lerp(config.OutlineWidth * (1f - config.PulseAmount),
                                                      config.OutlineWidth * (1f + config.PulseAmount), t));
        }

        private void OnDisable() => SetHighlighted(false);

        private void OnDestroy()
        {
            if (_mask != null) Destroy(_mask);
            if (_fill != null) Destroy(_fill);
        }

        // ── IHighlightable ─────────────────────────────────────────────────────

        public void SetHighlighted(bool on)
        {
            if (_on == on || _mask == null || _fill == null || _renderers == null) return;
            _on = on;

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                r.GetSharedMaterials(MaterialBuffer);
                if (on)
                {
                    if (!MaterialBuffer.Contains(_mask)) MaterialBuffer.Add(_mask);
                    if (!MaterialBuffer.Contains(_fill)) MaterialBuffer.Add(_fill);
                }
                else
                {
                    MaterialBuffer.Remove(_mask);
                    MaterialBuffer.Remove(_fill);
                }
                r.SetSharedMaterials(MaterialBuffer);
            }
            MaterialBuffer.Clear(); // don't keep materials alive from a static list
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private void BakeMeshes()
        {
            foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable || !BakedMeshes.Add(mesh)) continue;
                mesh.SetUVs(3, SmoothNormals(mesh));
                var r = mf.GetComponent<Renderer>();
                if (r != null) CombineSubmeshes(mesh, r.sharedMaterials.Length);
            }
        }

        private static List<Vector3> SmoothNormals(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var smooth = new List<Vector3>(mesh.normals);
            var groups = new Dictionary<Vector3, List<int>>();
            for (int i = 0; i < vertices.Length; i++)
            {
                if (!groups.TryGetValue(vertices[i], out var list))
                {
                    list = new List<int>();
                    groups[vertices[i]] = list;
                }
                list.Add(i);
            }
            foreach (var group in groups.Values)
            {
                if (group.Count == 1) continue;
                Vector3 n = Vector3.zero;
                foreach (var idx in group) n += smooth[idx];
                n.Normalize();
                foreach (var idx in group) smooth[idx] = n;
            }
            return smooth;
        }

        private static void CombineSubmeshes(Mesh mesh, int materialCount)
        {
            if (mesh.subMeshCount == 1) return;
            if (mesh.subMeshCount > materialCount) return;
            mesh.subMeshCount++;
            mesh.SetTriangles(mesh.triangles, mesh.subMeshCount - 1); // last submesh = whole mesh
        }

        private void ApplyConfig(float width)
        {
            _fill.SetColor(OutlineColorId, config.OutlineColor);
            _fill.SetFloat(OutlineWidthId, width);

            var fillZ = CompareFunction.Always;
            if (config.Mode == OutlineMode.OutlineVisible) fillZ = CompareFunction.LessEqual;
            else if (config.Mode == OutlineMode.OutlineHidden) fillZ = CompareFunction.Greater;
            _mask.SetFloat(ZTestId, (float)CompareFunction.Always);
            _fill.SetFloat(ZTestId, (float)fillZ);
        }
    }
}
