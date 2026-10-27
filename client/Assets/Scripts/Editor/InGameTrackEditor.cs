using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CarParking.Multiplayer;

namespace CarParking.TrackEditor
{
    /// <summary>
    /// Runtime in-game track & map editor.
    /// Place roads, ramps, barriers, checkpoints, parking bays; save/load layouts.
    /// </summary>
    public class InGameTrackEditor : MonoBehaviour
    {
        public static InGameTrackEditor Instance { get; private set; }

        public enum PlaceableType { Road, Ramp, Barrier, Checkpoint, ParkingBay }

        [System.Serializable]
        public class PlacedObjectData
        {
            public PlaceableType type;
            public Vector3 position;
            public Vector3 euler;
            public Vector3 scale = Vector3.one;
        }

        [System.Serializable]
        public class TrackLayout
        {
            public string name;
            public List<PlacedObjectData> objects = new();
        }

        [Header("Prefabs")]
        public GameObject roadPrefab;
        public GameObject rampPrefab;
        public GameObject barrierPrefab;
        public GameObject checkpointPrefab;
        public GameObject parkingBayPrefab;

        [Header("Settings")]
        public KeyCode toggleEditorKey = KeyCode.F6;
        public KeyCode placeKey = KeyCode.Mouse0;
        public KeyCode deleteKey = KeyCode.Delete;
        public KeyCode saveKey = KeyCode.F10;
        public float placeDistance = 40f;
        public PlaceableType currentType = PlaceableType.Road;

        private bool _editorActive;
        private List<GameObject> _spawned = new();
        private TrackLayout _currentLayout = new TrackLayout { name = "CustomTrack" };
        private Camera _cam;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            _cam = Camera.main;
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleEditorKey))
            {
                _editorActive = !_editorActive;
                Debug.Log(_editorActive ? "[TrackEditor] ON" : "[TrackEditor] OFF");
            }

            if (!_editorActive) return;

            if (Input.GetKeyDown(KeyCode.Alpha1)) currentType = PlaceableType.Road;
            if (Input.GetKeyDown(KeyCode.Alpha2)) currentType = PlaceableType.Ramp;
            if (Input.GetKeyDown(KeyCode.Alpha3)) currentType = PlaceableType.Barrier;
            if (Input.GetKeyDown(KeyCode.Alpha4)) currentType = PlaceableType.Checkpoint;
            if (Input.GetKeyDown(KeyCode.Alpha5)) currentType = PlaceableType.ParkingBay;

            if (Input.GetKeyDown(placeKey))
                TryPlace();

            if (Input.GetKeyDown(deleteKey))
                TryDelete();

            if (Input.GetKeyDown(saveKey))
                SaveLayout(_currentLayout.name);
        }

        private void TryPlace()
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, placeDistance))
                return;

            GameObject prefab = GetPrefab(currentType);
            if (prefab == null)
            {
                // Fallback empty marker
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = currentType.ToString();
                go.transform.position = hit.point + Vector3.up * 0.1f;
                go.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up));
                _spawned.Add(go);
                Record(go, currentType);
                return;
            }

            GameObject instance = Instantiate(prefab, hit.point, Quaternion.LookRotation(Vector3.ProjectOnPlane(_cam.transform.forward, Vector3.up)));
            _spawned.Add(instance);
            Record(instance, currentType);
        }

        private void TryDelete()
        {
            if (_cam == null) return;
            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, placeDistance))
            {
                if (_spawned.Contains(hit.collider.gameObject))
                {
                    _spawned.Remove(hit.collider.gameObject);
                    Destroy(hit.collider.gameObject);
                }
            }
        }

        private GameObject GetPrefab(PlaceableType type)
        {
            return type switch
            {
                PlaceableType.Road => roadPrefab,
                PlaceableType.Ramp => rampPrefab,
                PlaceableType.Barrier => barrierPrefab,
                PlaceableType.Checkpoint => checkpointPrefab,
                PlaceableType.ParkingBay => parkingBayPrefab,
                _ => null
            };
        }

        private void Record(GameObject go, PlaceableType type)
        {
            _currentLayout.objects.Add(new PlacedObjectData
            {
                type = type,
                position = go.transform.position,
                euler = go.transform.eulerAngles,
                scale = go.transform.localScale
            });
        }

        public void SaveLayout(string name)
        {
            _currentLayout.name = name;
            string json = JsonUtility.ToJson(_currentLayout, true);
            string path = Path.Combine(Application.persistentDataPath, name + ".track.json");
            File.WriteAllText(path, json);
            Debug.Log("[TrackEditor] Saved: " + path);

            // Optional multiplayer sync hook
            if (NetworkBackendClient.Instance != null)
            {
                // Could send layout json as custom message in future
            }
        }

        public void LoadLayout(string name)
        {
            string path = Path.Combine(Application.persistentDataPath, name + ".track.json");
            if (!File.Exists(path)) return;

            ClearAll();
            string json = File.ReadAllText(path);
            _currentLayout = JsonUtility.FromJson<TrackLayout>(json);

            foreach (var data in _currentLayout.objects)
            {
                GameObject prefab = GetPrefab(data.type);
                GameObject go;
                if (prefab != null)
                    go = Instantiate(prefab, data.position, Quaternion.Euler(data.euler));
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.position = data.position;
                    go.transform.rotation = Quaternion.Euler(data.euler);
                }
                go.transform.localScale = data.scale;
                _spawned.Add(go);
            }

            Debug.Log("[TrackEditor] Loaded: " + name);
        }

        public void ClearAll()
        {
            foreach (var go in _spawned)
                if (go != null) Destroy(go);
            _spawned.Clear();
            _currentLayout.objects.Clear();
        }

        public bool IsEditorActive => _editorActive;
    }
}
