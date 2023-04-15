// Adapted from online source
// https://github.com/AlTheSlacker/UnityCar

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace SimulationCar
{

    public class AeroDynamics : MonoBehaviour
    {

        [SerializeField] private float frontalArea = SimulationSingleton.FRONTAL_AREA;
        [SerializeField] private float drag_coeff = SimulationSingleton.DRAG_COEFFICENT;
        [SerializeField] private float clFront = SimulationSingleton.CL_FRONT;
        [SerializeField] private float clRear = SimulationSingleton.CL_REAR;

        private float velDependentLiftFront;
        private float velDependentLiftRear;
        private float velDependentDrag;
        private Rigidbody rB;
        private WheelCollider[] wC;

        private const float airDensity = SimulationSingleton.AIR_DENSITY;

        void Start()
        {
            // get the car rigidbody
            rB = GetComponent<Rigidbody>();

            // get the array of wheel colliders
            wC = gameObject.GetComponentsInChildren<WheelCollider>();

            // calculate the velocity dependent lift and drag factors
            // tempDragVar is only used here
            float tempDragVar = airDensity * frontalArea * 0.5f;

            // lift is divided over 4 wheels, it's applied at the wheel locations for simplicity/stability
            // cl is the coefficient of lift, negative values of fCl = downforce 
            velDependentLiftFront = clFront * tempDragVar / 4.0f;
            velDependentLiftRear = clRear * tempDragVar / 4.0f;

            // cd is the coefficient of drag, note the sign control as Z+ is car forward
            velDependentDrag = drag_coeff * tempDragVar;
        }

        public void ApplyAeroDrag(float vel)
        {
            // compute drag
            float velSq = vel * vel;
            float drag = velDependentDrag * velSq;

            // invert drag if vehicle is in reverse
            if (vel < 0.0f) 
            {
                drag = -drag;
            }

            // add drag force to rigidbody
            if(this.rB != null)
            {
                this.rB.AddRelativeForce(0.0f, 0.0f, -drag, ForceMode.Force);
            }
        }

        public void ApplyAeroLift(float vel)
        {
            if(this.rB != null)
            {
                float velSq = vel * vel;

                // compute aero lift for front
                float liftFront = velDependentLiftFront * velSq;

                // compute aero lift for rear
                float liftRear = velDependentLiftRear * velSq;

                // add forces at wheel colliders
                for (int i = 0; i < 2; i++)
                {
                    rB.AddForceAtPosition(wC[i].transform.up * liftRear, wC[i].transform.position);
                    rB.AddForceAtPosition(wC[i + 2].transform.up * liftFront, wC[i + 2].transform.position);
                }
            }
        }

    }
}