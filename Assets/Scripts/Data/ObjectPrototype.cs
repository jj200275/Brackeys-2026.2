using UnityEngine;

namespace Sussy
{
    [CreateAssetMenu(menuName = "Sussy/Object Prototype", fileName = "Obj_")]
    public sealed class ObjectPrototype : ScriptableObject
    {
        public string DisplayName = "Thing";
        public Tag    Tags = Tag.Cleanable;
        public Sprite Sprite;
        public Color  PlaceholderColor = Color.white;
        public bool   BlocksMovement = true;

        /// Shown when an NPC finds residue here, e.g. "the kettle is full of paperclips".
        [TextArea] public string ResidueLine = "";

        public string Article => DisplayName.Length > 0 && "aeiouAEIOU".IndexOf(DisplayName[0]) >= 0 ? "an" : "a";
    }
}
