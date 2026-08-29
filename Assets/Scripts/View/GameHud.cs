using System.Collections.Generic;
using UnityEngine;

namespace Sussy
{
    /// Deliberately plain IMGUI. It exists so the simulation is playable and legible now;
    /// replace it with real UI once the art passes land.
    public sealed class GameHud : MonoBehaviour
    {
        public NightDirector Director;

        readonly List<string> _log = new();
        Vector2 _logScroll;
        bool    _accusing;

        void Start()
        {
            if (Director == null) Director = NightDirector.Instance;
            if (Director == null) return;

            Director.OnLog += Append;
            Director.StartRun();
        }

        void OnDestroy()
        {
            if (Director != null) Director.OnLog -= Append;
        }

        void Append(string line)
        {
            _log.Add(line);
            if (_log.Count > 200) _log.RemoveAt(0);
            _logScroll.y = float.MaxValue;
        }

        void Update()
        {
            if (Director == null) return;
            for (int i = 0; i < Director.Feeds.Count && i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Director.SwitchFeed(i);
        }

        void OnGUI()
        {
            if (Director == null) return;

            // ---- status bar
            GUILayout.BeginArea(new Rect(10, 10, 460, 70), GUI.skin.box);
            GUILayout.Label($"NIGHT {Director.Night} / {Director.NightCount}    " +
                            $"{Mathf.CeilToInt(Mathf.Max(0, Director.NightTimeLeft))}s    " +
                            $"SCORE {Director.Score}");

            GUILayout.BeginHorizontal();
            for (int i = 0; i < Director.Feeds.Count; i++)
            {
                var feed = Director.Feeds[i];
                bool active = Director.ActiveFeed == feed;
                if (GUILayout.Button(active ? $"[{feed.FeedName}]" : feed.FeedName))
                    Director.SwitchFeed(i);
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // ---- log, outside the feed so what the camera shows stays separate
            // from what the player knows.
            float w = 380f;
            GUILayout.BeginArea(new Rect(Screen.width - w - 10, 10, w, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("NOTEBOOK");
            _logScroll = GUILayout.BeginScrollView(_logScroll);
            foreach (var line in _log) GUILayout.Label(line);
            GUILayout.EndScrollView();

            if (Director.State == RunState.NightReview)
            {
                GUILayout.Space(6);
                if (!_accusing)
                {
                    if (GUILayout.Button("Accuse someone")) _accusing = true;
                    if (GUILayout.Button("Pass to next night")) Director.ContinueToNextNight();
                }
                else
                {
                    GUILayout.Label("Who is it?");
                    foreach (var npc in Director.Npcs)
                    {
                        if (npc.IsEjected) continue;
                        if (GUILayout.Button($"{npc.PersonName} ({npc.Role})"))
                        {
                            Director.Accuse(npc);
                            _accusing = false;
                            if (Director.State == RunState.NightReview) Director.ContinueToNextNight();
                        }
                    }
                    if (GUILayout.Button("Never mind")) _accusing = false;
                }
            }

            if (Director.State is RunState.Won or RunState.Lost)
                GUILayout.Label(Director.State == RunState.Won ? "YOU GOT THEM." : "THEY GOT AWAY.");

            GUILayout.EndArea();

            // ---- name tags on whoever is on camera
            foreach (var npc in Director.Npcs)
            {
                if (npc.IsEjected || !npc.gameObject.activeInHierarchy) continue;
                if (!Director.IsWatched(npc)) continue;

                var sp = Camera.main.WorldToScreenPoint(npc.transform.position + Vector3.up * 0.6f);
                if (sp.z < 0) continue;

                string label = npc.PersonName;
                if (npc.State == NpcState.Interacting && npc.CurrentTask != null)
                    label += $"\n{npc.CurrentTask}";

                GUI.Label(new Rect(sp.x - 60, Screen.height - sp.y - 30, 120, 40), label);
            }
        }
    }
}
