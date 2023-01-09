using UnityEngine;
using Netick;

namespace Netick.Examples.Steam
{
    public class SteamFPSController : NetworkBehaviour
    {
        [SerializeField] private float  _movementSpeed = 2.5f;
        [SerializeField] private float  _sprintMultiplier = 1.65f;
        [SerializeField] private float  _movementAcceleration = 35;
        [SerializeField] private float _sensitivityX = 1.6f;
        [SerializeField] private float _sensitivityY = -1f;
        [SerializeField] private float _ShootForce = 10f;
        [SerializeField] private Transform _cameraParent;
        private CharacterController _CC;
        private Vector2 _camAngles;

        public GameObject ballPrefab;

        // Networked properties
        [Networked] public Vector2   YawPitch                     { get; set; }
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

            Vector2 input = new Vector2(Input.GetAxisRaw("Mouse X") * _sensitivityX, Input.GetAxisRaw("Mouse Y") * _sensitivityY);
            Sandbox.GetInput<SteamFPSInput>().YawPitch += input;

            // we apply the rotation in update too to have smooth camera control
            _camAngles = ClampAngles(_camAngles.x + input.x, _camAngles.y + input.y);
            ApplyRotations(_camAngles);
        }

        public override void NetworkFixedUpdate()
        {
            if (FetchInput(out SteamFPSInput input))
            {                
                MoveAndRotate(input);
            }
        }

        int numSpheres = 0;
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
                numSpheres++;
                Debug.Log($"{numSpheres} have been spawned so far");
                var ball = Sandbox.NetworkInstantiate(ballPrefab, transform.position + transform.forward + transform.up, Quaternion.identity);
                ball.GetComponent<Rigidbody>().AddForce(transform.forward * _ShootForce, ForceMode.Impulse);
            }
        }


        [OnChanged(nameof(YawPitch))]
        private void OnNetCamAnglesChanged(Vector2 previous)
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

