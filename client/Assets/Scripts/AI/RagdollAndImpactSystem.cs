using UnityEngine;

namespace CarParking.AI
{
    /// <summary>
    /// Ragdoll physics on pedestrian impact + driver ejection on high-speed crashes.
    /// </summary>
    public class RagdollAndImpactSystem : MonoBehaviour
    {
        public static RagdollAndImpactSystem Instance { get; private set; }

        [Header("Pedestrian Impact")]
        public float minImpulseForRagdoll = 2500f;
        public float ragdollForceMult = 1.2f;
        public float maxRagdollForce = 25f;

        [Header("Driver Ejection")]
        public float ejectionSpeedThreshold = 28f;
        public float ejectionImpulseThreshold = 18000f;
        public GameObject driverRagdollPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnEnable()
        {
            // Hook via collision messages on vehicles — vehicles call NotifyVehicleCollision
        }

        /// <summary>
        /// Call from vehicle OnCollisionEnter when hitting a character/pedestrian.
        /// </summary>
        public void ProcessPedestrianHit(GameObject pedestrian, Collision collision, Rigidbody vehicleRb)
        {
            if (pedestrian == null || collision.contactCount == 0) return;

            float impulse = collision.impulse.magnitude;
            if (impulse < minImpulseForRagdoll) return;

            ActivateRagdoll(pedestrian, collision, vehicleRb, impulse);
        }

        public void ActivateRagdoll(GameObject npc, Collision collision, Rigidbody vehicleRb, float impulse)
        {
            var animator = npc.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;

            var characterController = npc.GetComponent<CharacterController>();
            if (characterController != null) characterController.enabled = false;

            // Enable all child rigidbodies / colliders for ragdoll
            var bodies = npc.GetComponentsInChildren<Rigidbody>();
            bool hadBodies = bodies.Length > 0;

            if (!hadBodies)
            {
                var rb = npc.GetComponent<Rigidbody>();
                if (rb == null) rb = npc.AddComponent<Rigidbody>();
                rb.mass = 70f;
                rb.isKinematic = false;
                bodies = new[] { rb };
            }

            ContactPoint cp = collision.GetContact(0);
            Vector3 forceDir = vehicleRb != null
                ? vehicleRb.linearVelocity.normalized
                : (cp.point - collision.transform.position).normalized;

            float force = Mathf.Min(impulse * 0.001f * ragdollForceMult, maxRagdollForce);

            foreach (var rb in bodies)
            {
                rb.isKinematic = false;
                rb.AddForceAtPosition(forceDir * force + Vector3.up * force * 0.4f, cp.point, ForceMode.Impulse);
            }

            // Disable AI scripts if any
            var behaviours = npc.GetComponents<MonoBehaviour>();
            foreach (var b in behaviours)
            {
                if (b != this && b.GetType().Name.Contains("Pedestrian") || b.GetType().Name.Contains("Agent"))
                    b.enabled = false;
            }

            Debug.Log("[Ragdoll] NPC hit with impulse " + impulse.ToString("F0"));
        }

        /// <summary>
        /// High-speed crash driver ejection.
        /// </summary>
        public void TryEjectDriver(GameObject vehicle, Collision collision)
        {
            if (vehicle == null || collision.contactCount == 0) return;

            var rb = vehicle.GetComponent<Rigidbody>();
            if (rb == null) return;

            float speed = rb.linearVelocity.magnitude;
            float impulse = collision.impulse.magnitude;

            if (speed < ejectionSpeedThreshold && impulse < ejectionImpulseThreshold)
                return;

            // Spawn ragdoll driver
            Vector3 ejectPos = vehicle.transform.position + vehicle.transform.up * 1.2f + vehicle.transform.right * 0.5f;
            GameObject ragdoll;

            if (driverRagdollPrefab != null)
                ragdoll = Instantiate(driverRagdollPrefab, ejectPos, vehicle.transform.rotation);
            else
            {
                ragdoll = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                ragdoll.name = "EjectedDriver";
                ragdoll.transform.position = ejectPos;
                var rrb = ragdoll.AddComponent<Rigidbody>();
                rrb.mass = 75f;
                rrb.AddForce(rb.linearVelocity * 0.8f + Vector3.up * 5f, ForceMode.VelocityChange);
            }

            Debug.Log("[Ragdoll] Driver ejected at speed " + (speed * 3.6f).ToString("F0") + " km/h");
        }
    }

    /// <summary>
    /// Attach to vehicles to forward collision events to RagdollAndImpactSystem.
    /// </summary>
    public class VehicleImpactForwarder : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            if (RagdollAndImpactSystem.Instance == null) return;

            // Pedestrian layer / tag check
            if (collision.gameObject.CompareTag("Pedestrian") ||
                collision.gameObject.GetComponent<CharacterController>() != null)
            {
                var rb = GetComponent<Rigidbody>();
                RagdollAndImpactSystem.Instance.ProcessPedestrianHit(collision.gameObject, collision, rb);
            }

            // Driver ejection on heavy crash
            if (collision.relativeVelocity.magnitude > 20f)
                RagdollAndImpactSystem.Instance.TryEjectDriver(gameObject, collision);
        }
    }
}
