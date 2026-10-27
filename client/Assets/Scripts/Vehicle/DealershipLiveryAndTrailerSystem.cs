using System.Collections.Generic;
using UnityEngine;
using CarParking.Core;

namespace CarParking.Vehicle
{
    public enum VehicleClass { Hypercar, JDMDrift, Offroad4x4, LuxurySedan, Muscle }

    /// <summary>
    /// Car Dealership showroom, bodykit/livery editor and physical car trailer towing.
    /// </summary>
    public class DealershipLiveryAndTrailerSystem : MonoBehaviour
    {
        public static DealershipLiveryAndTrailerSystem Instance { get; private set; }

        [System.Serializable]
        public class DealershipVehicle
        {
            public string name;
            public VehicleClass vehicleClass;
            public GameObject prefab;
            public float price;
            public int horsepower;
            public string engineDesc;
        }

        [Header("Dealership")]
        public DealershipVehicle[] catalog;
        public Transform[] showroomSpots;

        [Header("Livery / Bodykit")]
        public Material[] liveryMaterials;
        public GameObject[] bodykitPrefabs; // splitters, widebody, wings, roof racks

        [Header("Trailer")]
        public GameObject trailerPrefab;
        public Transform hitchPoint;
        public float hitchDistance = 3.5f;

        private GameObject _activeTrailer;
        private bool _trailerAttached;
        private ConfigurableJoint _hitchJoint;
        private List<GameObject> _loadedVehicles = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public bool PurchaseVehicle(int catalogIndex)
        {
            if (catalog == null || catalogIndex < 0 || catalogIndex >= catalog.Length)
                return false;

            var item = catalog[catalogIndex];
            if (WorldServicesAndEconomySystem.Instance == null)
                return false;

            if (WorldServicesAndEconomySystem.Instance.playerCash < item.price)
            {
                Debug.Log("[Dealership] Not enough cash");
                return false;
            }

            WorldServicesAndEconomySystem.Instance.playerCash -= item.price;

            if (item.prefab != null)
            {
                Vector3 spawnPos = transform.position + transform.forward * 8f;
                Instantiate(item.prefab, spawnPos, Quaternion.identity);
            }

            Debug.Log($"[Dealership] Purchased {item.name} for ${item.price:N0}");

            if (GarageAndWorldSaveManager.Instance != null)
                GarageAndWorldSaveManager.Instance.SaveAll();

            return true;
        }

        public void ApplyLivery(GameObject vehicle, int materialIndex, Color primary, Color secondary)
        {
            if (vehicle == null || liveryMaterials == null || materialIndex < 0 || materialIndex >= liveryMaterials.Length)
                return;

            var renderers = vehicle.GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r.material != null && r.material.name.ToLower().Contains("body"))
                {
                    r.material = liveryMaterials[materialIndex];
                    if (r.material.HasProperty("_BaseColor"))
                        r.material.SetColor("_BaseColor", primary);
                    if (r.material.HasProperty("_SecondaryColor"))
                        r.material.SetColor("_SecondaryColor", secondary);
                }
            }
        }

        public void AttachBodykit(GameObject vehicle, int kitIndex)
        {
            if (vehicle == null || bodykitPrefabs == null || kitIndex < 0 || kitIndex >= bodykitPrefabs.Length)
                return;

            var kit = Instantiate(bodykitPrefabs[kitIndex], vehicle.transform);
            kit.transform.localPosition = Vector3.zero;
            kit.transform.localRotation = Quaternion.identity;
        }

        public void SpawnAndHitchTrailer()
        {
            if (trailerPrefab == null) return;

            var host = FindFirstObjectByType<RealisticVehicleController>();
            if (host == null) return;

            Vector3 pos = host.transform.position - host.transform.forward * hitchDistance;
            pos.y = host.transform.position.y;

            _activeTrailer = Instantiate(trailerPrefab, pos, host.transform.rotation);
            _trailerAttached = true;

            var trailerRb = _activeTrailer.GetComponent<Rigidbody>();
            if (trailerRb == null)
                trailerRb = _activeTrailer.AddComponent<Rigidbody>();

            trailerRb.mass = 900f;

            _hitchJoint = _activeTrailer.AddComponent<ConfigurableJoint>();
            _hitchJoint.connectedBody = host.GetComponent<Rigidbody>();
            _hitchJoint.anchor = new Vector3(0f, 0.5f, 2.2f);
            _hitchJoint.xMotion = ConfigurableJointMotion.Limited;
            _hitchJoint.yMotion = ConfigurableJointMotion.Limited;
            _hitchJoint.zMotion = ConfigurableJointMotion.Limited;
            _hitchJoint.angularXMotion = ConfigurableJointMotion.Limited;
            _hitchJoint.angularYMotion = ConfigurableJointMotion.Limited;
            _hitchJoint.angularZMotion = ConfigurableJointMotion.Limited;

            SoftJointLimit limit = new SoftJointLimit { limit = 0.6f };
            _hitchJoint.linearLimit = limit;

            Debug.Log("[Trailer] Hitch connected");
        }

        public void UnhitchTrailer()
        {
            if (_hitchJoint != null)
                Destroy(_hitchJoint);
            _trailerAttached = false;
            Debug.Log("[Trailer] Unhitched");
        }

        public bool LoadVehicleOntoTrailer(GameObject vehicle)
        {
            if (!_trailerAttached || _activeTrailer == null || vehicle == null)
                return false;

            var rb = vehicle.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }

            vehicle.transform.SetParent(_activeTrailer.transform);
            vehicle.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            vehicle.transform.localRotation = Quaternion.identity;

            _loadedVehicles.Add(vehicle);
            Debug.Log("[Trailer] Vehicle locked on trailer");
            return true;
        }

        public void UnloadVehicle(GameObject vehicle)
        {
            if (vehicle == null) return;

            vehicle.transform.SetParent(null);
            var rb = vehicle.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;

            _loadedVehicles.Remove(vehicle);
        }

        public bool IsTrailerAttached => _trailerAttached;
    }
}
