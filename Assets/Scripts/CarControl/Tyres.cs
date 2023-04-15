// Adapted from online source
// https://github.com/AlTheSlacker/UnityCar

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SimulationCar
{

    public class Tyres : MonoBehaviour
    {
        // simple scalars for the tyre friction, OK for small balancing tweaks, but large changes will be best with new WFCs
        // why 1.1, well the tyres are set up for extreme limits of 1g, but it's easy to fall off the peaks and the extra 10% grip
        // just makes for a more enjoyable experience. With analogue controls and a traction control module, 1.0 would probably be OK.
        [SerializeField] private float tyreFrictionFront = 1.2f;
        [SerializeField] private float tyreFrictionRear = 1.2f;

        private readonly WheelFrictionCurve[] awfcLong = new WheelFrictionCurve[4];
        private readonly WheelFrictionCurve[] awfcLat = new WheelFrictionCurve[4];

        public WheelFrictionCurve[] GetWFCLong { get { return awfcLong; } }
        public WheelFrictionCurve[] GetWFCLat { get { return awfcLat; } }

        void Start()
        {

            // get wheelcolliders and particles
            WheelCollider[] wC = gameObject.GetComponentsInChildren<WheelCollider>();

            for(int i=0; i<4; i++)
            {
                // longitudinal slip (% of longitudinal travel) versus normalised load
                awfcLong[i].extremumSlip = 0.15f;
                awfcLong[i].extremumValue = 1.0f;
                awfcLong[i].asymptoteSlip = 0.70f;
                awfcLong[i].asymptoteValue = i < 2 ? 0.60f : 0.70f;
                awfcLong[i].stiffness = i < 2 ? tyreFrictionRear : tyreFrictionFront;
                // lateral slip = radians slip versus normalised load
                awfcLat[i].extremumSlip = 0.11f;
                awfcLat[i].extremumValue = 1.0f;
                awfcLat[i].asymptoteSlip = 0.80f;
                awfcLat[i].asymptoteValue = 0.70f;
                awfcLat[i].stiffness = i < 2 ? tyreFrictionRear : tyreFrictionFront;
            }

            // Assign the WFC data to the wheel colliders
            // WC sequence RL RR FL FR

            for (int i = 0; i < 4; i++)
            {
                wC[i].ConfigureVehicleSubsteps(30, 8, 20);
                wC[i].forwardFriction = awfcLong[i];
                wC[i].sidewaysFriction = awfcLat[i];
            }

        }

    }
}