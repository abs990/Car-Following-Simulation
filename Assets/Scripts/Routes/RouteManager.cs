using System.Collections.Generic;
using UnityEngine;
using Utils;
using SimulationCar;
using UnityEditor;
using System.Collections.Specialized;
using System;
using System.Collections;

public class RouteManager : MonoBehaviour
{
    public bool varyX;
    public int maxNumCarsPerLane = 1;
    private const int numLanes = 2;
    public float laneOffset = 5f;
    private List<Dictionary<GameObject, GameObject[]>> leadCarTracker = new List<Dictionary<GameObject, GameObject[]>>(numLanes);
    private List<Dictionary<GameObject, float[]>> leadCarWeightTracker = new List<Dictionary<GameObject, float[]>>(numLanes);
    public Dictionary<GameObject, int> carLaneIdTracker = new Dictionary<GameObject, int>();
    public List<Dictionary<GameObject, bool>> laneSwitchFlags = new List<Dictionary<GameObject, bool>>();
    private Vector3 availableSpawnPosition;
    public float spawnGap = 10f;
    public Vector3 routeStartPosition;
    public Vector3 routeEndPosition;
    public Quaternion spawnRotation;
    public bool controlLeadCar = false;
    public float k_1D = 0.5f;
    public float k_1A = 0.9f;
    public float k_2D = 0.5f;
    public float k_2A = 0.9f;
    public float eta = 5f;
    public float tau = 0.2f;
    public float baseVelocityBound = 35f;
    public int velocityRandomizer = 5;
    public float brakingFactor = 2f;
    public bool laneChange = false;
    public bool randomSpawnGaps = false;
    private bool trackingStructuresInitialized = false;
    private GameObject[] routeCameras = new GameObject[3];
    public float cameraSwitchInterval = 3.0f;
    private float timeSinceLastCameraSwitch = 0.0f;

    // Start is called before the first frame update
    void Start()
    {
        this.varyX = this.routeStartPosition.x != this.routeEndPosition.x;
        // assign available spawn position
        this.availableSpawnPosition = this.routeStartPosition;
        // load prefab
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulationSingleton.LEAD_CAR_PREFAB);
        //initialise tracking structures
        InitialiseTrackingStructures();
        // initialise list to hold previously initialised cars
        List<GameObject> prevCars = new List<GameObject>(numLanes);
        for(int l=0; l<numLanes; l++)
        {
            prevCars.Add(null);
        }
        // random variable to decide if a car will be spawned or not
        System.Random carSpawnRandomiser = new System.Random();
        System.Random carLaneSwitchRandomiser = new System.Random();

        int laneSwitchParameter = 1;
        if(laneChange)
        {
            laneSwitchParameter = 2;
        }

        for(int i=0; i < maxNumCarsPerLane; i++)
        {
            for(int l=0; l < numLanes; l++)
            {
                if(randomSpawnGaps & carSpawnRandomiser.Next(2) == 0)
                {
                    continue;
                }

                GameObject newCar = UnityEngine.Object.Instantiate(prefab, availableSpawnPosition + new Vector3(0f, 0f, -l * laneOffset), spawnRotation);
                newCar.name = "Car" + l.ToString()+ i.ToString();
                // disable user input
                newCar.GetComponent<UserInput>().enabled = false;
                // set parent
                newCar.transform.SetParent(this.transform);
                // assign car lane switch flag
                this.laneSwitchFlags[l].Add(newCar, carLaneSwitchRandomiser.Next(laneSwitchParameter) == 1);
                // this.laneSwitchFlags[l].Add(newCar, true);
                // update lead car tracker
                this.leadCarTracker[l].Add(newCar, new GameObject[]{null, null});
                // update lead car weight tracker
                this.leadCarWeightTracker[l].Add(newCar, new float[]{0, 0});
                // update lane tracker
                this.carLaneIdTracker.Add(newCar, l);
                // update entry for previously created car
                if(prevCars[l] != null)
                {
                    // update lead car tracker
                    GameObject[] leadCars = new GameObject[]{null, null};
                    float[] leadCarWeights = new float[]{0, 0};
                    // update tracker for weights of lead car
                    leadCarWeights[l] = 1f;
                    leadCars[l] = newCar;
                    // update for previous car
                    this.leadCarWeightTracker[l][prevCars[l]] = leadCarWeights;
                    this.leadCarTracker[l][prevCars[l]] = leadCars;
                }
                prevCars[l] = newCar;
            }

            // set next spawn position
            if(this.varyX)
            {
                this.availableSpawnPosition = this.availableSpawnPosition + new Vector3(this.spawnGap, 0f, 0f);
            }
            else
            {
                this.availableSpawnPosition = this.availableSpawnPosition + new Vector3(0f, 0f, this.spawnGap);
            }
        }

