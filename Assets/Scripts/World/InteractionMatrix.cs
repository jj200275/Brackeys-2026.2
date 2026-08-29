using System.Collections.Generic;

namespace Sussy
{
    /// Validity is derived from tags. Overrides are authored per (verb, prototype) where the
    /// derivation gets it wrong. Deriving first means you only author the exceptions.
    public static class InteractionMatrix
    {
        public struct Override
        {
            public VerbId Verb;
            public ObjectPrototype Target;
            public bool Valid;
        }

        static readonly Dictionary<(VerbId, ObjectPrototype), bool> _overrides = new();

        public static void LoadOverrides(IEnumerable<Override> overrides)
        {
            _overrides.Clear();
            if (overrides == null) return;
            foreach (var o in overrides)
                if (o.Target != null) _overrides[(o.Verb, o.Target)] = o.Valid;
        }

        /// True if `verb` makes sense on `target` in the real world.
        public static bool IsValid(VerbId verb, WorldObject target, WorldObject held = null)
        {
            if (target == null) return false;

            if (target.Prototype != null && _overrides.TryGetValue((verb, target.Prototype), out bool forced))
                return forced;

            var v = VerbTable.Get(verb);
            if (!target.Tags.HasAll(v.RequiredTargetTags)) return false;

            if (v.IsTransitive)
            {
                if (held == null) return false;
                if (!held.Tags.HasAll(v.RequiredHeldTags)) return false;
            }
            return true;
        }

        /// Every verb that is valid on this object (ignoring the held-item requirement,
        /// which the schedule satisfies separately).
        public static List<VerbId> ValidVerbsOn(WorldObject target)
        {
            var result = new List<VerbId>();
            foreach (var id in VerbTable.All)
            {
                if (target.Prototype != null && _overrides.TryGetValue((id, target.Prototype), out bool forced))
                {
                    if (forced) result.Add(id);
                    continue;
                }
                if (target.Tags.HasAll(VerbTable.Get(id).RequiredTargetTags)) result.Add(id);
            }
            return result;
        }
    }
}
