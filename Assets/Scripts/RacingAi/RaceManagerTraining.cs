// RaceManagerTraining.cs
using System.Collections.Generic;
using UnityEngine;

public class RaceManagerTraining : MonoBehaviour
{

    private List<RacingAgent> agents = new List<RacingAgent>();
    private Dictionary<RacingAgent, int> progress = new Dictionary<RacingAgent, int>();
    private List<Vector3> startingPositions = new List<Vector3>();
    private Transform startFinish;
    private TrackCheckpoints trackCheckpoints;
    private int totalCheckpoints;

    private void Start()
    {
        trackCheckpoints = GetComponent<TrackCheckpoints>();
        totalCheckpoints = trackCheckpoints.GetCheckpoints().Count;

        startFinish = transform.Find("start_finish");
        foreach (Transform child in startFinish)
        {
            if (child.name == "startingPosition")
            {
                startingPositions.Add(child.position);
            }
        }
    }

    public void RegisterAgent(RacingAgent agent)
    {
        agents.Add(agent);
        progress[agent] = 0;
    }

    public int UpdateAgentProgress(RacingAgent agent, int lapCount, int nextCheckpoint)
    {
        int oldProg = progress[agent];
        int newProg = lapCount * totalCheckpoints + nextCheckpoint;
        progress[agent] = newProg;

        int overtakes = 0;
        foreach (var other in agents)
        {
            if (other == agent) continue;
            int otherProg = progress[other];
            if (oldProg <= otherProg && newProg > otherProg)
            {
                overtakes++;
            }
        }
        return overtakes;
    }

    public void ResetAllAgents()
    {
        foreach (var agent in agents)
        {
            progress[agent] = 0;
        }
        for (int i = 0; i < agents.Count; i++)
        {
            agents[i].ResetAgent(startingPositions[i], startFinish.rotation);
        }
    }
}