        // add camera to first spawned car in first lane
        GameObject routeCameraBack = new GameObject("routeCameraBack");
        routeCameraBack.AddComponent<Camera>();
        routeCameraBack.GetComponent<Camera>().targetDisplay = 0;
        GameObject routeCameraFront = new GameObject("routeCameraFront");
        routeCameraFront.AddComponent<Camera>();
        routeCameraFront.GetComponent<Camera>().targetDisplay = 0;
        GameObject[] keys = new GameObject[this.leadCarTracker[0].Keys.Count];
        GameObject routeCameraTop = new GameObject("routeCameraTop");
        routeCameraTop.AddComponent<Camera>();
        routeCameraTop.GetComponent<Camera>().targetDisplay = 0;
        if(keys.Length > 0)
        {
            this.leadCarTracker[0].Keys.CopyTo(keys, 0);
            routeCameraBack.transform.SetParent(keys[0].transform);
            routeCameraBack.transform.localPosition = new Vector3(-10f, 10f, -25f);
            routeCameraBack.transform.localRotation = Quaternion.identity;
            routeCameras[0] = routeCameraBack;
            routeCameraFront.transform.SetParent(keys[this.leadCarTracker[0].Keys.Count - 1].transform);
            routeCameraFront.transform.localPosition = new Vector3(10f, 10f, 25f);
            routeCameraFront.transform.forward = Vector3.left;
            routeCameras[1] = routeCameraFront;
            routeCameraTop.transform.SetParent(keys[0].transform);
            routeCameraTop.transform.localPosition = new Vector3(30f, 125f, 0f);
            routeCameraTop.transform.forward = Vector3.down;
            routeCameras[2] = routeCameraTop;
        }

        // allow the vehicles to start
        for(int l=0; l < numLanes; l++)
        {
            foreach(GameObject car in this.leadCarTracker[l].Keys)
            {
                car.GetComponent<AutomatedControl>().allowStart = true;
            }
        }

