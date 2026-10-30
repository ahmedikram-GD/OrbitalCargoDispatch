using UnityEngine;
using Lofelt.NiceVibrations; // New Namespace
using GameKit.Architecture.Variables;
//using GameKit.Architecture.Variables;  // Assuming DBInt is in this namespace based on your snippet

namespace GameKit.Haptics
{
    public enum HapticIntensity
    {
        Selection,
        Light,
        Soft,
        Medium,
        Hard,
        Success,
        Failure
    }

    [CreateAssetMenu(fileName = "HapticHandlerSO", menuName = "Haptic/HapticHandlerSO", order = 1)]
    public class HapticHandlerSO : ScriptableObject
    {
        // Updated to use the DBInt from your new system
        [SerializeField] private DBBoolWithEvent vibrations;

   

        public void ApplyHapticFeedback(HapticIntensity intensity)
        {
            if (!vibrations.GetValue())
            {
                return;
            }

            switch (intensity)
            {
                case HapticIntensity.Selection:
                    HapticPatterns.PlayPreset(HapticPatterns.PresetType.Selection);
                    break;
                case HapticIntensity.Light:
                    HapticPatterns.PlayPreset(HapticPatterns.PresetType.LightImpact);
                    break;
                case HapticIntensity.Soft:
                    HapticPatterns.PlayPreset(HapticPatterns.PresetType.SoftImpact);
                    break;
                case HapticIntensity.Medium:
                    HapticPatterns.PlayPreset(HapticPatterns.PresetType.MediumImpact);
                    break;
                case HapticIntensity.Hard:
                    // 'Rigid' or 'Heavy' are the closest to the old Hard/Heavy
                    HapticPatterns.PlayPreset(HapticPatterns.PresetType.HeavyImpact);
                    break;
                case HapticIntensity.Success:
                    HapticPatterns.PlayPreset(HapticPatterns.PresetType.Success);
                    break;
                case HapticIntensity.Failure:
                    HapticPatterns.PlayPreset(HapticPatterns.PresetType.Failure);
                    break;
                default:
                    Debug.LogWarning("[Haptic] Unknown haptic intensity.");
                    break;
            }
        }

        public void ApplyDynamicHaptic(float intensity01)
        {
            if (!vibrations.GetValue())
            {
                return;
            }

            intensity01 = Mathf.Clamp01(intensity01);

            // Mapping 0-1 range to the new Lofelt Presets
            if (intensity01 < 0.2f)
            {
                HapticPatterns.PlayPreset(HapticPatterns.PresetType.LightImpact);
            }
            else if (intensity01 < 0.4f)
            {
                HapticPatterns.PlayPreset(HapticPatterns.PresetType.SoftImpact);
            }
            else if (intensity01 < 0.6f)
            {
                HapticPatterns.PlayPreset(HapticPatterns.PresetType.MediumImpact);
            }
            else if (intensity01 < 0.8f)
            {
                HapticPatterns.PlayPreset(HapticPatterns.PresetType.HeavyImpact);
            }
            else
            {
                // Rigid impact is very strong in the new API
                HapticPatterns.PlayPreset(HapticPatterns.PresetType.RigidImpact);
            }
        }
    }
}