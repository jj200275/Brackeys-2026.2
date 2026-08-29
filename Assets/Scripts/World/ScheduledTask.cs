using System;

namespace Sussy
{
    [Serializable]
    public sealed class ScheduledTask
    {
        public VerbId      Verb;
        public WorldObject Target;
        public WorldObject Held;          // null for intransitive verbs
        public float       DurationMult = 1f;

        /// Set when this task came from an impostor's belief. Drives residue + scoring.
        public bool IsAnomaly;
        /// Set when this task came from an innocent's quirk. Deliberately looks identical.
        public bool IsQuirk;
        /// 1 = absurd, 2 = wrong actor, 3 = wrong sequence.
        public int  Tier = 1;

        public ScheduledTask Clone() => (ScheduledTask)MemberwiseClone();

        public override string ToString()
        {
            string t = Target != null ? Target.DisplayName : "?";
            string h = Held != null ? $" [{Held.DisplayName}]" : "";
            return $"{Verb} {t}{h}";
        }
    }
}
