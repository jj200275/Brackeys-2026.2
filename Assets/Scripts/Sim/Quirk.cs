namespace Sussy
{
    /// Every innocent has exactly one, fixed for the run: one verb on one target, repeated.
    /// Quirk events must look structurally identical to anomalies, or there is no doubt.
    public sealed class Quirk
    {
        public VerbId      Verb;
        public WorldObject Target;

        public string Describe() => $"always {VerbTable.Get(Verb).PresentTense} the {Target.DisplayName}";
    }
}
