using UnityEngine;
using NextGenDrive.Physics;

namespace NextGenDrive.Player
{
    public class CharacterVehicleInteraction : MonoBehaviour
    {
        [Header("Character & Vehicle References")]
        public GameObject characterObject;
        public Transform driverSeatPoint;
        public Transform exitPoint;
        public RealisticCarController currentVehicle;

        [Header("Interactive Vehicle Doors & Hood")]
        public Transform hoodTransform;
        public Transform driverDoorTransform;
        public Transform trunkTransform;

        public float openAngle = 55f;
        public float animSpeed = 6f;

        private bool isInsideVehicle = false;
        private bool isHoodOpen = false;
        private bool isDoorOpen = false;
        private bool isTrunkOpen = false;

        private void Update()
        {
            SmoothAnimatePanels();
        }

        public void ToggleEnterExitVehicle()
        {
            if (currentVehicle == null) return;

            isInsideVehicle = !isInsideVehicle;

            if (isInsideVehicle)
            {
                // Place character in driver seat & enable driving physics input
                characterObject.transform.SetParent(driverSeatPoint);
                characterObject.transform.localPosition = Vector3.zero;
                characterObject.transform.localRotation = Quaternion.identity;
                isDoorOpen = false;
            }
            else
            {
                // Exit car to walk around in auction / open world
                characterObject.transform.SetParent(null);
                characterObject.transform.position = exitPoint.position;
                characterObject.transform.rotation = exitPoint.rotation;
                currentVehicle.SetInputs(0f, 0f, 1f, true); // Engage handbrake on exit
            }
        }

        public void ToggleHood() => isHoodOpen = !isHoodOpen;
        public void ToggleDriverDoor() => isDoorOpen = !isDoorOpen;
        public void ToggleTrunk() => isTrunkOpen = !isTrunkOpen;

        private void SmoothAnimatePanels()
        {
            if (hoodTransform != null)
            {
                Quaternion target = Quaternion.Euler(isHoodOpen ? -openAngle : 0f, 0f, 0f);
                hoodTransform.localRotation = Quaternion.Slerp(hoodTransform.localRotation, target, Time.deltaTime * animSpeed);
            }

            if (driverDoorTransform != null)
            {
                Quaternion target = Quaternion.Euler(0f, isDoorOpen ? openAngle : 0f, 0f);
                driverDoorTransform.localRotation = Quaternion.Slerp(driverDoorTransform.localRotation, target, Time.deltaTime * animSpeed);
            }

            if (trunkTransform != null)
            {
                Quaternion target = Quaternion.Euler(isTrunkOpen ? openAngle : 0f, 0f, 0f);
                trunkTransform.localRotation = Quaternion.Slerp(trunkTransform.localRotation, target, Time.deltaTime * animSpeed);
            }
        }
    }
}
