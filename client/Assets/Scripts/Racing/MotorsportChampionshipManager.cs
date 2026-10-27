using System.Collections.Generic;
using UnityEngine;

namespace CarParking.Racing
{
    /// <summary>
    /// MODULE 45 — Motorsport Championship Sanctioning Body
    /// GT3, Time Attack, Formula Drift, Rally, Baja.
    /// </summary>
    public class MotorsportChampionshipManager : MonoBehaviour
    {
        public static MotorsportChampionshipManager Instance { get; private set; }

        public enum Discipline { GT3, TimeAttack, FormulaDrift, Rally, Baja }

        [System.Serializable]
        public class EventEntry
        {
            public Discipline discipline;
            public string name;
            public int position;
            public float bestTime;
            public int points;
        }

        public List<EventEntry> calendar = new();
        public Discipline activeDiscipline;
        public bool inSession;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildCalendar();
        }

        private void BuildCalendar()
        {
            calendar.Add(new EventEntry { discipline = Discipline.GT3, name = "Night Circuit" });
            calendar.Add(new EventEntry { discipline = Discipline.TimeAttack, name = "Airport Sprint" });
            calendar.Add(new EventEntry { discipline = Discipline.FormulaDrift, name = "Harbor Drift" });
            calendar.Add(new EventEntry { discipline = Discipline.Rally, name = "Mountain Stage" });
            calendar.Add(new EventEntry { discipline = Discipline.Baja, name = "Desert Endurance" });
        }

        public void StartSession(Discipline d)
        {
            activeDiscipline = d;
            inSession = true;
            Debug.Log($"[Motorsport] Started {d}");
        }

        public void FinishSession(float time, int pos)
        {
            inSession = false;
            var e = calendar.Find(x => x.discipline == activeDiscipline);
            if (e != null)
            {
                e.bestTime = e.bestTime <= 0f ? time : Mathf.Min(e.bestTime, time);
                e.position = pos;
                e.points += Mathf.Max(0, 25 - pos * 3);
            }
            Debug.Log($"[Motorsport] Finished {activeDiscipline} pos {pos} time {time:F2}");
        }

        public string GetStandings()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var e in calendar)
                sb.AppendLine($"{e.discipline}: {e.points} pts best {e.bestTime:F1}s");
            return sb.ToString();
        }
    }
}
