using System.Collections.Generic;
using UnityEngine;
using CarParking.Core;

namespace CarParking.World
{
    /// <summary>
    /// Hidden stunt jumps, secret garages, collectible mystery items
    /// across the map with rewards.
    /// </summary>
    public class WorldExplorationEasterEggs : MonoBehaviour
    {
        public static WorldExplorationEasterEggs Instance { get; private set; }

        [System.Serializable]
        public class EasterEgg
        {
            public string id;
            public string displayName;
            public Vector3 position;
            public float triggerRadius = 8f;
            public float reward;
            public bool collected;
            public EggType type;
        }

        public enum EggType { StuntJump, SecretGarage, MysteryItem, Viewpoint }

        [Header("Eggs")]
        public int stuntJumpCount = 12;
        public int secretGarageCount = 5;
        public int mysteryItemCount = 25;

        private List<EasterEgg> _eggs = new();
        private Transform _player;
        private int _collectedCount;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            GenerateEggs();
            var v = FindFirstObjectByType<CarParking.Vehicle.RealisticVehicleController>();
            if (v != null) _player = v.transform;
        }

        private void Update()
        {
            if (_player == null)
            {
                var v = FindFirstObjectByType<CarParking.Vehicle.RealisticVehicleController>();
                if (v != null) _player = v.transform;
                return;
            }

            foreach (var egg in _eggs)
            {
                if (egg.collected) continue;
                float d = Vector3.Distance(_player.position, egg.position);
                if (d < egg.triggerRadius)
                    Collect(egg);
            }
        }

        private void GenerateEggs()
        {
            _eggs.Clear();

            for (int i = 0; i < stuntJumpCount; i++)
            {
                _eggs.Add(new EasterEgg
                {
                    id = "stunt_" + i,
                    displayName = "Stunt Jump #" + (i + 1),
                    position = RandomWorldPoint(),
                    triggerRadius = 12f,
                    reward = 500f + i * 50f,
                    type = EggType.StuntJump
                });
            }

            for (int i = 0; i < secretGarageCount; i++)
            {
                _eggs.Add(new EasterEgg
                {
                    id = "garage_" + i,
                    displayName = "Secret Garage #" + (i + 1),
                    position = RandomWorldPoint(),
                    triggerRadius = 10f,
                    reward = 2000f,
                    type = EggType.SecretGarage
                });
            }

            for (int i = 0; i < mysteryItemCount; i++)
            {
                _eggs.Add(new EasterEgg
                {
                    id = "item_" + i,
                    displayName = "Mystery Item #" + (i + 1),
                    position = RandomWorldPoint(),
                    triggerRadius = 5f,
                    reward = 150f + Random.Range(0f, 350f),
                    type = EggType.MysteryItem
                });
            }

            Debug.Log($"[EasterEggs] Generated {_eggs.Count} exploration targets");
        }

        private Vector3 RandomWorldPoint()
        {
            return new Vector3(Random.Range(-800f, 800f), 1f, Random.Range(-800f, 800f));
        }

        private void Collect(EasterEgg egg)
        {
            egg.collected = true;
            _collectedCount++;

            if (WorldServicesAndEconomySystem.Instance != null)
                WorldServicesAndEconomySystem.Instance.playerCash += egg.reward;

            Debug.Log($"[EasterEgg] {egg.displayName} found! +${egg.reward:N0} ({_collectedCount}/{_eggs.Count})");

            // Special handling
            if (egg.type == EggType.SecretGarage)
                Debug.Log("[EasterEgg] Secret garage unlocked — free repair point");

            if (egg.type == EggType.StuntJump)
                Debug.Log("[EasterEgg] Stunt jump completed!");
        }

        public int CollectedCount => _collectedCount;
        public int TotalCount => _eggs.Count;
    }
}
