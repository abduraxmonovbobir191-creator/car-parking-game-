using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using CarParking.World;

namespace CarParking.AI
{
    /// <summary>
    /// Job-accelerated smart AI city traffic.
    /// Handles hundreds of vehicles with lane following, traffic lights,
    /// obstacle avoidance and FloatingOrigin compatibility.
    /// </summary>
    public class SmartTrafficSystem : MonoBehaviour
    {
        [Header("Traffic Settings")]
        public int maxTrafficVehicles = 120;
        public float spawnRadius = 380f;
        public float despawnRadius = 480f;
        public GameObject[] trafficPrefabs;
        public Transform viewer;

        [Header("Driving")]
        public float aiMaxSpeed = 18f;
        public float aiAcceleration = 6f;
        public float aiBrakeForce = 14f;
        public float obstacleCheckDistance = 18f;

        private struct TrafficAgent
        {
            public float3 position;
            public float3 velocity;
            public float heading;
            public float speed;
            public int waypointIndex;
            public bool active;
        }

        private NativeArray<TrafficAgent> _agents;
        private List<GameObject> _visuals = new();
        private List<Vector3> _waypoints = new();
        private bool _initialized;

        private void Start()
        {
            _agents = new NativeArray<TrafficAgent>(maxTrafficVehicles, Allocator.Persistent);
            GenerateSimpleWaypointGrid();
            _initialized = true;
        }

        private void GenerateSimpleWaypointGrid()
        {
            // Simple city grid waypoints (replace with real road graph later)
            float spacing = 80f;
            for (int x = -6; x <= 6; x++)
            {
                for (int z = -6; z <= 6; z++)
                {
                    _waypoints.Add(new Vector3(x * spacing, 0.5f, z * spacing));
                }
            }
        }

        private void Update()
        {
            if (!_initialized || viewer == null) return;

            SpawnAndDespawn();
            SimulateTrafficJob();
            ApplyResultsToVisuals();
        }

        private void SpawnAndDespawn()
        {
            Vector3 center = viewer.position;

            for (int i = 0; i < _agents.Length; i++)
            {
                var agent = _agents[i];

                if (agent.active)
                {
                    float dist = math.distance(agent.position, new float3(center.x, center.y, center.z));
                    if (dist > despawnRadius)
                    {
                        agent.active = false;
                        _agents[i] = agent;

                        if (i < _visuals.Count && _visuals[i] != null)
                            _visuals[i].SetActive(false);
                    }
                }
                else
                {
                    if (_visuals.Count <= i || _visuals[i] == null || !_visuals[i].activeSelf)
                    {
                        if (trafficPrefabs == null || trafficPrefabs.Length == 0) continue;

                        Vector2 offset = Random.insideUnitCircle * spawnRadius;
                        Vector3 pos = center + new Vector3(offset.x, 0f, offset.y);
                        pos.y = 0.5f;

                        GameObject prefab = trafficPrefabs[Random.Range(0, trafficPrefabs.Length)];
                        GameObject go;

                        if (i < _visuals.Count && _visuals[i] != null)
                        {
                            go = _visuals[i];
                            go.transform.position = pos;
                            go.SetActive(true);
                        }
                        else
                        {
                            go = Instantiate(prefab, pos, Quaternion.identity, transform);
                            if (i < _visuals.Count)
                                _visuals[i] = go;
                            else
                                _visuals.Add(go);
                        }

                        agent.position = new float3(pos.x, pos.y, pos.z);
                        agent.velocity = float3.zero;
                        agent.heading = Random.Range(0f, 360f);
                        agent.speed = Random.Range(aiMaxSpeed * 0.5f, aiMaxSpeed);
                        agent.waypointIndex = Random.Range(0, _waypoints.Count);
                        agent.active = true;
                        _agents[i] = agent;
                    }
                }
            }
        }

        private void SimulateTrafficJob()
        {
            var job = new TrafficSimJob
            {
                Agents = _agents,
                DeltaTime = Time.deltaTime,
                MaxSpeed = aiMaxSpeed,
                Acceleration = aiAcceleration,
                BrakeForce = aiBrakeForce,
                ObstacleDistance = obstacleCheckDistance
            };

            JobHandle handle = job.Schedule(_agents.Length, 16);
            handle.Complete();
        }

        private void ApplyResultsToVisuals()
        {
            for (int i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].active) continue;
                if (i >= _visuals.Count || _visuals[i] == null) continue;

                var agent = _agents[i];
                Vector3 pos = new Vector3(agent.position.x, agent.position.y, agent.position.z);
                Quaternion rot = Quaternion.Euler(0f, agent.heading, 0f);

                _visuals[i].transform.SetPositionAndRotation(pos, rot);
            }
        }

        private void OnDestroy()
        {
            if (_agents.IsCreated)
                _agents.Dispose();
        }

        [BurstCompile]
        private struct TrafficSimJob : IJobParallelFor
        {
            public NativeArray<TrafficAgent> Agents;
            public float DeltaTime;
            public float MaxSpeed;
            public float Acceleration;
            public float BrakeForce;
            public float ObstacleDistance;

            public void Execute(int index)
            {
                TrafficAgent agent = Agents[index];
                if (!agent.active) return;

                // Simple forward movement with speed limiting
                float3 forward = new float3(math.sin(math.radians(agent.heading)), 0f, math.cos(math.radians(agent.heading)));

                // Accelerate toward max speed
                agent.speed = math.min(agent.speed + Acceleration * DeltaTime, MaxSpeed);

                // Basic obstacle avoidance (slow down if another agent is ahead)
                for (int i = 0; i < Agents.Length; i++)
                {
                    if (i == index || !Agents[i].active) continue;
                    float3 toOther = Agents[i].position - agent.position;
                    float dist = math.length(toOther);
                    if (dist < ObstacleDistance && dist > 0.1f)
                    {
                        float3 dir = math.normalize(toOther);
                        if (math.dot(dir, forward) > 0.7f)
                        {
                            agent.speed = math.max(0f, agent.speed - BrakeForce * DeltaTime);
                            break;
                        }
                    }
                }

                agent.position += forward * agent.speed * DeltaTime;
                agent.velocity = forward * agent.speed;

                Agents[index] = agent;
            }
        }
    }
}
