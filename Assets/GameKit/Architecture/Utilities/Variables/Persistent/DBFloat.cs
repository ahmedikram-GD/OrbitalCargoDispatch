using GameKit.Architecture.Variables;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameKit.Architecture.Variables
{

[CreateAssetMenu(fileName = "vDBFloat_", menuName = "ProjectCore/Variables/Persistent/DBFloat")]
public class DBFloat : Float
{
    [SerializeField]private string key;
    private void OnEnable()
    {
        Load();
    }

    public override void Decrement(float _decrement)
    {
        base.Decrement(_decrement);
        Save();
    }

    public override void Increment(float _increment)
    {
        base.Increment(_increment);
        Save();
    }

    public override void SetValue(float value)
    {
        base.SetValue(value);
        Save();
    }
    

    [Button(ButtonSizes.Small)]
    private void Save()
    {
        PlayerPrefs.SetFloat(key, Value);
        PlayerPrefs.Save(); // Ensure the data is saved
    }

    private void Load()
    {
        // Check if the key exists in PlayerPrefs
        if (PlayerPrefs.HasKey(key))
        {
            // Load value from PlayerPrefs using the generated key
            Value = PlayerPrefs.GetFloat(key);
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