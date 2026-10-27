using UnityEngine;
using System.Collections;

namespace CarParking.Character
{
    /// <summary>
    /// Handles opening/closing doors, hood and trunk with smooth animation.
    /// </summary>
    public class VehicleDoorInteraction : MonoBehaviour
    {
        [System.Serializable]
        public class Door
        {
            public string name;
            public Transform doorTransform;
            public Vector3 closedLocalEuler;
            public Vector3 openLocalEuler;
            public float openAngle = 65f;
            public bool isOpen;
            public bool isHoodOrTrunk;
        }

        public Door[] doors;
        public float animationSpeed = 3.5f;
        public KeyCode interactKey = KeyCode.E;
        public float interactRange = 2.8f;

        private DriverCharacterController _character;

        private void Start()
        {
            _character = FindFirstObjectByType<DriverCharacterController>();

            foreach (var d in doors)
            {
                if (d.doorTransform != null)
                    d.closedLocalEuler = d.doorTransform.localEulerAngles;
            }
        }

        private void Update()
        {
            if (_character != null && _character.IsInVehicle) return;

            if (Input.GetKeyDown(interactKey))
            {
                TryInteractNearestDoor();
            }
        }

        private void TryInteractNearestDoor()
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, interactRange))
            {
                foreach (var door in doors)
                {
                    if (door.doorTransform != null &&
                        (hit.transform == door.doorTransform || hit.transform.IsChildOf(door.doorTransform)))
                    {
                        ToggleDoor(door);
                        break;
                    }
                }
            }
        }

        public void ToggleDoor(Door door)
        {
            StopAllCoroutines();
            StartCoroutine(AnimateDoor(door, !door.isOpen));
        }

        private IEnumerator AnimateDoor(Door door, bool open)
        {
            Quaternion start = door.doorTransform.localRotation;
            Quaternion end = open
                ? Quaternion.Euler(door.openLocalEuler)
                : Quaternion.Euler(door.closedLocalEuler);

            if (door.openLocalEuler == Vector3.zero && open)
            {
                // Fallback: rotate around local Y or X depending on type
                Vector3 euler = door.closedLocalEuler;
                if (door.isHoodOrTrunk)
                    euler.x -= door.openAngle;
                else
                    euler.y += door.openAngle;

                end = Quaternion.Euler(euler);
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * animationSpeed;
                door.doorTransform.localRotation = Quaternion.Slerp(start, end, t);
                yield return null;
            }

            door.doorTransform.localRotation = end;
            door.isOpen = open;
        }
    }
}
