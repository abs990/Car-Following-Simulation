// Adapted from online source
// https://github.com/AlTheSlacker/UnityCar

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace UnityCar
{

    public class Brakes : MonoBehaviour
    {

        [SerializeField] private float brakeMaxDeceleration = SimulationSingleton.BRAKE_MAX_DECELERATION;
        [SerializeField] private float brakeFrontBias = SimulationSingleton.BRAKE_FRONT_BIAS;

        private float maxBrakeTorqueFront;
        private float maxBrakeTorqueRear;

        void Start()
        {
            // calculate front/rear max brake force / torques per wheel
            // note brakeFrontBias needs to be modified depending on CoG Z (weight transfer during braking)
            Rigidbody rB = GetComponent<Rigidbody>();
            Suspension suspension = GetComponent<Suspension>();

            // compue maximum brake force
            float maxBrakeForce = brakeMaxDeceleration * Physics.gravity.y * rB.mass;

            // compute maximum brake force for front
            float maxBrakeForceFront = maxBrakeForce * brakeFrontBias / 2.0f;

            // compute maximum brake force for rear
            float maxBrakeForceRear = maxBrakeForce * (1.0f - brakeFrontBias) / 2.0f;

            // compute brake torque for front
            maxBrakeTorqueFront = maxBrakeForceFront * suspension.GetRollingRadiusFront;

            // compute brake torque for rear
            maxBrakeTorqueRear = maxBrakeForceRear * suspension.GetRollingRadiusRear;
        }

        public float[] GetBrakeTorques(float inputY)
        {
            float[] brakeTorques = new float[4];
            for (int i = 0; i < 4; i++)
            {
                // threshold to apply brake torque
                if (inputY >= SimulationSingleton.BRAKE_TORQUE_MIN_THRESHOLD) 
                {
                    brakeTorques[i] = 0.0f;
                }
                else
                {
                    brakeTorques[i] = i < 2 ? maxBrakeTorqueRear * inputY : maxBrakeTorqueFront * inputY;
                }
            }
            return brakeTorques;
        }
    }
}