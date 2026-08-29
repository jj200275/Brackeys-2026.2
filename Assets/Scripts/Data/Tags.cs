using System;

namespace Sussy
{
    [Flags]
    public enum Tag
    {
        None         = 0,
        Edible       = 1 << 0,
        LiquidHolder = 1 << 1,
        ItemHolder   = 1 << 2,
        Wearable     = 1 << 3,
        Sittable     = 1 << 4,
        Hittable     = 1 << 5,
        Alive        = 1 << 6,
        Cleanable    = 1 << 7,
        Talkable     = 1 << 8,
        EarHoldable  = 1 << 9,
        Carryable    = 1 << 10,
    }

    public static class TagUtil
    {
        /// All tags in declaration order. Used by belief generation and content validation.
        public static readonly Tag[] All =
        {
            Tag.Edible, Tag.LiquidHolder, Tag.ItemHolder, Tag.Wearable, Tag.Sittable,
            Tag.Hittable, Tag.Alive, Tag.Cleanable, Tag.Talkable, Tag.EarHoldable, Tag.Carryable,
        };

        public static bool HasAll(this Tag have, Tag required) => (have & required) == required;
    }
}
