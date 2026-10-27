using System.Collections;
using UnityEngine;
using CarParking.Vehicle;

namespace CarParking.Multiplayer
{
    public enum RaceType { Drag400m, Drag800m, DriftBattle }

    /// <summary>
    /// 1v1 Drag Race and Drift battle system with countdown tree,
    /// false-start detection and real-time telemetry (0-60, 0-100, 1/4 mile ET).
    /// </summary>
    public class StreetRaceAndTelemetrySystem : MonoBehaviour
    {
        public static StreetRaceAndTelemetrySystem Instance { get; private set; }

        [Header("Race")]
        public RaceType currentRaceType = RaceType.Drag400m;
        public float dragDistance400 = 402.336f;
        public float dragDistance800 = 804.672f;

        [Header("UI / Audio")]
        public AudioSource countdownSource;
        public AudioClip[] treeClips; // 3 amber + green

        private bool _raceActive;
        private bool _countdownRunning;
        private float _raceStartTime;
        private bool _falseStart;

        private class RacerTelemetry
        {
            public Transform transform;
            public Rigidbody rb;
            public DrivetrainSystem drivetrain;
            public float startTime;
            public float zeroToSixty = -1f;
            public float zeroToHundred = -1f;
            public float quarterMileET = -1f;
            public float trapSpeed = -1f;
            public bool finished;
            public Vector3 startPos;
        }

        private RacerTelemetry _player;
        private RacerTelemetry _opponent;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void ChallengePlayer(GameObject playerVehicle, GameObject opponentVehicle, RaceType type)
        {
            if (_raceActive || _countdownRunning) return;

            currentRaceType = type;
            _player = CreateTelemetry(playerVehicle);
            _opponent = CreateTelemetry(opponentVehicle);
            _falseStart = false;

            StartCoroutine(CountdownTree());
        }

        private RacerTelemetry CreateTelemetry(GameObject go)
        {
            return new RacerTelemetry
            {
                transform = go.transform,
                rb = go.GetComponent<Rigidbody>(),
                drivetrain = go.GetComponent<DrivetrainSystem>(),
                startPos = go.transform.position
            };
        }

        private IEnumerator CountdownTree()
        {
            _countdownRunning = true;

            // Three amber lights
            for (int i = 0; i < 3; i++)
            {
                if (countdownSource != null && treeClips != null && i < treeClips.Length)
                    countdownSource.PlayOneShot(treeClips[i]);
                yield return new WaitForSeconds(0.5f);

                // False start check during tree
                if (IsMoving(_player) || IsMoving(_opponent))
                {
                    _falseStart = true;
                    Debug.Log("[Race] FALSE START!");
                    _countdownRunning = false;
                    yield break;
                }
            }

            // Green
            if (countdownSource != null && treeClips != null && treeClips.Length > 3)
                countdownSource.PlayOneShot(treeClips[3]);

            _raceStartTime = Time.time;
            _player.startTime = _raceStartTime;
            _opponent.startTime = _raceStartTime;
            _raceActive = true;
            _countdownRunning = false;
        }

        private bool IsMoving(RacerTelemetry r)
        {
            if (r == null || r.rb == null) return false;
            return r.rb.linearVelocity.magnitude > 0.8f;
        }

        private void Update()
        {
            if (!_raceActive) return;

            UpdateTelemetry(_player);
            UpdateTelemetry(_opponent);

            float dist = currentRaceType == RaceType.Drag800m ? dragDistance800 : dragDistance400;

            if (!_player.finished && Vector3.Distance(_player.startPos, _player.transform.position) >= dist)
            {
                _player.finished = true;
                _player.quarterMileET = Time.time - _player.startTime;
                _player.trapSpeed = _player.rb.linearVelocity.magnitude * 3.6f;
                Debug.Log($"[Race] Player finished: {_player.quarterMileET:F3}s @ {_player.trapSpeed:F1} km/h");
            }

            if (!_opponent.finished && Vector3.Distance(_opponent.startPos, _opponent.transform.position) >= dist)
            {
                _opponent.finished = true;
                _opponent.quarterMileET = Time.time - _opponent.startTime;
                _opponent.trapSpeed = _opponent.rb.linearVelocity.magnitude * 3.6f;
            }

            if (_player.finished && _opponent.finished)
            {
                _raceActive = false;
                string winner = _player.quarterMileET < _opponent.quarterMileET ? "Player" : "Opponent";
                Debug.Log($"[Race] Winner: {winner}");
            }
        }

        private void UpdateTelemetry(RacerTelemetry r)
        {
            if (r == null || r.finished || r.rb == null) return;

            float speedMs = r.rb.linearVelocity.magnitude;
            float speedMph = speedMs * 2.23694f;
            float speedKmh = speedMs * 3.6f;
            float elapsed = Time.time - r.startTime;

            if (r.zeroToSixty < 0f && speedMph >= 60f)
                r.zeroToSixty = elapsed;

            if (r.zeroToHundred < 0f && speedKmh >= 100f)
                r.zeroToHundred = elapsed;
        }

        public void AbortRace()
        {
            _raceActive = false;
            _countdownRunning = false;
            _player = null;
            _opponent = null;
        }
    }
}