        // set first camera to active
        routeCameras[0].SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        if(this.controlLeadCar)
        {
            GameObject[] keys = new GameObject[this.leadCarTracker[0].Keys.Count];
            this.leadCarTracker[0].Keys.CopyTo(keys, 0);
            GameObject leadCar = keys[this.leadCarTracker[0].Keys.Count - 1];
            leadCar.GetComponent<AutomatedControl>().enabled = false;
            leadCar.GetComponent<UserInput>().enabled = true;
        }
    }

    void LateUpdate()
    {
        // Increment the time since the last switch
        timeSinceLastCameraSwitch += Time.deltaTime;

        // Check if it's time to switch cameras
        if (timeSinceLastCameraSwitch >= cameraSwitchInterval)
        {
            // Reset the time since the last switch
            timeSinceLastCameraSwitch = 0.0f;

            // Disable all cameras
            foreach (var camera in routeCameras)
            {
                camera.SetActive(false);
            }

            // Choose a random camera and activate it
            var randomCameraIndex = new System.Random().Next(0, routeCameras.Length);
            routeCameras[randomCameraIndex].SetActive(true);
        }
    }

    private void InitialiseTrackingStructures()
    {
        // initialise trackers for each lane
        for(int l=0; l < numLanes; l++)
        {
            this.leadCarTracker.Add(new Dictionary<GameObject, GameObject[]>());
            this.leadCarWeightTracker.Add(new Dictionary<GameObject, float[]>());
            this.laneSwitchFlags.Add(new Dictionary<GameObject, bool>());
        }

        trackingStructuresInitialized = true;
    }

    public int GetCurrentLane(GameObject car)
    {
        int laneNumber;
        bool result = this.carLaneIdTracker.TryGetValue(car, out laneNumber);
        return result ? laneNumber : -1;
    }
    public GameObject[] GetLeadCars(GameObject car)
    {
        int laneNum = GetCurrentLane(car);
        int altLaneNum = 1 - laneNum;
        GameObject[] value = new GameObject[numLanes];
        bool result = this.leadCarTracker[laneNum].TryGetValue(car, out value);
        if(result)
        {
            if(laneChange)
            {
                UpdateLeadCars(car);
                value[0] = this.leadCarTracker[laneNum][car][0];
                value[1] = this.leadCarTracker[laneNum][car][1];
            }
            return value;
        }
        else
        {
            return null;
        }
    }

    public float[] GetLeadCarWeights(GameObject car)
    {
        int laneNum = GetCurrentLane(car);
        float[] value = new float[numLanes];
        bool result = this.leadCarWeightTracker[laneNum].TryGetValue(car, out value);
        if(result)
        {
            return value;
        }
        else
        {
            return null;
        }
    }

    public bool GetLaneSwitchFlag(GameObject car)
    {
        int laneNum = GetCurrentLane(car);
        return this.laneSwitchFlags[laneNum][car];
    }

    public void FinalizeCarLaneSwitch(GameObject car)
    {
        int laneNum = GetCurrentLane(car);

        // remove car from original lane
        this.leadCarTracker[laneNum].Remove(car);
        this.leadCarWeightTracker[laneNum].Remove(car);

        // get other lane's number
        int altLaneNum = 1 - laneNum;

        // create entry for car in new lane
        this.leadCarTracker[altLaneNum].Add(car, new GameObject[]{null, null});
        this.leadCarWeightTracker[altLaneNum].Add(car, new float[]{0f, 0f});

        // update lane number tracker
        this.carLaneIdTracker[car] = altLaneNum;                    
    }

    public float GetCarIntermediateFractionPosition(GameObject car)
    {
        int laneNum = GetCurrentLane(car);
        float currentLateralPosition = 0f, laneZeroDist = 0f, intermediatePosition = 0f;
        if(varyX)
        {
            currentLateralPosition = car.transform.position.z;
            laneZeroDist = Math.Abs(currentLateralPosition - this.routeStartPosition.z);
            print("laneZeroDist"+" "+car.name+" "+laneZeroDist);
            intermediatePosition = (laneNum == 0)? laneZeroDist : this.laneOffset - laneZeroDist;
            intermediatePosition /= this.laneOffset;
        }
        else
        {
            currentLateralPosition = car.transform.position.x;
            laneZeroDist = Math.Abs(currentLateralPosition - this.routeStartPosition.x);
            intermediatePosition = (laneNum == 0)? laneZeroDist : this.laneOffset - laneZeroDist;
            intermediatePosition /= this.laneOffset;
        }
        intermediatePosition = Math.Abs(intermediatePosition) < 0.1f ? 0 : intermediatePosition;
        print("Intermediate pos"+" "+car.name+" "+intermediatePosition);
        return intermediatePosition;        
    }

    public bool LaneSwitchPreCheck(GameObject car)
    {
        Debug.Log("Lane Switch Pre-Check::In-Progress::"+car.name);
        int laneNum = GetCurrentLane(car);

        // allow switch for valid lane
        if(laneNum == -1)
        {
            return false;
        }

        int otherLaneNum = 1 - laneNum;

        // check for sufficient spacing with lead car in current lane
        foreach (GameObject entry in this.leadCarTracker[laneNum].Keys)
        {
            if(entry == car){
                GameObject leadCar = this.leadCarTracker[laneNum][car][laneNum];
                if(leadCar == null) break;

                float currentDist = GetInterCarDistance(leadCar, car);           
                
                Debug.Log("Lane Switch Pre-Check::In-Progress::"+car.name+" "+leadCar.name+" "+currentDist);
                if(currentDist < 15.0f || GetCurrentCarState(leadCar) == SimulationSingleton.CarState.LANE_SWITCH)
                {
                    return false;
                }
            }
        }

        //check if another car is switching lanes in front or back in the other lane
        GameObject leadCarInAltLane = null;
        GameObject followCarInAltLane = null;
        leadCarInAltLane = GetLeadCarInAltLane(car);
        followCarInAltLane = GetFollowCarInAltLane(car);
        if((leadCarInAltLane != null && GetInterCarDistance(leadCarInAltLane, car) < 20.0f) ||
           // (followCarInAltLane != null && GetInterCarDistance(car, followCarInAltLane) < 20.0f)
           (followCarInAltLane != null && GetInterCarDistance(car, followCarInAltLane) < 
                                            GetBrakingDistance(followCarInAltLane.GetComponent<CarController>().GetVel))
           )
        {
            return false;
        }

        Debug.Log("Lane Switch Pre-Check::Passed::"+car.name);

        return true;
    }

    private GameObject GetLeadCarInAltLane(GameObject car)
    {
        int laneNum = GetCurrentLane(car);
        int otherLaneNum = 1 - laneNum;
        GameObject leadCarInAltLane = null;
        float minDist = GetRouteLength();

        foreach (GameObject currentObject in this.leadCarTracker[otherLaneNum].Keys)
        {
            float currentDist = GetInterCarDistance(currentObject, car);

            if(currentDist > 0f && currentDist < minDist)
            {
                leadCarInAltLane = currentObject;
                minDist = currentDist;
            }
        }

        if(leadCarInAltLane != null) Debug.Log("GetLeadCarInAltLane for "+car.name+" "+leadCarInAltLane.name);

        return leadCarInAltLane;        
    }

    private GameObject GetFollowCarInAltLane(GameObject car)
    {
        int laneNum = GetCurrentLane(car);
        int otherLaneNum = 1 - laneNum;
        GameObject followCarInAltLane = null;
        float minDist = GetRouteLength();

        foreach (GameObject currentObject in this.leadCarTracker[otherLaneNum].Keys)
        {
            float currentDist = GetInterCarDistance(car, currentObject);

            if(currentDist >= 0f && Mathf.Abs(currentDist) < minDist)
            {
                followCarInAltLane = currentObject;
                minDist = currentDist;
            }
        }

        return followCarInAltLane;        
    }

    private GameObject GetLeadCarInCurrentLane(GameObject car)
    {
        int laneNum = GetCurrentLane(car);
        GameObject leadCarInCurrentLane = null;
        float minDist = GetRouteLength();

        foreach(GameObject currentCar in this.leadCarTracker[laneNum].Keys)
        {
            if(currentCar != car)
            {
                float currentDist = GetInterCarDistance(currentCar, car);
                if(currentDist > 0f && currentDist < minDist)
                {
                    leadCarInCurrentLane = currentCar;
                    minDist = currentDist;
                }
            }
        }

        if(leadCarInCurrentLane != null) Debug.Log("GetLeadCarInCurrentLane for "+car.name+" "+leadCarInCurrentLane.name);
        return leadCarInCurrentLane;      
    }

    private SimulationSingleton.CarState GetCurrentCarState(GameObject car)
    {
        return car.GetComponent<AutomatedControl>().currentState;
    }

    private float GetInterCarDistance(GameObject car1, GameObject car2)
    {
        Vector3 leadPos = car1.transform.position;
        Vector3 followPos = car2.transform.position;

        return varyX ? leadPos.x - followPos.x : leadPos.z - followPos.z ;
    }

    private float GetRouteLength()
    {
        return varyX ? this.routeEndPosition.x - this.routeStartPosition.x : 
                       this.routeEndPosition.z - this.routeStartPosition.z;        
    }

    private void UpdateLeadCars(GameObject car)
    {
        int laneNum = GetCurrentLane(car);
        int altLaneNum = 1 - laneNum;
        GameObject leadCarInAltLane =  GetLeadCarInAltLane(car);
        GameObject leadCarInCurrentLane = GetLeadCarInCurrentLane(car);
        float maxDist = GetRouteLength();
        float distCurrentLane = leadCarInCurrentLane == null? maxDist : GetInterCarDistance(leadCarInCurrentLane, car); 
        float distAltLane = leadCarInAltLane == null ? maxDist : GetInterCarDistance(leadCarInAltLane, car);
        SimulationSingleton.CarState currentCarState = GetCurrentCarState(car);
        if(currentCarState == SimulationSingleton.CarState.STRAIGHT_DRIVE)
        {
            if(distCurrentLane < distAltLane)
            {
                // follow speed of lead car in current lane as it is closer

                this.leadCarTracker[laneNum][car][laneNum] = leadCarInCurrentLane;
                this.leadCarWeightTracker[laneNum][car][laneNum] = 1f;
                this.leadCarTracker[laneNum][car][altLaneNum] = null;
                this.leadCarWeightTracker[laneNum][car][altLaneNum] = 0f;
            }
            else if(leadCarInAltLane != null && 
                    GetCurrentCarState(leadCarInAltLane) == SimulationSingleton.CarState.LANE_SWITCH)
            {
                // follow speed of adjacent lane car if it is closer and it is switching lanes

                this.leadCarTracker[laneNum][car][laneNum] = null;
                this.leadCarWeightTracker[laneNum][car][laneNum] = 0f;
                this.leadCarTracker[laneNum][car][altLaneNum] = leadCarInAltLane;
                this.leadCarWeightTracker[laneNum][car][altLaneNum] = 1f;                        
            }
            else if(leadCarInCurrentLane != null)
            {
                // influenced solely by lead car in current lane

                this.leadCarTracker[laneNum][car][laneNum] = leadCarInCurrentLane;
                this.leadCarWeightTracker[laneNum][car][laneNum] = 1f;
                this.leadCarTracker[laneNum][car][altLaneNum] = null;
                this.leadCarWeightTracker[laneNum][car][altLaneNum] = 0f;                 
            }
            else
            {
                // no lead cars influencing

                this.leadCarTracker[laneNum][car][laneNum] = null;
                this.leadCarWeightTracker[laneNum][car][laneNum] = 0f;
                this.leadCarTracker[laneNum][car][altLaneNum] = null;
                this.leadCarWeightTracker[laneNum][car][altLaneNum] = 0f;                
            }
        }
        else if(currentCarState == SimulationSingleton.CarState.LANE_SWITCH)
        {
            float intermediatePosition = GetCarIntermediateFractionPosition(car);

            if(leadCarInCurrentLane != null)
            {
                // there is a lead car in current lane

                if(GetCurrentCarState(leadCarInCurrentLane) == SimulationSingleton.CarState.STRAIGHT_DRIVE)
                {
                    if(leadCarInAltLane != null)
                    {
                        // there is a potential lead car in the other lane as well

                        // if both lead car in alt lane and current car are lane switching simultaneously
                        if(GetCurrentCarState(leadCarInAltLane) == SimulationSingleton.CarState.LANE_SWITCH)
                        {
                            if(distAltLane < distCurrentLane)
                            {
                                // follow alt lane car if it is closer
                                this.leadCarTracker[laneNum][car][laneNum] = null;
                                this.leadCarTracker[laneNum][car][altLaneNum] = leadCarInAltLane;
                                this.leadCarWeightTracker[laneNum][car][laneNum] = 0f;
                                this.leadCarWeightTracker[laneNum][car][altLaneNum] = 1f;
                            }
                            else
                            {
                                //otherwise follow lead car in current lane
                                this.leadCarTracker[laneNum][car][laneNum] = leadCarInCurrentLane;
                                this.leadCarTracker[laneNum][car][altLaneNum] = null;
                                this.leadCarWeightTracker[laneNum][car][laneNum] = 1f;
                                this.leadCarWeightTracker[laneNum][car][altLaneNum] = 0f;
                            }
                        }
                        else
                        {
                            // weighted AOVRV w.r.t lead cars in both lanes if both are not switching lanes
                            this.leadCarTracker[laneNum][car][laneNum] = leadCarInCurrentLane;
                            this.leadCarWeightTracker[laneNum][car][laneNum] = intermediatePosition;
                            this.leadCarTracker[laneNum][car][altLaneNum] = leadCarInAltLane;
                            this.leadCarWeightTracker[laneNum][car][altLaneNum] = 1 - intermediatePosition;                             
                        }
                    }
                    else
                    {
                        // no lead car in other lane - weighted AOVRV with respect to lead car in current lane
                        this.leadCarTracker[laneNum][car][laneNum] = leadCarInCurrentLane;
                        this.leadCarTracker[laneNum][car][altLaneNum] = null;
                        this.leadCarWeightTracker[laneNum][car][laneNum] = intermediatePosition;
                        this.leadCarWeightTracker[laneNum][car][altLaneNum] = 1 - intermediatePosition;                        
                    }
                }
                else
                {
                    // both lead car and current car are lane switching simultaneously
                    // take action as per closer car ahead of current one
                    if(distCurrentLane < distAltLane)
                    {
                        this.leadCarTracker[laneNum][car][laneNum] = leadCarInCurrentLane;
                        this.leadCarTracker[laneNum][car][altLaneNum] = null;
                        this.leadCarWeightTracker[laneNum][car][laneNum] = 1f;
                        this.leadCarWeightTracker[laneNum][car][altLaneNum] = 0f;
                    }
                    else
                    {
                        this.leadCarTracker[laneNum][car][laneNum] = null;
                        this.leadCarTracker[laneNum][car][altLaneNum] = leadCarInAltLane;
                        this.leadCarWeightTracker[laneNum][car][laneNum] = 0f;
                        this.leadCarWeightTracker[laneNum][car][altLaneNum] = 1f;
                    }
                }
            }
            else
            {
                // there is no lead car in current lane

                if(leadCarInAltLane != null)
                {
                    // drive as per lead car in target lane

                    this.leadCarTracker[laneNum][car][laneNum] = null;
                    this.leadCarTracker[laneNum][car][altLaneNum] = leadCarInAltLane;
                    this.leadCarWeightTracker[laneNum][car][laneNum] = 0f;
                    this.leadCarWeightTracker[laneNum][car][altLaneNum] = 1f;                    
                }
                else
                {
                    // no lead cars in either lane

                    this.leadCarTracker[laneNum][car][laneNum] = null;
                    this.leadCarTracker[laneNum][car][altLaneNum] = null;
                    this.leadCarWeightTracker[laneNum][car][laneNum] = 0f;
                    this.leadCarWeightTracker[laneNum][car][altLaneNum] = 0f;                    
                }
            }                 
        }
    }

    public float GetBrakingDistance(float velocity)
    {
        return (float)Math.Pow(velocity, 2) / this.brakingFactor;
    }
}
