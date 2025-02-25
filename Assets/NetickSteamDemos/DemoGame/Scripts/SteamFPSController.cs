using UnityEngine;
using Netick;
using Netick.Unity;

namespace Netick.Examples.Steam
{
    public struct SteamFPSInput : INetworkInput
    {
        public Vector2 YawPitch;
        public Vector2 Movement;
        public bool SprintInput;
        public bool ShootInput;
    }
    public class SteamFPSController : NetworkBehaviour
    {
        [SerializeField] private float  _movementSpeed = 2.5f;
        [SerializeField] private float  _sprintMultiplier = 1.65f;
        [SerializeField] private float  _movementAcceleration = 35;
        [SerializeField] private float _sensitivityX = 1.6f;
        [SerializeField] private float _sensitivityY = -1f;
        [SerializeField] private float _ShootForce = 10f;
        [SerializeField] private Transform _cameraParent;
        [SerializeField] private GameObject _ballPrefab;
        private CharacterController _CC;
        private Vector2 _camAngles;
        private bool cursorLocked;

        // Networked properties
        [Networked] public ulong SteamID { get; set; }
        [Networked] public Vector2 YawPitch { get; set; }
        [Networked(relevancy: Relevancy.InputSource)] Vector3 Velocity { get; set; }

        public override void NetworkStart()
        {
            _CC = GetComponent<CharacterController>();

            if (IsInputSource)
            {
                var cam                     = Sandbox.FindObjectOfType<Camera>();
                cam.transform.parent        = _cameraParent;
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localRotation = Quaternion.identity;
                if (Sandbox.IsServer)
                    numSpheres = 0;

                ToggleCursor();
            }

            if (Sandbox.IsServer)
                SteamID = Transports.Facepunch.FacepunchTransport.GetPlayerSteamID(InputSource);
        }

        void ToggleCursor()
        {
            cursorLocked = !cursorLocked;
            if (cursorLocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Confined;
                Cursor.visible = true;
            }
        }

        public override void OnInputSourceLeft()
        {
            // destroy the player object when its input source (controller player) leaves the game
            Sandbox.Destroy(Object);
        }
        
        public override void NetworkUpdate()
        {
            if (!IsInputSource || !Sandbox.InputEnabled)
                return;

            if (Input.GetKeyDown(KeyCode.Escape))
                ToggleCursor();

            var networkInput = Sandbox.GetInput<SteamFPSInput>();

            networkInput.Movement = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
            networkInput.ShootInput |= Input.GetMouseButtonDown(0);
            networkInput.SprintInput = Input.GetKey(KeyCode.LeftShift);

            Vector2 cameraInput = new Vector2(Input.GetAxisRaw("Mouse X") * _sensitivityX, Input.GetAxisRaw("Mouse Y") * _sensitivityY);
            if (Cursor.lockState != CursorLockMode.Locked)
                cameraInput *= 0;
            networkInput.YawPitch += cameraInput;

            // we apply the rotation in update too to have smooth camera control
            _camAngles = ClampAngles(_camAngles.x + cameraInput.x, _camAngles.y + cameraInput.y);
            ApplyRotations(_camAngles);

            Sandbox.SetInput<SteamFPSInput>(networkInput);
        }

        public override void NetworkFixedUpdate()
        {
            if (FetchInput(out SteamFPSInput input))
            {                
                MoveAndRotate(input);
            }
        }

        static int numSpheres = 0;
        private void MoveAndRotate(SteamFPSInput input)
        {
            // rotation
            // note: the rotation happens through the [OnChanged] callback below 
            YawPitch = ClampAngles(YawPitch.x + input.YawPitch.x, YawPitch.y + input.YawPitch.y);

            // movement direction
            var targetMovement = transform.TransformVector(new Vector3(input.Movement.x, 0, input.Movement.y)) * _movementSpeed * (input.SprintInput ? _sprintMultiplier : 1);
            targetMovement.y   = 0;

            Velocity = Vector3.MoveTowards(Velocity, targetMovement, Sandbox.FixedDeltaTime * _movementAcceleration);

            var gravity  = 15f * Vector3.down;

            // move
            _CC.Move((Velocity + gravity) * Sandbox.FixedDeltaTime);

            if (Sandbox.IsServer && input.ShootInput)
            {
                if (numSpheres > 480)
                    return;
                numSpheres++;
                Debug.Log($"{numSpheres} spheres have been spawned so far");
                var ball = Sandbox.NetworkInstantiate(_ballPrefab, transform.position + transform.forward + transform.up, Quaternion.identity);
                ball.GetComponent<Rigidbody>().AddForce(transform.forward * _ShootForce, ForceMode.Impulse);
            }
        }


        [OnChanged(nameof(YawPitch))]
        private void OnNetCamAnglesChanged(OnChangedData onChangedData)
        {
            ApplyRotations(YawPitch);
            _camAngles = YawPitch;
        }

        private void ApplyRotations(Vector2 camAngles)
        {
            // on the player transform, we apply yaw
            transform.rotation = Quaternion.Euler(new Vector3(0, camAngles.x, 0));

            // on the weapon/camera holder, we apply the pitch angle
            _cameraParent.localEulerAngles = new Vector3(camAngles.y, 0, 0);
        }


        private Vector2 ClampAngles(float yaw, float pitch)
        {
            return new Vector2(ClampAngle(yaw, -360, 360), ClampAngle(pitch, -80, 80));
        }

        private float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360F)
                angle += 360F;
            if (angle > 360F)
                angle -= 360F;
            return Mathf.Clamp(angle, min, max);
        }
    }
}
