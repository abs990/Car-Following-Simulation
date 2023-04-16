using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Collections.Specialized;
using Utils;

namespace SimulationCar
{
    public class AutomatedControl : MonoBehaviour
    {
        private float controllerInputX;
        private float controllerInputY;
        private float controllerInputReverse;
        private float controllerInputHandBrake;
        private float maxVelocity;
        private float brakingDistance;
        private Vector3 desiredDirection;
        private RouteManager currentRouteManager;
        private float k_1D;
        private float k_1A;
        private float k_2D;
        private float k_2A;
        private float eta;
        private float tou;
        public Utils.SimulationSingleton.CarState currentState = SimulationSingleton.CarState.IDLE;
        public bool laneSwitchAllowed;
        public bool allowStart = false;

        public float intermediatePosition = 1f;

        public float ControllerInputX
        {
            get { return controllerInputX; }
        }

        public float ControllerInputY
        {
            get { return controllerInputY; }
        }

        public float ControllerInputReverse
        {
            get { return controllerInputReverse; }
        }

        public float ControllerInputHandBrake
        {
            get { return controllerInputHandBrake; }
        }

        // Update is called once per frame

        void Start()
        {
            SetupRouteManager();
            this.gameObject.GetComponent<Rigidbody>().freezeRotation = true;
            currentState = SimulationSingleton.CarState.STRAIGHT_DRIVE;

            controllerInputY = 0f;
            controllerInputX = 0f;
            controllerInputReverse = 0f;
            controllerInputHandBrake = 0f;
        }
        void Update()
        {

            if(!allowStart || currentRouteManager.GetCurrentLane(this.gameObject) == -1)
            {
                return;
            }
            
            ForwardDriveUpdate();

            if(currentState == SimulationSingleton.CarState.STRAIGHT_DRIVE)
            {
                if(this.laneSwitchAllowed && 
                   controllerInputY > 0 &&
                   this.GetComponent<CarController>().GetVel >= 20f && 
                   currentRouteManager.LaneSwitchPreCheck(this.gameObject))
                {
                    currentState = SimulationSingleton.CarState.LANE_SWITCH;
                }
            }
            else if(currentState == SimulationSingleton.CarState.LANE_SWITCH)
            {
                int currentLane = currentRouteManager.GetCurrentLane(this.gameObject);
                Vector3 currentCarPosition = this.transform.position;
                float currentLinePosition = desiredDirection == Vector3.forward ? currentCarPosition.x : 
                                                                                  currentCarPosition.z;
                float targetLinePosition = desiredDirection == Vector3.forward ? currentRouteManager.routeStartPosition.x : 
                                                                                 currentRouteManager.routeStartPosition.z;
                targetLinePosition -= currentLane == 0 ? currentRouteManager.laneOffset : 0f;
                if(Math.Abs(currentLinePosition - targetLinePosition) <= 0.1f)
                {
                    controllerInputX = 0f;
                    currentState = SimulationSingleton.CarState.STRAIGHT_DRIVE;
                    currentRouteManager.FinalizeCarLaneSwitch(this.gameObject);
                    this.laneSwitchAllowed = false;
                }
                else
                {
                    SteerUpdate();
                }
            }
        }

        private void SetupRouteManager()
        {
            currentRouteManager = this.gameObject.GetComponentInParent<RouteManager>();
            if(currentRouteManager.varyX)
            {
                desiredDirection = Vector3.right;
            }
            else
            {
                desiredDirection = Vector3.forward;
            }

            // AOVRV params
            this.k_1D = currentRouteManager.k_1D;
            this.k_1A = currentRouteManager.k_1A;
            this.k_2D = currentRouteManager.k_2D;
            this.k_2A = currentRouteManager.k_2A;
            this.eta = currentRouteManager.eta;
            this.tou = currentRouteManager.tau;

            // driving restriction
            System.Random random = new System.Random();
            this.maxVelocity = currentRouteManager.baseVelocityBound + random.Next(0,currentRouteManager.velocityRandomizer);
            this.brakingDistance = this.maxVelocity * currentRouteManager.brakingFactor;
            this.laneSwitchAllowed = currentRouteManager.GetLaneSwitchFlag(this.gameObject);
        }

