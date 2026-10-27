using System.Collections.Generic;
using UnityEngine;

namespace CarParking.Mechanic
{
    /// <summary>
    /// MODULE 44 — Barn Finds & Classic Car Restoration System
    /// Discover, tow, sandblast, weld, rebuild classic cars.
    /// </summary>
    public class BarnFindRestorationSystem : MonoBehaviour
    {
        public static BarnFindRestorationSystem Instance { get; private set; }

        [System.Serializable]
        public class BarnFind
        {
            public string id;
            public string name;
            public float rustAmount = 1f;
            public float bodyCondition = 0.1f;
            public float engineCondition = 0f;
            public bool isRestored;
            public bool isTowed;
        }

        public List<BarnFind> finds = new();
        public BarnFind activeRestore;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            SeedFinds();
        }

        private void SeedFinds()
        {
            string[] names = { "1967 Shelby", "1970 Cuda", "1985 AE86", "1992 NSX", "1973 Porsche", "1969 Camaro", "1981 DeLorean", "1971 Skyline" };
            for (int i = 0; i < names.Length; i++)
                finds.Add(new BarnFind { id = "barn_" + i, name = names[i] });
        }

        public void StartRestoration(string id)
        {
            activeRestore = finds.Find(f => f.id == id);
            if (activeRestore != null)
                Debug.Log($"[Barn] Restoring {activeRestore.name}");
        }

        public void Sandblast(float amount)
        {
            if (activeRestore == null) return;
            activeRestore.rustAmount = Mathf.Max(0f, activeRestore.rustAmount - amount);
        }

        public void WeldPanels(float amount)
        {
            if (activeRestore == null) return;
            activeRestore.bodyCondition = Mathf.Min(1f, activeRestore.bodyCondition + amount);
        }

        public void RebuildEngine(float amount)
        {
            if (activeRestore == null) return;
            activeRestore.engineCondition = Mathf.Min(1f, activeRestore.engineCondition + amount);
            if (activeRestore.rustAmount < 0.1f && activeRestore.bodyCondition > 0.9f && activeRestore.engineCondition > 0.9f)
            {
                activeRestore.isRestored = true;
                Debug.Log($"[Barn] {activeRestore.name} fully restored!");
            }
        }
    }
}
