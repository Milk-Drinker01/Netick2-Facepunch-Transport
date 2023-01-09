using UnityEngine;
using Netick;

namespace Netick.Examples.Steam
{
    public class SteamFPSInput : NetworkInput
    {
        public Vector2 YawPitch;
        public Vector2 Movement;
        public bool SprintInput;
        public bool ShootInput;
    }
}