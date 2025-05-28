// RacingAgent.cs
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem;

public class RacingAgent : Agent
{
    private TrackCheckpoints trackCheckpoints;
    private List<GameObject> orderedGoals;
    private CarControllerAgent carControllerAgent;
    private Rigidbody rb;
    private RaceManagerTraining raceManager;

    private int nextCheckpoint = 0;
    private int lapCount = 0;

    [Header("Input Actions")]
    public InputActionReference acceleration;
    public InputActionReference steering;

    [Header("Raycast Settings")]
    [SerializeField] private int numRays = 16;
    [SerializeField] private float rayMaxDistance = 25f;
    [SerializeField] private float rayHeightOffset = 0.5f;


    private Vector3 _lastPosition;
    private float _stuckTimer;
    public float stuckTimeout = 8f;
    public float movementThreshold = 0.5f;

    public override void Initialize()
    {
        _lastPosition = transform.position;
        _stuckTimer = 0f;
        carControllerAgent = GetComponent<CarControllerAgent>();
        trackCheckpoints = transform.parent.GetComponent<TrackCheckpoints>();
        orderedGoals = trackCheckpoints.GetCheckpoints();
        rb = GetComponent<Rigidbody>();

        raceManager = transform.parent.GetComponent<RaceManagerTraining>();
        raceManager.RegisterAgent(this);
    }

    private void FixedUpdate()
    {
        float dist = Vector3.Distance(transform.position, _lastPosition);
        if (dist < movementThreshold * Time.fixedDeltaTime)
        {
            _stuckTimer += Time.fixedDeltaTime;
            if (_stuckTimer >= stuckTimeout)
            {
                AddReward(-10.0f);
                raceManager.ResetAllAgents();
            }
        }
        else
        {
            _stuckTimer = 0f;
        }
        _lastPosition = transform.position;
    }

    public override void OnEpisodeBegin()
    {
        Debug.Log("new episode");
        _lastPosition = transform.position;
        _stuckTimer = 0f;
        orderedGoals = trackCheckpoints.GetCheckpoints();
        lapCount = 0;
        nextCheckpoint = 0;
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.collider.CompareTag("car"))
        {
            //Debug.Log("collision stay");
            AddReward(-0.04f);
        }
    }

    public void ResetAgent(Vector3 startingPosition, Quaternion startingRotation)
    {
        EndEpisode();
        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
        transform.position = startingPosition;
        transform.rotation = startingRotation * Quaternion.Euler(0, 180f, 0);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Time penalty
        AddReward(-0.0015f);

        // Drive input
        carControllerAgent.GetInput(
            actions.DiscreteActions[0],
            actions.DiscreteActions[1]
        );

        Vector3 velLocal = transform.InverseTransformDirection(rb.linearVelocity);
        float forwardVel = velLocal.z;

        float speedReward = 0.0006f;
        //if (forwardVel > 25.0f)
        //{
        //    speedReward = 0.0003f;
        //}
        //else if (forwardVel > 5.0f)
        //{
        //    speedReward = 0.0002f;
        //}
        AddReward(speedReward * forwardVel);

    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var d = actionsOut.DiscreteActions;
        float accel = acceleration.action.ReadValue<float>();
        d[0] = accel > 0 ? 2 : accel < 0 ? 0 : 1;
        float steer = steering.action.ReadValue<float>();
        d[1] = steer > 0 ? 2 : steer < 0 ? 0 : 1;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (orderedGoals == null || orderedGoals.Count == 0)
        {
            Debug.LogWarning($"{name}: orderedGoals is null or empty");
            return;
        }

        int checkpointIndexToObserve = nextCheckpoint;

        if (checkpointIndexToObserve >= orderedGoals.Count)
        {
            checkpointIndexToObserve = 0;
        }

        Vector3 toCheckpointWorld = orderedGoals[checkpointIndexToObserve].transform.position - transform.position;
        Vector3 toCheckpointLocal = transform.InverseTransformDirection(toCheckpointWorld);
        sensor.AddObservation(toCheckpointLocal.normalized);

        Vector3 toCheckpointDir = toCheckpointWorld.normalized;
        float alignment = Vector3.Dot(transform.forward, toCheckpointDir);
        sensor.AddObservation(alignment);

        sensor.AddObservation(rb.linearVelocity);

        var origin = transform.position + Vector3.up * rayHeightOffset;
        float angleStep = 360f / numRays;

        for (int i = 0; i < numRays; i++)
        {
            float angle = i * angleStep;
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * transform.forward;
            Debug.DrawRay(origin, dir * rayMaxDistance, Color.red);
            // cast and sort hits by distance
            var hits = Physics.RaycastAll(origin, dir, rayMaxDistance);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                // take first two hits (or fill with defaults)
                for (int h = 0; h < 3; h++)
                {
                    if (h < hits.Length)
                    {
                    float tagId = hits[h].collider.CompareTag("wall") ? 1f :
                                      hits[h].collider.CompareTag("car") ? 2f :
                                      hits[h].collider.CompareTag("goal") ? 3f :
                                      0f;

                        float distNorm = hits[h].distance / rayMaxDistance;

                        sensor.AddObservation(tagId);
                        sensor.AddObservation(distNorm);
                    }
                    else
                    {
                        sensor.AddObservation(0f);
                        sensor.AddObservation(1f);
                    }
                }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("car"))
        {
            //Debug.Log("Collision with another car!");
            AddReward(-3.0f);
        }
        if (collision.collider.CompareTag("wall"))
        {
            AddReward(-3.0f);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("goal"))
        {
            var hitGoal = other.gameObject;
            if (hitGoal == orderedGoals[nextCheckpoint])
            {
                AddReward(+10.0f / orderedGoals.Count);

                int oldNext = nextCheckpoint;
                int oldLap = lapCount;

                nextCheckpoint++;
                if (nextCheckpoint >= orderedGoals.Count)
                {
                    // completed a lap
                    lapCount++;
                    nextCheckpoint = 0;

                    if (lapCount >= 3)
                    {
                        Debug.Log("successful episode(3 laps)");
                        raceManager.ResetAllAgents();
                    }
                }

                int overtaken = raceManager.UpdateAgentProgress(this, lapCount, nextCheckpoint);
                if (overtaken > 0)
                {
                    AddReward(0.05f);
                    //Debug.Log($"{name} overtook {overtaken} car(s)!");
                }
            }
            else
            {
                // Wrong checkpoint
                Debug.Log("wrong checkpoint!");
                AddReward(-5.0f);
            }
        }
    }
}