using System.Collections.Generic;
using System.Linq;

namespace Sussy
{
    /// A trace left behind by an anomaly. Never records who did it — that is the whole point.
    /// Residue is what makes deliberately looking away playable: an unwatched room still
    /// yields knowledge, just later and without a face attached.
    public sealed class Residue
    {
        public int    RoomId;
        public int    Night;
        public string Description;
        public bool   Cleaned;
    }

    public sealed class ResidueLog
    {
        readonly List<Residue> _entries = new();

        public IReadOnlyList<Residue> Entries => _entries;

        public void Add(int roomId, int night, ScheduledTask task)
        {
            string line = !string.IsNullOrEmpty(task.Target?.Prototype?.ResidueLine)
                ? task.Target.Prototype.ResidueLine
                : $"someone's been {VerbTable.Get(task.Verb).PresentTense} the {task.Target.DisplayName}";

            _entries.Add(new Residue { RoomId = roomId, Night = night, Description = line });
        }

        public Residue FindUncleaned(int roomId) =>
            _entries.FirstOrDefault(r => r.RoomId == roomId && !r.Cleaned);

        public void CleanRoom(int roomId)
        {
            foreach (var r in _entries.Where(r => r.RoomId == roomId)) r.Cleaned = true;
        }

        public IEnumerable<Residue> ForNight(int night) => _entries.Where(r => r.Night == night);
    }
}
