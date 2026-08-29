using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sussy
{
    public enum RunState { Setup, Running, NightReview, Won, Lost }

    /// Orchestrates the run: picks impostors, generates beliefs, builds nightly schedules,
    /// gates anomalies on what the player is watching, and records residue.
    public sealed class NightDirector : MonoBehaviour
    {
        public static NightDirector Instance { get; private set; }

        [Header("Run")]
        public int   NightCount = 5;
        public float NightSeconds = 100f;
        public int   ImpostorCount = 1;
        public int   TasksPerNight = 6;

        [Header("Anomalies")]
        [Tooltip("Hard floor per impostor per night. Never zero: probabilistic evidence makes unwinnable runs.")]
        public int   MinAnomaliesPerNight = 1;
        [Range(0f, 1f)] public float AnomalyProbability = 0.6f;
        [Tooltip("Raised permanently by 1 for every surviving impostor after a wrong accusation.")]
        public int   TellFloorTier = 1;

        [Header("Scene")]
        public List<Npc> Npcs = new();
        public List<WorldObject> Objects = new();
        public List<CameraFeed> Feeds = new();
        public List<Transform> SocialPoints = new();

        public RunState State { get; private set; } = RunState.Setup;
        public int      Night { get; private set; }
        public float    NightTimeLeft { get; private set; }
        public CameraFeed ActiveFeed { get; private set; }
        public ResidueLog Residue { get; } = new();
        public int      Score { get; private set; }

        public event Action<string> OnLog;
        public event Action<Npc, ScheduledTask> OnAnomalySeen;
        public event Action OnNightEnded;

        readonly Dictionary<Npc, List<ScheduledTask>> _baselines = new();
        readonly Dictionary<Npc, int> _anomaliesTonight = new();

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        // ---------------------------------------------------------------- run setup

        public void StartRun()
        {
            var objectPool = Objects.Where(o => o != null && !o.IsNpc).ToList();
            var beliefPool = Objects.Where(o => o != null).ToList();   // NPCs are valid subjects too

            int id = 0;
            foreach (var o in Objects) o.Id = id++;

            foreach (var npc in Npcs)
            {
                npc.IsImpostor = false;
                npc.Belief = null;
                npc.Quirk = null;
                npc.IsEjected = false;
                _baselines[npc] = ScheduleBuilder.BuildBaseline(objectPool, TasksPerNight);
            }

            foreach (var npc in Npcs.OrderBy(_ => UnityEngine.Random.value).Take(ImpostorCount))
            {
                npc.IsImpostor = true;
                npc.Belief = BeliefGenerator.Generate(beliefPool);   // each impostor, independently
                Log($"[setup] {npc.PersonName} {npc.Belief.Describe()}");
            }

            foreach (var npc in Npcs.Where(n => !n.IsImpostor))
            {
                npc.Quirk = BeliefGenerator.GenerateQuirk(objectPool);
                if (npc.Quirk != null) Log($"[setup] {npc.PersonName} {npc.Quirk.Describe()}");
            }

            ActiveFeed = Feeds.FirstOrDefault();
            Night = 0;
            Score = 0;
            State = RunState.Running;
            StartCoroutine(RunNight());
        }

        // ---------------------------------------------------------------- night loop

        IEnumerator RunNight()
        {
            Night++;
            _anomaliesTonight.Clear();
            Log($"--- Night {Night} ---");

            var objectPool = Objects.Where(o => o != null && !o.IsNpc).ToList();

            foreach (var npc in Npcs.Where(n => !n.IsEjected))
            {
                var night = ScheduleBuilder.BuildNight(_baselines[npc], objectPool);

                if (npc.IsImpostor)
                    ScheduleBuilder.InjectAnomalies(night, npc.Belief, MinAnomaliesPerNight, AnomalyProbability);
                else
                    ScheduleBuilder.InjectQuirk(night, npc.Quirk);

                npc.Schedule.Clear();
                npc.Schedule.AddRange(night);
                _anomaliesTonight[npc] = 0;
                npc.BeginNight();
            }

            NightTimeLeft = NightSeconds;
            bool forcedTellChecked = false;

            while (NightTimeLeft > 0f)
            {
                NightTimeLeft -= Time.deltaTime;

                // Late in the night, make sure every impostor has actually slipped at least once.
                if (!forcedTellChecked && NightTimeLeft < NightSeconds * 0.15f)
                {
                    forcedTellChecked = true;
                    ForceMissingTells();
                }
                yield return null;
            }

            foreach (var npc in Npcs) npc.StopNight();

            // Closing shift tidies up, so residue does not accumulate across the whole run.
            foreach (var feed in Feeds) Residue.CleanRoom(feed.RoomId);

            State = RunState.NightReview;
            Log($"--- Night {Night} over ---");
            OnNightEnded?.Invoke();
        }

        void ForceMissingTells()
        {
            foreach (var npc in Npcs.Where(n => n.IsImpostor && !n.IsEjected))
            {
                if (_anomaliesTonight.GetValueOrDefault(npc) >= MinAnomaliesPerNight) continue;
                if (npc.Belief.Expressions.Count == 0) continue;

                var e = npc.Belief.Expressions[UnityEngine.Random.Range(0, npc.Belief.Expressions.Count)];
                npc.Schedule.Insert(0, new ScheduledTask
                {
                    Verb = e.Verb, Target = e.Target, IsAnomaly = true, Tier = e.Tier,
                });
                Log($"[floor] nudging {npc.PersonName} to slip before the night ends");
            }
        }

        public void ContinueToNextNight()
        {
            if (Night >= NightCount) { Finish(); return; }
            State = RunState.Running;
            StartCoroutine(RunNight());
        }

        void Finish()
        {
            bool allCaught = Npcs.Where(n => n.IsImpostor).All(n => n.IsEjected);
            State = allCaught ? RunState.Won : RunState.Lost;
            Log(State == RunState.Won ? "All impostors caught." : "The impostors got away.");
        }

        // ---------------------------------------------------------------- observation gating

        /// Called by an NPC as it begins an interaction. A watched impostor does the honest
        /// thing instead, which is what makes deliberately looking away a real tactic.
        public void NotifyInteractionStart(Npc npc, ScheduledTask task)
        {
            if (!task.IsAnomaly) return;

            if (task.Tier < TellFloorTier)
            {
                task.IsAnomaly = false;   // too blatant for a careful impostor
                return;
            }

            if (IsWatched(npc))
            {
                task.IsAnomaly = false;
                Log($"[gated] {npc.PersonName} thought better of it (on camera)");
            }
        }

        public void NotifyInteractionComplete(Npc npc, ScheduledTask task)
        {
            if (task.IsAnomaly)
            {
                _anomaliesTonight[npc] = _anomaliesTonight.GetValueOrDefault(npc) + 1;
                Residue.Add(RoomOf(npc), Night, task);

                if (IsWatched(npc))
                {
                    Log($"CAUGHT: {npc.PersonName} was {VerbTable.Get(task.Verb).PresentTense} the {task.Target.DisplayName}");
                    OnAnomalySeen?.Invoke(npc, task);
                }
            }
            else if (task.IsQuirk && IsWatched(npc))
            {
                // Deliberately indistinguishable from the line above.
                Log($"{npc.PersonName} was {VerbTable.Get(task.Verb).PresentTense} the {task.Target.DisplayName}");
                OnAnomalySeen?.Invoke(npc, task);
            }
        }

        /// An innocent walking into a room notices what was left there.
        public void NotifyLoitering(Npc npc)
        {
            if (npc.IsImpostor) return;
            var r = Residue.FindUncleaned(RoomOf(npc));
            if (r == null) return;
            r.Cleaned = true;
            Log($"{npc.PersonName}: \"{r.Description}\"");
        }

        public bool IsWatched(Npc npc) => ActiveFeed != null && ActiveFeed.Contains(npc.transform.position);

        public int RoomOf(Npc npc)
        {
            foreach (var f in Feeds) if (f.Contains(npc.transform.position)) return f.RoomId;
            return -1;
        }

        public void SwitchFeed(int index)
        {
            if (Feeds.Count == 0) return;
            ActiveFeed = Feeds[Mathf.Clamp(index, 0, Feeds.Count - 1)];
        }

        public Vector3 NearestSocialPoint(Vector3 from)
        {
            if (SocialPoints.Count == 0) return from;
            return SocialPoints
                .Where(t => t != null)
                .OrderBy(t => (t.position - from).sqrMagnitude)
                .First().position;
        }

        // ---------------------------------------------------------------- accusation

        public void Accuse(Npc npc)
        {
            if (npc == null || npc.IsEjected) return;

            npc.IsEjected = true;
            npc.StopNight();
            npc.gameObject.SetActive(false);

            if (npc.IsImpostor)
            {
                Score += 1000 + EarlyBonus();
                Log($"{npc.PersonName} was an impostor. {npc.Belief.Describe()}.");
                if (Npcs.Where(n => n.IsImpostor).All(n => n.IsEjected)) { Finish(); return; }
            }
            else
            {
                // Wrong accusation is never fatal, but the survivors get more careful,
                // and that cost compounds across the remaining nights.
                Score -= 400;
                TellFloorTier += 1;
                Log($"{npc.PersonName} was innocent. The others are being more careful now.");
            }
        }

        int EarlyBonus()
        {
            int[] bonus = { 500, 350, 200, 100, 0 };
            return bonus[Mathf.Clamp(Night - 1, 0, bonus.Length - 1)];
        }

        void Log(string msg)
        {
            Debug.Log(msg);
            OnLog?.Invoke(msg);
        }
    }
}
