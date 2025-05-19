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
                AddReward(-1.0f);
                EndEpisode();
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
        raceManager.ResetAllAgents();
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.collider.CompareTag("car"))
        {
            //Debug.Log("collision stay");
            AddReward(-0.001f);
        }
    }

    public void ResetAgent(Vector3 startingPosition, Quaternion startingRotation)
    {
        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
        transform.position = startingPosition;
        // face the right way
        transform.rotation = startingRotation * Quaternion.Euler(0, 180f, 0);
        nextCheckpoint = 0;
        lapCount = 0;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Time penalty
        AddReward(-0.0001f);

        // Drive input
        carControllerAgent.GetInput(
            actions.DiscreteActions[0],
            actions.DiscreteActions[1]
        );

        Vector3 velLocal = transform.InverseTransformDirection(rb.linearVelocity);
        float forwardVel = velLocal.z;
        AddReward(0.00002f * forwardVel);
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

        sensor.AddObservation(rb.linearVelocity);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("car"))
        {
            //Debug.Log("Collision with another car!");
            AddReward(-0.2f);
        }
        if (collision.collider.CompareTag("wall"))
        {
            AddReward(-0.2f);
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("goal"))
        {
            var hitGoal = other.gameObject;
            if (hitGoal == orderedGoals[nextCheckpoint])
            {
                AddReward(+1.0f / orderedGoals.Count);

                int oldNext = nextCheckpoint;
                int oldLap = lapCount;

                nextCheckpoint++;
                if (nextCheckpoint >= orderedGoals.Count)
                {
                    // completed a lap
                    lapCount++;
                    nextCheckpoint = 0;

                    AddReward(+0.5f);

                    if (lapCount >= 3)
                    {
                        Debug.Log("successful episode(3 laps)");
                        EndEpisode();
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
                AddReward(-0.5f);
            }
        }
    }
}
