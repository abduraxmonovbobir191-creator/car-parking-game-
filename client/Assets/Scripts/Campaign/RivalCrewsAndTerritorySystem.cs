using System.Collections.Generic;
using UnityEngine;

namespace CarParking.Campaign
{
    /// <summary>
    /// MODULE 42 — Rival Street Racing Crews & Territory Control
    /// 8 factions, respect levels, turf, pink-slip duels.
    /// </summary>
    public class RivalCrewsAndTerritorySystem : MonoBehaviour
    {
        public static RivalCrewsAndTerritorySystem Instance { get; private set; }

        public enum Crew { ApexTuners, WanganPhantoms, MudCrawlers, HyperCartel, DriftRonin, DesertOutlaws, NightShift, ContinentalElite }

        [System.Serializable]
        public class CrewData
        {
            public Crew id;
            public string name;
            public float respect;
            public int territoryCells;
            public bool isHostile;
        }

        public List<CrewData> crews = new();
        public Crew playerCrew = Crew.ApexTuners;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            InitCrews();
        }

        private void InitCrews()
        {
            crews.Add(new CrewData { id = Crew.ApexTuners, name = "Apex Tuners", respect = 10f });
            crews.Add(new CrewData { id = Crew.WanganPhantoms, name = "Wangan Phantoms", respect = 0f, isHostile = true });
            crews.Add(new CrewData { id = Crew.MudCrawlers, name = "Mud Crawlers", respect = 0f });
            crews.Add(new CrewData { id = Crew.HyperCartel, name = "Hyper Cartel", respect = 0f, isHostile = true });
            crews.Add(new CrewData { id = Crew.DriftRonin, name = "Drift Ronin", respect = 5f });
            crews.Add(new CrewData { id = Crew.DesertOutlaws, name = "Desert Outlaws", respect = 0f });
            crews.Add(new CrewData { id = Crew.NightShift, name = "Night Shift", respect = 0f });
            crews.Add(new CrewData { id = Crew.ContinentalElite, name = "Continental Elite", respect = 0f, isHostile = true });
        }

        public void AddRespect(Crew c, float amount)
        {
            var d = crews.Find(x => x.id == c);
            if (d != null) d.respect = Mathf.Clamp(d.respect + amount, -100f, 100f);
        }

        public string GetCrewStatus()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in crews)
                sb.AppendLine($"{c.name}: respect {c.respect:F0} {(c.isHostile?"HOSTILE":"")}");
            return sb.ToString();
        }
    }
}
