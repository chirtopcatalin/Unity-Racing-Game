using System.Collections.Generic;
using UnityEngine;

public class CarCheckpointTracker : MonoBehaviour
{
    [Tooltip("Filled by TrackCheckpoints in Start()")]
    private TrackCheckpoints trackCheckpoints;
    private  List<GameObject> orderedGoals;

    // Which checkpoint the car is aiming for next
    public int nextCheckpoint = 0;
    // How many laps the car has completed
    public int lapCount = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("goal")) return;

        var hitGoal = other.gameObject;

        // Did we hit the “correct” next checkpoint?
        if (hitGoal == orderedGoals[nextCheckpoint])
        {
            nextCheckpoint++;
            Debug.Log($"{name} hit checkpoint #{nextCheckpoint}");

            // If we've just passed the last checkpoint in the list...
            Debug.Log("HAI SALUTARE " + orderedGoals.Count);
            if (nextCheckpoint >= orderedGoals.Count)
            {
                lapCount++;
                Debug.Log($"{name} completed lap {lapCount}!");
                nextCheckpoint = 0;
            }
        }
        else
        {
            // Wrong checkpoint out of order
            Debug.Log($"{name} hit WRONG checkpoint. Expected {orderedGoals[nextCheckpoint].name}.");
        }
    }
    private void Start()
    {
        trackCheckpoints = transform.parent.gameObject.GetComponent<TrackCheckpoints>();
        orderedGoals = TrackCheckpoints.GetCheckpoints(trackCheckpoints.transform);
        Debug.Log("number of checkpoints on the map: " + orderedGoals.Count);
    }
}
