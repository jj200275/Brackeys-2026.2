using System.Collections.Generic;

namespace Sussy
{
    public enum VerbId { Hit, Wipe, PourInto, Eat, InsertInto, Carry, SitOn, HoldToEar, Wear, TalkTo }

    public enum Arity { Intransitive, Transitive }

    public sealed class Verb
    {
        public VerbId Id;
        public Arity  Arity;
        public Tag    RequiredTargetTags;
        public Tag    RequiredHeldTags;
        public float  BaseDuration;      // seconds
        public string PresentTense;      // for barks: "pouring coffee into"

        public bool IsTransitive => Arity == Arity.Transitive;
    }

    /// The verb table from the spec, as plain data. Durations converted from ticks at 10/s.
    public static class VerbTable
    {
        static readonly Dictionary<VerbId, Verb> _byId = new();

        static VerbTable()
        {
            Add(VerbId.Hit,        Arity.Transitive,   Tag.Hittable,     Tag.Carryable,     1.5f, "hitting");
            Add(VerbId.Wipe,       Arity.Transitive,   Tag.Cleanable,    Tag.Carryable,     4.0f, "wiping");
            Add(VerbId.PourInto,   Arity.Transitive,   Tag.LiquidHolder, Tag.LiquidHolder,  3.0f, "pouring into");
            Add(VerbId.InsertInto, Arity.Transitive,   Tag.ItemHolder,   Tag.Carryable,     2.5f, "putting something into");
            Add(VerbId.Eat,        Arity.Intransitive, Tag.Edible,       Tag.None,          5.0f, "eating");
            Add(VerbId.Carry,      Arity.Intransitive, Tag.Carryable,    Tag.None,          1.0f, "picking up");
            Add(VerbId.SitOn,      Arity.Intransitive, Tag.Sittable,     Tag.None,          6.0f, "sitting on");
            Add(VerbId.HoldToEar,  Arity.Intransitive, Tag.EarHoldable,  Tag.None,          3.5f, "holding to their ear");
            Add(VerbId.Wear,       Arity.Intransitive, Tag.Wearable,     Tag.None,          3.0f, "wearing");
            Add(VerbId.TalkTo,     Arity.Intransitive, Tag.Talkable,     Tag.None,          4.5f, "talking to");
        }

        static void Add(VerbId id, Arity arity, Tag target, Tag held, float dur, string present)
        {
            _byId[id] = new Verb
            {
                Id = id, Arity = arity, RequiredTargetTags = target,
                RequiredHeldTags = held, BaseDuration = dur, PresentTense = present,
            };
        }

        public static readonly VerbId[] All =
        {
            VerbId.Hit, VerbId.Wipe, VerbId.PourInto, VerbId.Eat, VerbId.InsertInto,
            VerbId.Carry, VerbId.SitOn, VerbId.HoldToEar, VerbId.Wear, VerbId.TalkTo,
        };

        public static Verb Get(VerbId id) => _byId[id];
    }
}
