using UnityEngine;
using UnityEditor;

namespace Utils{
    public sealed class SimulationSingleton
    {
        private static SimulationSingleton instance = null;
        private static readonly object padlock = new object();

        private SimulationSingleton()
        {
        }

        public static SimulationSingleton Instance
        {
            get
            {
                lock (padlock)
                {
                    if (instance == null)
                    {
                        instance = new SimulationSingleton();
                    }
                    return instance;
                }
            }
        }

        // Prefab names
        public static string LEAD_CAR_PREFAB = "Assets/Prefabs/yellow-racing-car.prefab";

        // Constants for Aerodynamics
        public static float FRONTAL_AREA = 0.7f;
        public static float DRAG_COEFFICENT = 0.30f;
        public static float CL_FRONT = -0.05f;
        public static float CL_REAR = -0.05f;

        public const float AIR_DENSITY = 1.292f;

        // Constants for brakes

        public static float BRAKE_MAX_DECELERATION = 0.9f;

        public static float BRAKE_FRONT_BIAS = 0.6f;

        public static float BRAKE_TORQUE_MIN_THRESHOLD = 0.05f;

        // Constants for car controller

        public static float CAR_MASS = 1200.0f;

        public static Vector3 CENTRE_OF_GRAVITY = new Vector3(0.0f, 0.6f, -2.5f);

        public static Vector3 INERTIA_TENSOR = new Vector3(3600.0f, 3900.0f, 800.0f);

        public static float DELTA_TIME = 0.008333f;

        // Constants for Engine
        public static float ENGINE_RPM_IDLE = 800.0f;
        public static float ENGINE_POWER_IDLE = 10.0f;
        public static float ENGINE_RPM_LOW_POWER_BAND = 2500.0f;
        public static float ENGINE_POWER_LOW_POWER_BAND  = 50.0f;
        public static float ENGINE_RPM_MAX_POWER = 5500.0f;
        public static float ENGINE_POWER_MAX_POWER = 120.0f;
        public static float ENGINE_RPM_MAX = 6000.0f;
        public static float ENGINE_POWER_MAX_RPM = 100.0f;
        public static float POWER_TORQUE_CONVERSION_FACTOR = 7120.54f;

        // enum to manage car state
        public enum CarState{
            IDLE,
            STRAIGHT_DRIVE,
            LANE_SWITCH
        }
    }
}
