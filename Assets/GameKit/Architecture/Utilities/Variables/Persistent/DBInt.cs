using GameKit.Architecture.Variables;
using Sirenix.OdinInspector;
using UnityEngine;

namespace GameKit.Architecture.Variables
{

[CreateAssetMenu(fileName = "vDBInt_", menuName = "ProjectCore/Variables/Persistent/DBInt")]
public class DBInt : Int
{
    [SerializeField]private string key;

    // PlayerPrefs is the ONLY runtime source of truth; the serialized Value is editor residue that ships in
    // builds. [NonSerialized] guard so the FIRST access hydrates from PlayerPrefs even when this asset's OnEnable
    // hasn't run yet (ScriptableObject OnEnable order between assets is undefined) — otherwise a consumer could
    // read/increment the baked editor value on device. See DBString for the full rationale.
    [System.NonSerialized] private bool _hydrated;

    // True when THIS session's hydrate found nothing stored — a fresh install, or a player who cleared data.
    // Remote config always lands AFTER the first hydrate, so once the value exists in PlayerPrefs there is no
    // way to tell a brand-new player from a returning one by looking at the key. This flag is that distinction,
    // captured at the only moment it is still knowable. See SeedStartingValue.
    [System.NonSerialized] private bool _isFreshInstall;

    private void OnEnable()
    {
        base.OnEnable();
        Load();
    }

    private void EnsureHydrated()
    {
        if (!_hydrated) Load();
    }

    public override void Decrement(int _decrement)
    {
        EnsureHydrated(); // +=/-= read the current value — it must be the persisted one, not the baked one
        base.Decrement(_decrement);
        Save();
    }

    public override void Increment(int _increment)
    {
        EnsureHydrated();
        base.Increment(_increment);
        Save();
    }

    public override void SetValue(int value)
    {
        _hydrated = true; // caller's value is now the truth — a later lazy Load must not clobber it
        base.SetValue(value);
        Save();
    }

    public override int GetValue()
    {
        EnsureHydrated();
        return base.GetValue();
    }

    [Button(ButtonSizes.Small)]
    private void Save()
    {
        PlayerPrefs.SetInt(key, Value);
        PlayerPrefs.Save(); // Ensure the data is saved
    }

    private void Load()
    {
        _hydrated = true;
        // Check if the key exists in PlayerPrefs
        if (PlayerPrefs.HasKey(key))
        {
            // Load value from PlayerPrefs using the generated key
            Value = PlayerPrefs.GetInt(key);
        }
        else
        {
            // If the key doesn't exist, use the default value and save it to PlayerPrefs
            _isFreshInstall = true;
            Value = DefaultValue;
            Save();
        }
    }

    /// <summary>
    /// Applies a remotely-configured STARTING amount: updates <c>DefaultValue</c>, and pushes it into
    /// the live value as well — but only for a player who has never had one stored.
    ///
    /// <para>
    /// <b>Why setting the default alone is not enough.</b> A <see cref="DBInt"/> hydrates the moment
    /// its asset loads, which is long before remote config arrives: <see cref="Load"/> has already
    /// copied <c>DefaultValue</c> into PlayerPrefs and every later read comes from there. By the time
    /// a remote value lands, <c>DefaultValue</c> is a number nothing will ever read again — this
    /// launch is past it, and the next launch finds the stored key instead. Hence the second write.
    /// </para>
    ///
    /// <para>
    /// <b>Why it is gated on <see cref="_isFreshInstall"/>.</b> Writing the value unconditionally would
    /// hand every returning player the starting amount on every launch — wiping a balance they spent,
    /// bought or earned, and handing out free charges to anyone sitting at a low count. The gate makes
    /// this a SEED, not a reset. It also fires at most once per session, so a config refetch mid-session
    /// cannot re-seed a player who has since spent something.
    /// </para>
    ///
    /// <para>
    /// A player who was already playing before this build shipped is correctly treated as returning:
    /// their key exists, so <see cref="Load"/> never set the flag. No migration step is needed.
    /// </para>
    /// </summary>
    public void SeedStartingValue(int value)
    {
        EnsureHydrated();   // decide fresh-vs-returning against the STORED value, never the baked one
        DefaultValue = value;

        if (!_isFreshInstall) return;
        _isFreshInstall = false;   // seed exactly once
        SetValue(value);           // SetValue persists it
    }
}

}