using System;
using UnityEngine;

namespace NextGenDrive.Gameplay
{
    public class DriftAndDragScoring : MonoBehaviour
    {
        [Header("Drift Settings")]
        public float minDriftSpeedKmh = 25f;
        public float minDriftAngle = 15f;
        public float maxComboMultiplier = 10f;

        public float totalDriftScore = 0f;
        public float currentDriftPoints = 0f;
        public float currentMultiplier = 1f;

        [Header("Drag Race Telemetry (0-100 & 402m)")]
        public bool isDragRunActive = false;
        public float zeroToHundredSeconds = 0f;
        public float quarterMileSeconds = 0f;

        public event Action<float, float> OnDragRunCompleted; // 402m time, topSpeedKmh

        private Rigidbody rb;
        private float driftComboTimer = 0f;
        private Vector3 dragStartPosition;
        private float dragElapsedTimer = 0f;
        private bool reachedHundred = false;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            EvaluateDriftPhysics();
            EvaluateDragRun();
        }

        private void EvaluateDriftPhysics()
        {
            if (rb == null) return;

            Vector3 velocity = rb.linearVelocity;
            float speedKmh = velocity.magnitude * 3.6f;

            if (speedKmh < minDriftSpeedKmh)
            {
                BankDriftPointsIfIdle();
                return;
            }

            Vector3 flatForward = new Vector3(transform.forward.x, 0f, transform.forward.z).normalized;
            Vector3 flatVelocity = new Vector3(velocity.x, 0f, velocity.z).normalized;
            float driftAngle = Vector3.Angle(flatForward, flatVelocity);

            if (driftAngle >= minDriftAngle && driftAngle <= 115f)
            {
                driftComboTimer = 2.5f;
                currentMultiplier = Mathf.Min(maxComboMultiplier, currentMultiplier + Time.deltaTime * 0.45f);
                float pointsPerSec = (driftAngle * 2.2f) * (speedKmh * 0.08f) * currentMultiplier;
                currentDriftPoints += pointsPerSec * Time.deltaTime;
            }
            else
            {
                BankDriftPointsIfIdle();
            }
        }

        private void BankDriftPointsIfIdle()
        {
            if (currentDriftPoints <= 0f) return;

            driftComboTimer -= Time.deltaTime;
            if (driftComboTimer <= 0f)
            {
                totalDriftScore += Mathf.Round(currentDriftPoints);
                currentDriftPoints = 0f;
                currentMultiplier = 1f;
            }
        }

        public void OnCollisionEnter(Collision collision)
        {
            if (collision.relativeVelocity.magnitude * 3.6f > 15f)
            {
                // Cancel unbanked drift combo on wall crash
                currentDriftPoints = 0f;
                currentMultiplier = 1f;
            }
        }

        public void StartQuarterMileDrag()
        {
            dragStartPosition = transform.position;
            dragElapsedTimer = 0f;
            zeroToHundredSeconds = 0f;
            quarterMileSeconds = 0f;
            reachedHundred = false;
            isDragRunActive = true;
        }

        private void EvaluateDragRun()
        {
            if (!isDragRunActive || rb == null) return;

            dragElapsedTimer += Time.deltaTime;
            float speedKmh = rb.linearVelocity.magnitude * 3.6f;

            if (!reachedHundred && speedKmh >= 100f)
            {
                zeroToHundredSeconds = dragElapsedTimer;
                reachedHundred = true;
            }

            float distanceTraveled = Vector3.Distance(dragStartPosition, transform.position);
            if (distanceTraveled >= 402.336f) // Official 1/4 Mile
            {
                quarterMileSeconds = dragElapsedTimer;
                isDragRunActive = false;
                OnDragRunCompleted?.Invoke(quarterMileSeconds, speedKmh);
            }
        }
    }
}
