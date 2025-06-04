using System.Collections.Generic;
using UnityEngine;

public class CarCheckpointTracker : MonoBehaviour
{
    public TrackCheckpoints trackCheckpoints;

    private List<GameObject> orderedGoals;

    public int nextCheckpoint = 0;
    public int lapCount = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (orderedGoals == null || orderedGoals.Count == 0) return;

        if (!other.CompareTag("goal")) return;

        var hitGoal = other.gameObject;
        if (nextCheckpoint < orderedGoals.Count && hitGoal == orderedGoals[nextCheckpoint])
        {
            nextCheckpoint++;
            //Debug.Log($"{name} hit checkpoint #{nextCheckpoint}");

            if (nextCheckpoint >= orderedGoals.Count)
            {
                lapCount++;
                //Debug.Log($"{name} completed lap {lapCount}!");
                nextCheckpoint = 0;
            }
        }
    }

    private void Start()
    {
        if (trackCheckpoints != null)
        {
            orderedGoals = trackCheckpoints.GetCheckpoints();
            if (orderedGoals == null || orderedGoals.Count == 0)
            {
                Debug.LogError($"no checkpoint foud.");
            }
        }
        else
        {
            Debug.LogError($"trackCheckpointsReference not assigned.");
        }
    }

    public void ResetProgress()
    {
        lapCount = 0;
        nextCheckpoint = 0;
    }
}