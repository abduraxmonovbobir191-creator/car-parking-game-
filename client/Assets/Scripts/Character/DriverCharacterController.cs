using UnityEngine;

namespace CarParking.Character
{
    /// <summary>
    /// First and third person character controller for entering/exiting vehicles
    /// anywhere in the open world.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class DriverCharacterController : MonoBehaviour
    {
        [Header("Movement")]
        public float walkSpeed = 3.2f;
        public float runSpeed = 6.5f;
        public float jumpHeight = 1.2f;
        public float gravity = -18f;
        public float mouseSensitivity = 2.1f;

        [Header("Camera")]
        public Transform firstPersonCamera;
        public Transform thirdPersonPivot;
        public float thirdPersonDistance = 3.8f;
        public bool isFirstPerson = false;

        [Header("Vehicle Interaction")]
        public float enterDistance = 3.5f;
        public KeyCode interactKey = KeyCode.F;
        public KeyCode cameraToggleKey = KeyCode.V;

        private CharacterController _controller;
        private Vector3 _velocity;
        private float _xRotation;
        private bool _isInVehicle;
        private GameObject _currentVehicle;
        private Transform _seatPoint;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void Update()
        {
            if (_isInVehicle)
            {
                if (Input.GetKeyDown(interactKey))
                    ExitVehicle();
                return;
            }

            HandleMouseLook();
            HandleMovement();
            HandleCameraToggle();

            if (Input.GetKeyDown(interactKey))
                TryEnterNearestVehicle();
        }

        private void HandleMouseLook()
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            _xRotation -= mouseY;
            _xRotation = Mathf.Clamp(_xRotation, -85f, 85f);

            if (isFirstPerson && firstPersonCamera != null)
            {
                firstPersonCamera.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
                transform.Rotate(Vector3.up * mouseX);
            }
            else if (thirdPersonPivot != null)
            {
                thirdPersonPivot.Rotate(Vector3.up * mouseX);
                thirdPersonPivot.localRotation = Quaternion.Euler(_xRotation, thirdPersonPivot.localEulerAngles.y, 0f);
            }
        }

        private void HandleMovement()
        {
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");
            bool running = Input.GetKey(KeyCode.LeftShift);

            Vector3 move = transform.right * x + transform.forward * z;
            float speed = running ? runSpeed : walkSpeed;

            _controller.Move(move * speed * Time.deltaTime);

            if (_controller.isGrounded && _velocity.y < 0)
                _velocity.y = -2f;

            if (Input.GetButtonDown("Jump") && _controller.isGrounded)
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            _velocity.y += gravity * Time.deltaTime;
            _controller.Move(_velocity * Time.deltaTime);
        }

        private void HandleCameraToggle()
        {
            if (Input.GetKeyDown(cameraToggleKey))
            {
                isFirstPerson = !isFirstPerson;
                if (firstPersonCamera) firstPersonCamera.gameObject.SetActive(isFirstPerson);
                if (thirdPersonPivot) thirdPersonPivot.gameObject.SetActive(!isFirstPerson);
            }
        }

        private void TryEnterNearestVehicle()
        {
            Collider[] cols = Physics.OverlapSphere(transform.position, enterDistance);
            GameObject nearest = null;
            float nearestDist = float.MaxValue;

            foreach (var col in cols)
            {
                if (col.CompareTag("Vehicle") || col.GetComponent<CarParking.Vehicle.RealisticVehicleController>() != null)
                {
                    float dist = Vector3.Distance(transform.position, col.transform.position);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = col.gameObject;
                    }
                }
            }

            if (nearest != null)
                EnterVehicle(nearest);
        }

        public void EnterVehicle(GameObject vehicle)
        {
            _currentVehicle = vehicle;
            _isInVehicle = true;

            _controller.enabled = false;

            // Find seat
            _seatPoint = vehicle.transform.Find("Seat") ?? vehicle.transform;
            transform.SetParent(_seatPoint);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;

            // Disable character rendering if needed
            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = false;

            // Enable vehicle input
            var vc = vehicle.GetComponent<CarParking.Vehicle.RealisticVehicleController>();
            if (vc != null) vc.enabled = true;

            var bridge = vehicle.GetComponent<CarParking.Vehicle.VehicleEnvironmentBridge>();
            if (bridge != null) bridge.enabled = true;
        }

        public void ExitVehicle()
        {
            if (_currentVehicle == null) return;

            transform.SetParent(null);

            // Place character beside the vehicle
            Vector3 exitPos = _currentVehicle.transform.position + _currentVehicle.transform.right * 2.2f;
            exitPos.y = _currentVehicle.transform.position.y;
            transform.position = exitPos;

            _controller.enabled = true;

            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers) r.enabled = true;

            var vc = _currentVehicle.GetComponent<CarParking.Vehicle.RealisticVehicleController>();
            if (vc != null) vc.enabled = false;

            _isInVehicle = false;
            _currentVehicle = null;
            _seatPoint = null;
        }

        public bool IsInVehicle => _isInVehicle;
    }
}
