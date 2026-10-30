using GameKit.Architecture.Variables;
using OrbitalCargo.Flow.EditorTools;
using OrbitalCargo.Shift.EditorTools;
using UnityEditor;
using UnityEngine;

namespace OrbitalCargo.Economy.EditorTools
{
    /// <summary>
    /// Pitch-rehearsal reset: credits 0, collection 0, back to shift 1, grav-sled not owned. Goes through
    /// the persistent variables (which write PlayerPrefs) so the Editor's already-hydrated assets agree
    /// with what the next play session loads.
    /// </summary>
    public static class ProgressTools
    {
        [MenuItem("Tools/OrbitalCargo/Reset Progress")]
        public static void ResetProgress()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[ProgressTools] Stop play mode before resetting progress.");
                return;
            }
            if (!EditorUtility.DisplayDialog("Reset progress",
                    "Set credits and collection to 0, go back to shift 1 and remove the grav-sled?", "Reset", "Cancel"))
                return;

            AssetDatabase.LoadAssetAtPath<DBInt>(CoreAssetFactory.CreditsPath)?.SetValue(0);
            AssetDatabase.LoadAssetAtPath<DBInt>(CoreAssetFactory.ItemsCollectedPath)?.SetValue(0);
            AssetDatabase.LoadAssetAtPath<DBInt>(FlowAssetFactory.ShiftNumberPath)?.SetValue(1);
            AssetDatabase.LoadAssetAtPath<DBBool>(FlowAssetFactory.GravSledPath)?.SetValue(false);
            Debug.Log("[ProgressTools] Progress reset: 0 credits, 0 items, shift 1, no grav-sled.");
        }
    }
}
