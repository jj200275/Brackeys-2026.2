using UnityEngine;

namespace Sussy
{
    /// Anything a verb can target. NPCs inherit from this, which is why SitOn(npc) works.
    public class WorldObject : MonoBehaviour
    {
        public ObjectPrototype Prototype;
        public int  Id = -1;
        public int  RoomId = -1;

        [HideInInspector] public bool IsBroken;

        Npc _user;   // who is currently interacting with this

        public virtual bool IsNpc => false;
        public virtual Tag  Tags  => Prototype != null ? Prototype.Tags : Tag.None;
        public virtual string DisplayName => Prototype != null ? Prototype.DisplayName : name;

        /// "a mug" vs "an apple", for bark and belief text.
        public string Article =>
            DisplayName.Length > 0 && "aeiouAEIOU".IndexOf(DisplayName[0]) >= 0 ? "an" : "a";

        public bool IsFree => _user == null;

        public bool TryClaim(Npc npc)
        {
            if (_user != null && _user != npc) return false;
            _user = npc;
            return true;
        }

        public void Release(Npc npc)
        {
            if (_user == npc) _user = null;
        }

        /// Where an NPC should stand to use this. Just outside the object's own cell.
        public Vector3 ApproachPoint => transform.position;
    }
}
