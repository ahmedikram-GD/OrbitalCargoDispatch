using GameKit.Architecture.Variables;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameKit.Architecture.Variables
{

[CreateAssetMenu(fileName = "vDBBool_", menuName = "ProjectCore/Variables/Persistent/DBBool")]
public class DBBool : Bool
{
    [SerializeField] private string key;

    // PlayerPrefs is the ONLY runtime source of truth; the serialized Value is editor residue that ships in
    // builds. [NonSerialized] guard so the FIRST access hydrates from PlayerPrefs even when this asset's OnEnable
    // hasn't run yet (ScriptableObject OnEnable order between assets is undefined). See DBString for rationale.
    [System.NonSerialized] private bool _hydrated;

    private void OnEnable()
    {
        Load();
    }

    private void EnsureHydrated()
    {
        if (!_hydrated) Load();
    }

    public override bool GetValue()
    {
        EnsureHydrated();
        return base.GetValue();
    }

    public override void SetValue(bool value)
    {
        _hydrated = true; // caller's value is now the truth — a later lazy Load must not clobber it
        base.SetValue(value);
        Save();
    }

    [Button(ButtonSizes.Small)]
    private void Save()
    {
        PlayerPrefs.SetInt(key, Value ? 1 : 0);
        PlayerPrefs.Save(); // Ensure the data is saved
    }

    public virtual bool GetBool(string key)
    {
        return PlayerPrefs.GetInt(key) == 1;
    }

    private void Load()
    {
        _hydrated = true;
        // Check if the key exists in PlayerPrefs
        if (PlayerPrefs.HasKey(key))
        {
            // Load value from PlayerPrefs using the generated key
            Value = GetBool(key);
        }
        else
        {
            // If the key doesn't exist, use the default value and save it to PlayerPrefs
            Value = DefaultValue;
            Save();
        }
    }
}

}