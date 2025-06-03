using System.Collections.Generic;
using UnityEngine;

public class CarCheckpointTracker : MonoBehaviour
{
    private TrackCheckpoints trackCheckpoints;
    private  List<GameObject> orderedGoals;

    public int nextCheckpoint = 0;
    public int lapCount = 0;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("goal")) return;

        var hitGoal = other.gameObject;
        if (hitGoal == orderedGoals[nextCheckpoint])
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
        else
        {
            //Debug.Log($"{name} hit WRONG checkpoint. Expected {orderedGoals[nextCheckpoint].name}.");
        }
    }
    private void Start()
    {
        trackCheckpoints = transform.parent.gameObject.GetComponent<TrackCheckpoints>();
        orderedGoals = trackCheckpoints.GetCheckpoints();
    }
}
