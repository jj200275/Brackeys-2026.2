using System.Collections.Generic;

namespace Sussy
{
    public enum BeliefType { ObjectSubstitution, TagHallucination, VerbSubstitution }

    /// One wrong thing an impostor believes about the world, held consistently all run.
    /// Object beliefs make many verbs converge on ONE object.
    /// Verb beliefs make ONE wrong verb spread across many objects.
    /// An innocent quirk is a single (verb, target) point. That contrast is the deduction.
    public sealed class Belief
    {
        public BeliefType  Type;

        public WorldObject Subject;          // object beliefs: the one object all anomalies touch
        public WorldObject BelievedObject;   // ObjectSubstitution
        public Tag         TagPayload;       // TagHallucination

        public VerbId      SubjectVerb;      // VerbSubstitution: the verb being replaced
        public VerbId      BelievedVerb;     // VerbSubstitution: what they do instead

        public List<Expression> Expressions = new();

        public string Describe() => Type switch
        {
            BeliefType.ObjectSubstitution =>
                $"believes the {Subject.DisplayName} is {BelievedObject.Article} {BelievedObject.DisplayName}",
            BeliefType.TagHallucination =>
                $"believes the {Subject.DisplayName} is {TagPayload}",
            BeliefType.VerbSubstitution =>
                $"does {BelievedVerb} whenever they should {SubjectVerb}",
            _ => "?",
        };
    }

    public sealed class Expression
    {
        public VerbId      Verb;
        public WorldObject Target;
        public int         Tier = 1;
        public float       DurationMult = 1f;

        public override string ToString() => $"{Verb} -> {Target.DisplayName} (T{Tier})";
    }
}
