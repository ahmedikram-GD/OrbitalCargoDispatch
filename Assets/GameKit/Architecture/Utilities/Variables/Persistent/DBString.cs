using UnityEngine;
using GameKit.Architecture.Variables;
using Sirenix.OdinInspector;

namespace GameKit.Architecture.Variables
{

[CreateAssetMenu(fileName = "vDBString_", menuName = "ProjectCore/Variables/Persistent/DBString")]
public class DBString : SharedString
{
    [SerializeField] private string key;

    // PlayerPrefs is the ONLY source of truth at runtime; the serialized Value is just what the editor last
    // baked into the asset (editor play sessions dirty SO assets, and those values SHIP in a build). [NonSerialized]
    // so a fresh player/domain-reload always starts un-hydrated: the first access re-reads PlayerPrefs even if this
    // asset's OnEnable hasn't run yet — ScriptableObject OnEnable order between assets is UNDEFINED, and a consumer
    // SO (e.g. RaceService.OnEnable reading its save blob) can otherwise see the baked editor state on device.
    [System.NonSerialized] private bool _hydrated;

    private void OnEnable()
    {
        Load();
    }

    private void EnsureHydrated()
    {
        if (!_hydrated) Load();
    }

    public override string GetValue()
    {
        EnsureHydrated();
        return base.GetValue();
    }

    public override void SetValue(string value)
    {
        _hydrated = true; // the caller's value is now the runtime truth — a later lazy Load must not clobber it
        base.SetValue(value);
    }
    [Button(ButtonSizes.Small)]
    public void Save()
    {
        PlayerPrefs.SetString(key, Value);
        PlayerPrefs.Save(); // Ensure the data is saved
    }
    private void Load()
    {
        _hydrated = true;
        // Check if the key exists in PlayerPrefs
        if (PlayerPrefs.HasKey(key))
        {
            // Load value from PlayerPrefs using the generated key
            Value = PlayerPrefs.GetString(key);
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