using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Sussy
{
    public enum NpcState { Idle, Moving, Interacting, Loitering }

    /// An NPC is a WorldObject, so every verb that targets an object can target a person.
    /// Deliberately absent from its tags: Sittable, Edible, Wearable, LiquidHolder,
    /// ItemHolder, Carryable, EarHoldable. Each absence is an available anomaly.
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class Npc : WorldObject
    {
        public string    PersonName = "NPC";
        public string    Role = "Staff";
        public Transform Body;                 // sprite child, so we can face without rotating the agent

        public bool   IsImpostor;
        public Belief Belief;                  // null if innocent
        public Quirk  Quirk;                   // exactly one if innocent, null if impostor
        public bool   IsEjected;

        public readonly List<ScheduledTask> Schedule = new();
        public NpcState State { get; private set; } = NpcState.Idle;
        public ScheduledTask CurrentTask { get; private set; }

        NavMeshAgent _agent;
        Coroutine    _routine;

        public override bool IsNpc => true;
        public override Tag  Tags  => Tag.Alive | Tag.Talkable | Tag.Hittable | Tag.Cleanable;
        public override string DisplayName => PersonName;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            // NavMeshPlus bakes in XY; keep the agent from rotating the sprite into the ground plane.
            _agent.updateRotation = false;
            _agent.updateUpAxis   = false;
        }

        public void BeginNight()
        {
            StopNight();
            _routine = StartCoroutine(RunSchedule());
        }

        public void StopNight()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (CurrentTask != null) { CurrentTask.Target?.Release(this); CurrentTask = null; }
            State = NpcState.Idle;
            if (_agent != null && _agent.isOnNavMesh) _agent.ResetPath();
        }

        IEnumerator RunSchedule()
        {
            foreach (var task in Schedule)
            {
                if (IsEjected) yield break;
                yield return DoTask(task);
            }
            // Out of work before the night ends: mill about rather than freeze.
            while (true) yield return Loiter(Random.Range(2f, 5f));
        }

        IEnumerator DoTask(ScheduledTask task)
        {
            const int maxAttempts = 4;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (task.Target == null) yield break;

                if (!task.Target.TryClaim(this))
                {
                    // Occupied. Loiter and try again — never stand frozen.
                    yield return Loiter(Random.Range(2f, 4f));
                    continue;
                }

                CurrentTask = task;
                yield return MoveTo(task.Target.ApproachPoint);
                yield return Perform(task);

                task.Target.Release(this);
                CurrentTask = null;
                yield break;
            }
            // Gave up on a contested object; don't stall the rest of the schedule.
        }

        IEnumerator MoveTo(Vector3 worldPos)
        {
            State = NpcState.Moving;
            if (!_agent.isOnNavMesh) { State = NpcState.Idle; yield break; }

            _agent.SetDestination(worldPos);
            // Give the agent a frame to compute a path before we test for arrival.
            yield return null;

            float giveUpAt = Time.time + 30f;
            while (_agent.pathPending ||
                   _agent.remainingDistance > _agent.stoppingDistance + 0.05f)
            {
                FaceMovement();
                if (Time.time > giveUpAt) break;
                yield return null;
            }

            _agent.ResetPath();
            State = NpcState.Idle;
        }

        IEnumerator Perform(ScheduledTask task)
        {
            State = NpcState.Interacting;
            Face(task.Target.transform.position);

            // The director decides whether a watched impostor swaps to the honest version.
            NightDirector.Instance?.NotifyInteractionStart(this, task);

            float duration = VerbTable.Get(task.Verb).BaseDuration * Mathf.Max(0.1f, task.DurationMult);
            yield return new WaitForSeconds(duration);

            NightDirector.Instance?.NotifyInteractionComplete(this, task);
            State = NpcState.Idle;
        }

        IEnumerator Loiter(float seconds)
        {
            State = NpcState.Loitering;

            Vector3 spot = NightDirector.Instance != null
                ? NightDirector.Instance.NearestSocialPoint(transform.position)
                : transform.position;

            if (spot != transform.position) yield return MoveTo(spot);

            State = NpcState.Loitering;
            NightDirector.Instance?.NotifyLoitering(this);
            yield return new WaitForSeconds(seconds);
            State = NpcState.Idle;
        }

        void FaceMovement()
        {
            if (_agent.velocity.sqrMagnitude > 0.01f)
                Face(transform.position + (Vector3)(Vector2)_agent.velocity);
        }

        void Face(Vector3 worldPos)
        {
            if (Body == null) return;
            float dx = worldPos.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f)
            {
                var s = Body.localScale;
                s.x = Mathf.Abs(s.x) * (dx < 0 ? -1f : 1f);
                Body.localScale = s;
            }
        }
    }
}
