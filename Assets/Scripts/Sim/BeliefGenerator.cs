using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sussy
{
    public static class BeliefGenerator
    {
        const int MinExpressions = 3;
        const int MinDistinct    = 2;
        const int MaxAttempts    = 20;

        /// Generates one belief per impostor, independently. Rerolls until the belief has
        /// enough expressions to be learnable, so a run can't be unsolvable.
        public static Belief Generate(IReadOnlyList<WorldObject> world)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var b = TryOne(world);
                if (b != null && IsLearnable(b)) return b;
            }
            return Fallback(world);
        }

        static Belief TryOne(IReadOnlyList<WorldObject> world)
        {
            return Random.Range(0, 3) switch
            {
                0 => ObjectSubstitution(world),
                1 => TagHallucination(world),
                _ => VerbSubstitution(world),
            };
        }

        /// "The server rack is a kettle." Every verb valid on the kettle but not the rack,
        /// performed on the rack. Many verbs, one object.
        static Belief ObjectSubstitution(IReadOnlyList<WorldObject> world)
        {
            var subject  = Pick(world);
            var believed = Pick(world.Where(o => o != subject).ToList());
            if (subject == null || believed == null) return null;

            var b = new Belief
            {
                Type = BeliefType.ObjectSubstitution,
                Subject = subject,
                BelievedObject = believed,
            };

            foreach (var verb in InteractionMatrix.ValidVerbsOn(believed))
            {
                if (InteractionMatrix.ValidVerbsOn(subject).Contains(verb)) continue;
                b.Expressions.Add(new Expression { Verb = verb, Target = subject, Tier = 1 });
            }
            return b;
        }

        /// "The fuse box is edible." Every verb that requires the hallucinated tag,
        /// performed on an object that lacks it.
        static Belief TagHallucination(IReadOnlyList<WorldObject> world)
        {
            var subject = Pick(world);
            if (subject == null) return null;

            var missing = TagUtil.All.Where(t => !subject.Tags.HasAll(t)).ToList();
            if (missing.Count == 0) return null;

            var payload = missing[Random.Range(0, missing.Count)];

            var b = new Belief
            {
                Type = BeliefType.TagHallucination,
                Subject = subject,
                TagPayload = payload,
            };

            foreach (var verb in VerbTable.All)
            {
                var v = VerbTable.Get(verb);
                if (!v.RequiredTargetTags.HasAll(payload)) continue;
                if (InteractionMatrix.IsValid(verb, subject)) continue;
                b.Expressions.Add(new Expression { Verb = verb, Target = subject, Tier = 1 });
            }
            return b;
        }

        /// "Wiping is hitting." One wrong verb, spread across many objects.
        static Belief VerbSubstitution(IReadOnlyList<WorldObject> world)
        {
            var subjectVerb  = VerbTable.All[Random.Range(0, VerbTable.All.Length)];
            var believedVerb = VerbTable.All[Random.Range(0, VerbTable.All.Length)];
            if (subjectVerb == believedVerb) return null;

            var b = new Belief
            {
                Type = BeliefType.VerbSubstitution,
                SubjectVerb = subjectVerb,
                BelievedVerb = believedVerb,
            };

            // Targets where the real verb applies but the substituted one does not:
            // that mismatch is what the player sees.
            foreach (var o in world)
            {
                if (!InteractionMatrix.IsValid(subjectVerb, o)) continue;
                if (InteractionMatrix.IsValid(believedVerb, o)) continue;
                b.Expressions.Add(new Expression { Verb = believedVerb, Target = o, Tier = 1 });
            }
            return b;
        }

        /// Enough material to learn from: several expressions, and genuinely a line
        /// through the verb x object grid rather than a single point.
        static bool IsLearnable(Belief b)
        {
            if (b.Expressions.Count < MinExpressions) return false;

            if (b.Type == BeliefType.VerbSubstitution)
                return b.Expressions.Select(e => e.Target).Distinct().Count() >= MinDistinct;

            return b.Expressions.Select(e => e.Verb).Distinct().Count() >= MinDistinct;
        }

        /// Known-good belief when 20 rolls failed: hallucinate Edible on the object with
        /// the fewest tags, which always yields Eat plus whatever else it lacks.
        static Belief Fallback(IReadOnlyList<WorldObject> world)
        {
            var subject = world.OrderBy(o => CountTags(o.Tags)).First();
            var b = new Belief
            {
                Type = BeliefType.TagHallucination,
                Subject = subject,
                TagPayload = Tag.Edible,
            };
            foreach (var verb in VerbTable.All)
            {
                if (InteractionMatrix.IsValid(verb, subject)) continue;
                b.Expressions.Add(new Expression { Verb = verb, Target = subject, Tier = 1 });
            }
            return b;
        }

        static int CountTags(Tag t)
        {
            int n = 0;
            foreach (var x in TagUtil.All) if (t.HasAll(x)) n++;
            return n;
        }

        static WorldObject Pick(IReadOnlyList<WorldObject> xs) =>
            xs == null || xs.Count == 0 ? null : xs[Random.Range(0, xs.Count)];

        /// One quirk per innocent: a single (verb, target) pair they repeat all run.
        public static Quirk GenerateQuirk(IReadOnlyList<WorldObject> world)
        {
            for (int i = 0; i < MaxAttempts; i++)
            {
                var target = Pick(world);
                if (target == null) continue;

                // Quirks are odd but not impossible-looking: prefer an invalid pair so it
                // reads the same as an anomaly.
                var candidates = VerbTable.All.Where(v => !InteractionMatrix.IsValid(v, target)).ToList();
                if (candidates.Count == 0) continue;

                return new Quirk { Verb = candidates[Random.Range(0, candidates.Count)], Target = target };
            }
            return null;
        }
    }
}