        private void ForwardDriveUpdate()
        {
            GameObject[] leadCars = currentRouteManager.GetLeadCars(this.gameObject);
            float[] leadCarWeights = currentRouteManager.GetLeadCarWeights(this.gameObject);
            int currentLane = currentRouteManager.GetCurrentLane(this.gameObject);
            
            if(leadCars == null || leadCars[currentLane] == null)
            {
                float leadCarVelocity = this.GetComponent<CarController>().GetVel;
                float currentBrakingDistance = currentRouteManager.GetBrakingDistance(leadCarVelocity);
                if(desiredDirection == Vector3.forward)
                {
                    float currentDistFromEnd = this.gameObject.GetComponentInParent<RouteManager>().routeEndPosition.z - this.transform.position.z;
                    if (currentBrakingDistance >= currentDistFromEnd)
                    {
                        controllerInputY = -1f;
                    }
                    else if(leadCarVelocity < this.maxVelocity)
                    {
                        controllerInputY = 1f;
                    }
                    else
                    {
                        controllerInputY = 0f;
                    }
                }
                else if(desiredDirection == Vector3.right)
                {
                    float currentDistFromEnd = this.gameObject.GetComponentInParent<RouteManager>().routeEndPosition.x - this.transform.position.x;
                    if (currentBrakingDistance >= currentDistFromEnd)
                    {
                        controllerInputY = -1f;
                    }
                    else if(leadCarVelocity < this.maxVelocity)
                    {
                        controllerInputY = 1f;
                    }
                    else
                    {
                        controllerInputY = 0f;
                    }
                }            
            }
            else
            {
                print(this.gameObject.name+" LEAD_CARS "+leadCarWeights[0]+" "+leadCars[0]+" "+leadCarWeights[1]+" "+leadCars[1]);
                float forwardDelta = 0f;

                // weighted AOVRV
                for(int i=0; i<2; i++)
                {
                    GameObject leadCar = leadCars[i];
                    float leadCarWeight = leadCarWeights[i];

                    if(leadCar == null)
                    {
                        // controllerInputY += 0f;
                        forwardDelta += 0f;
                    }
                    else
                    {
                        Vector3 leadCarVelocity = leadCar.GetComponent<Rigidbody>().velocity;
                        Vector3 currentcarVelocity = this.gameObject.GetComponent<Rigidbody>().velocity;
                        
                        // wheel colliders need to compute follow distance
                        WheelCollider[] wcLeadCar = leadCar.GetComponentsInChildren<WheelCollider>();
                        WheelCollider[] wcCurrentCar = this.gameObject.GetComponentsInChildren<WheelCollider>();
                        Vector3 leadCarRearWheelPosition;
                        Quaternion leadCarRearWheelRotation;
                        wcLeadCar[0].GetWorldPose(out leadCarRearWheelPosition, out leadCarRearWheelRotation);
                        Vector3 currentCarFrontWheelPosition;
                        Quaternion currentCarFrontWheelRotation;
                        wcCurrentCar[2].GetWorldPose(out currentCarFrontWheelPosition, out currentCarFrontWheelRotation);
                        
                        float leadCarVelocityMag = 0f;
                        float currentcarVelocityMag = 0f;
                        float relativeVelocity = 0f;
                        float followDistance = 0f;
                        
                        if(desiredDirection == Vector3.forward)
                        {
                            leadCarVelocityMag = leadCarVelocity.z;
                            currentcarVelocityMag = currentcarVelocity.z;
                            followDistance = Mathf.Abs(leadCarRearWheelPosition.z - currentCarFrontWheelPosition.z);
                        }
                        else
                        {
                            leadCarVelocityMag = leadCarVelocity.x;
                            currentcarVelocityMag = currentcarVelocity.x;
                            followDistance = Mathf.Abs(leadCarRearWheelPosition.x - currentCarFrontWheelPosition.x);
                        }

                        // compute magnitude of relative velocity
                        relativeVelocity = leadCarVelocityMag - currentcarVelocityMag;

                        // compute delta required based on relative velocity
                        float deltaY = 0f;
                        if(relativeVelocity <= 0)
                        {
                            deltaY = k_1D * (followDistance - eta - tou * currentcarVelocityMag) + k_2D * relativeVelocity;
                        }
                        else
                        {
                            deltaY = k_1A * (followDistance - eta - tou * currentcarVelocityMag) + k_2A * relativeVelocity;
                        }

                        forwardDelta += leadCarWeight * deltaY;
                    }
                }

                // update input along drive
                controllerInputY = forwardDelta;
            }
        }

        private void SteerUpdate()
        {
            if(currentState == SimulationSingleton.CarState.LANE_SWITCH)
            {
                int laneNum = currentRouteManager.GetCurrentLane(this.gameObject);
                controllerInputX = laneNum == 0? 2f : -2f;
            }
            else
            {
                controllerInputX = 0f;
            }
        }
    }
}
