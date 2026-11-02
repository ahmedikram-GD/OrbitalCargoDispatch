using OrbitalCargo.Config;
using OrbitalCargo.Crates;

namespace OrbitalCargo.Radio
{
    /// <summary>What a guided crate needs from the player right now.</summary>
    public enum LessonStage
    {
        Fetch,   // on the belt or lying somewhere — go pick it up
        Deliver, // in the hands — take it to its target
        Wait,    // scanning or mid-throw — nothing to do
        Done,    // in a bay, launched or chuted
    }

    /// <summary>Where a guided crate should go next.</summary>
    public enum LessonTarget
    {
        Bay,
        Chute,
        Scanner,
    }

    /// <summary>
    /// Pure rules for one guided crate delivery, kept out of the MonoBehaviours so the onboarding's
    /// decisions are unit-tested.
    /// </summary>
    public static class TutorialRules
    {
        public static LessonStage StageOf(CrateState state)
        {
            switch (state)
            {
                case CrateState.OnConveyor:
                case CrateState.Pickable:
                    return LessonStage.Fetch;
                case CrateState.Carried:
                    return LessonStage.Deliver;
                case CrateState.Scanning:
                case CrateState.Thrown:
                    return LessonStage.Wait;
                default:
                    return LessonStage.Done;
            }
        }

        /// <summary>
        /// A suspicious crate goes to the scanner first; once scanned, contraband goes down the chute and
        /// a clean one to its own bay. Junk's destination is the chute.
        /// </summary>
        public static LessonTarget TargetOf(BayId destination, bool suspicious, bool scanned, bool bad)
        {
            if (suspicious && !scanned) return LessonTarget.Scanner;
            if (scanned && bad) return LessonTarget.Chute;
            return destination == BayId.Chute ? LessonTarget.Chute : LessonTarget.Bay;
        }
    }
}
