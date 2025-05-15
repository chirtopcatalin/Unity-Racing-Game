using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine.InputSystem.XR;
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

    public override void Initialize()
    {
        lapCount = 0;
        carControllerAgent = GetComponent<CarControllerAgent>();
        trackCheckpoints = transform.parent.GetComponent<TrackCheckpoints>();
        orderedGoals = trackCheckpoints.GetCheckpoints();
        rb = GetComponent<Rigidbody>();
        raceManager = transform.parent.GetComponent<RaceManagerTraining>();
        raceManager.RegisterAgent(this);
    }

    public override void OnEpisodeBegin()
    {
        orderedGoals = trackCheckpoints.GetCheckpoints();
        raceManager.ResetAllAgents();
    }

    public void ResetAgent(Vector3 startingPosition, Quaternion startingRotation)
    {
        rb.angularVelocity = Vector3.zero;
        rb.linearVelocity = Vector3.zero;
        transform.position= startingPosition;
        transform.rotation = startingRotation * Quaternion.Euler(0, 180f, 0);
        nextCheckpoint = 0;
        lapCount = 0;
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        AddReward(-0.001f);

        carControllerAgent.GetInput(
            actions.DiscreteActions[0],
            actions.DiscreteActions[1]
        );
    }
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var agentActions = actionsOut.DiscreteActions;
        if (acceleration.action.ReadValue<float>() > 0)
        {
            agentActions[0] = 2;
        }
        else if (acceleration.action.ReadValue<float>() < 0)
        {
            agentActions[0] = 0;
        }
        else
        {
            agentActions[0] = 1;
        }

        if (steering.action.ReadValue<float>() > 0)
        {
            agentActions[1] = 2;
        }
        else if (steering.action.ReadValue<float>() < 0)
        {
            agentActions[1] = 0;
        }
        else
        {
            agentActions[1] = 1;
        }
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
        sensor.AddObservation(toCheckpointLocal);
        sensor.AddObservation(orderedGoals[checkpointIndexToObserve].transform.localPosition);

        sensor.AddObservation(rb.linearVelocity);

        //Vector3 velLocal   = transform.InverseTransformDirection(rb.velocity);
        //float   forwardVel = velLocal.z;
        //float   lateralVel = Mathf.Abs(velLocal.x);

        //AddReward( 0.001f * forwardVel);  
        //AddReward(-0.0005f * lateralVel);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("goal"))
        {
            var hitGoal = other.gameObject;
            if (hitGoal == orderedGoals[nextCheckpoint])
            {
                AddReward(+0.1f);
                nextCheckpoint++;
                Debug.Log($"{name} hit checkpoint #{nextCheckpoint}");

                if (nextCheckpoint >= orderedGoals.Count)
                {
                    AddReward(+1.0f);
                    lapCount++;
                    Debug.Log($"{name} completed lap {lapCount}!");
                    //if (Academy.Instance.IsCommunicatorOn)
                        //EndEpisode();
                    nextCheckpoint = 0;
                }
                if(lapCount == 3)
                {
                    AddReward(+2.0f);
                    EndEpisode();
                }
            }
            else
            {
                AddReward(-0.2f);
                //Debug.Log($"{name} hit WRONG checkpoint. Expected {orderedGoals[nextCheckpoint].name}.");
            }
        }
        else if (other.CompareTag("wall"))
        {
            //if (Academy.Instance.IsCommunicatorOn)
            //{
                AddReward(-4f);
                EndEpisode();
            //}
            //Debug.Log($"{name} hit a wall.");
        }
        else if (other.CompareTag("car"))
        {
            AddReward(-0.5f);
        }
    }
}
