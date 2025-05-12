using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class RaceManagerTraining : MonoBehaviour
{
    private List<RacingAgent> agents = new List<RacingAgent>();
    private List<Vector3> startingPositions = new List<Vector3>();
    private Transform startPosition;
    private Transform startFinish;
    private void Start()
    {
        startFinish = transform.Find("start_finish");


        foreach (Transform child in startFinish)
        {
            if (child.name == "startingPosition")
            {
                startingPositions.Add(child.position);
            }
        }

        foreach (var position in startingPositions)
        {
            Debug.Log("starting positions: " + position);
        }
    }
    public void RegisterAgent(RacingAgent agent)
    {
        agents.Add(agent);
    }

    public void ResetAllAgents()
    {
        for (int i = 0; i < agents.Count; i++)
        {
            agents[i].ResetAgent(startingPositions[i], startFinish.rotation);
        }
    }

}
