using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarParking.Campaign
{
    /// <summary>
    /// MODULE 41 — Full 50-Mission Story Campaign Engine
    /// 5 Chapters, narrative state machine, objectives, rewards.
    /// </summary>
    public class StoryCampaignEngine : MonoBehaviour
    {
        public static StoryCampaignEngine Instance { get; private set; }

        public enum Chapter { StreetRookie = 1, TougeDrift = 2, OffRoadRaid = 3, CorporateLogistics = 4, GrandFinale = 5 }
        public enum MissionState { Locked, Available, Active, Completed, Failed }

        [Serializable]
        public class Mission
        {
            public int id;
            public Chapter chapter;
            public string title;
            public string description;
            public MissionState state;
            public float rewardCash;
            public string[] objectives;
            public int currentObjective;
        }

        public List<Mission> missions = new();
        public int currentMissionId = -1;
        public Chapter currentChapter = Chapter.StreetRookie;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            GenerateMissions();
        }

        private void GenerateMissions()
        {
            for (int i = 1; i <= 50; i++)
            {
                Chapter ch = (Chapter)Mathf.Clamp((i-1)/10 + 1, 1, 5);
                missions.Add(new Mission
                {
                    id = i,
                    chapter = ch,
                    title = $"Mission {i}: {GetMissionTitle(i, ch)}",
                    description = GetMissionDescription(i, ch),
                    state = i == 1 ? MissionState.Available : MissionState.Locked,
                    rewardCash = 500f + i * 150f,
                    objectives = GetObjectives(i),
                    currentObjective = 0
                });
            }
        }

        private string GetMissionTitle(int id, Chapter ch)
        {
            return ch switch
            {
                Chapter.StreetRookie => id switch { 1 => "First Parking", 2 => "Night Delivery", 5 => "Underground Meet", _ => "Street Job" },
                Chapter.TougeDrift => "Touge Run",
                Chapter.OffRoadRaid => "Desert Stage",
                Chapter.CorporateLogistics => "VIP Escort",
                Chapter.GrandFinale => "Continental Finale",
                _ => "Mission"
            };
        }

        private string GetMissionDescription(int id, Chapter ch) => $"Complete objectives for mission {id} in {ch}.";

        private string[] GetObjectives(int id)
        {
            return new[] { "Reach location", "Complete challenge", "Return to base" };
        }

        public void StartMission(int id)
        {
            var m = missions.Find(x => x.id == id);
            if (m == null || m.state != MissionState.Available) return;
            m.state = MissionState.Active;
            currentMissionId = id;
            Debug.Log($"[Campaign] Started {m.title}");
        }

        public void CompleteCurrentObjective()
        {
            var m = missions.Find(x => x.id == currentMissionId);
            if (m == null) return;
            m.currentObjective++;
            if (m.currentObjective >= m.objectives.Length)
            {
                m.state = MissionState.Completed;
                if (WorldServicesAndEconomySystem.Instance != null)
                    WorldServicesAndEconomySystem.Instance.playerCash += m.rewardCash;
                UnlockNext(m.id);
                Debug.Log($"[Campaign] Completed {m.title} +${m.rewardCash}");
            }
        }

        private void UnlockNext(int completedId)
        {
            var next = missions.Find(x => x.id == completedId + 1);
            if (next != null) next.state = MissionState.Available;
        }

        public string GetProgressSummary()
        {
            int done = missions.FindAll(m => m.state == MissionState.Completed).Count;
            return $"Campaign: {done}/50 missions. Chapter {currentChapter}";
        }
    }
}
