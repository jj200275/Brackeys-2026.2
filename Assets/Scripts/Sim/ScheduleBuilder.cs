using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sussy
{
    public static class ScheduleBuilder
    {
        /// The stable set of chores this NPC does. Generated once for the whole run:
        /// without a baseline the player never learns normal, and deviation is unreadable.
        public static List<ScheduledTask> BuildBaseline(IReadOnlyList<WorldObject> world, int count)
        {
            var tasks = new List<ScheduledTask>();
            var pool  = world.Where(o => !o.IsNpc).ToList();
            if (pool.Count == 0) return tasks;

            int guard = 0;
            while (tasks.Count < count && guard++ < 200)
            {
                var target = pool[Random.Range(0, pool.Count)];
                var verbs  = InteractionMatrix.ValidVerbsOn(target);
                if (verbs.Count == 0) continue;

                var verb = verbs[Random.Range(0, verbs.Count)];
                if (tasks.Any(t => t.Verb == verb && t.Target == target)) continue;

                tasks.Add(new ScheduledTask { Verb = verb, Target = target, Held = FindHeld(verb, world) });
            }
            return tasks;
        }

        /// Tonight's schedule: mostly the baseline, lightly varied. Keep recurrence high —
        /// more variety here reads as noise, not as life.
        public static List<ScheduledTask> BuildNight(
            IReadOnlyList<ScheduledTask> baseline, IReadOnlyList<WorldObject> world, float recurrence = 0.8f)
        {
            var night = new List<ScheduledTask>();

            foreach (var t in baseline)
            {
                if (Random.value <= recurrence) night.Add(t.Clone());
            }

            // Backfill so every night is a full shift.
            int wanted = baseline.Count;
            var extra  = BuildBaseline(world, Mathf.Max(0, wanted - night.Count));
            night.AddRange(extra);

            Shuffle(night);
            return night;
        }

        /// Replace or insert tasks so the impostor acts out their belief tonight.
        /// Guarantees at least `minimum` anomalies — probabilistic evidence produces
        /// unwinnable runs, and players read that as a bug.
        public static void InjectAnomalies(List<ScheduledTask> night, Belief belief, int minimum, float probability)
        {
            if (belief == null || belief.Expressions.Count == 0) return;

            var pool = new List<Expression>(belief.Expressions);
            Shuffle(pool);

            int placed = 0;

            if (belief.Type == BeliefType.VerbSubstitution)
            {
                // Substitute in place wherever tonight's schedule calls for the real verb.
                foreach (var task in night)
                {
                    if (task.Verb != belief.SubjectVerb) continue;
                    if (placed >= minimum && Random.value > probability) continue;
                    task.Verb = belief.BelievedVerb;
                    task.IsAnomaly = true;
                    task.Tier = 1;
                    placed++;
                }
            }

            // Top up from the expression pool until the floor is met.
            foreach (var e in pool)
            {
                if (placed >= minimum && Random.value > probability) continue;
                if (placed >= Mathf.Max(minimum, 3)) break;

                night.Insert(Random.Range(0, night.Count + 1), new ScheduledTask
                {
                    Verb = e.Verb,
                    Target = e.Target,
                    Held = null,
                    IsAnomaly = true,
                    Tier = e.Tier,
                    DurationMult = e.DurationMult,
                });
                placed++;
            }
        }

        /// Innocents repeat their one quirk. Same shape as an anomaly on purpose.
        public static void InjectQuirk(List<ScheduledTask> night, Quirk quirk, int times = 2)
        {
            if (quirk == null) return;
            for (int i = 0; i < times; i++)
            {
                night.Insert(Random.Range(0, night.Count + 1), new ScheduledTask
                {
                    Verb = quirk.Verb,
                    Target = quirk.Target,
                    IsQuirk = true,
                    Tier = 1,
                });
            }
        }

        static WorldObject FindHeld(VerbId verb, IReadOnlyList<WorldObject> world)
        {
            var v = VerbTable.Get(verb);
            if (!v.IsTransitive) return null;
            var options = world.Where(o => !o.IsNpc && o.Tags.HasAll(v.RequiredHeldTags)).ToList();
            return options.Count == 0 ? null : options[Random.Range(0, options.Count)];
        }

        static void Shuffle<T>(IList<T> xs)
        {
            for (int i = xs.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (xs[i], xs[j]) = (xs[j], xs[i]);
            }
        }
    }
}
