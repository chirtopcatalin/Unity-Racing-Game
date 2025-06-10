using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class RaceManagerTraining : MonoBehaviour
{
    private List<RacingAgentTraining> agents = new List<RacingAgentTraining>();
    private Dictionary<RacingAgentTraining, int> agentProgress = new Dictionary<RacingAgentTraining, int>();

    private List<Transform> startingPositions = new List<Transform>();
    private Transform startFinish;
    private int totalCheckpoints;

    private void Start()
    {
        List<GameObject> checkpoints = GetCheckpoints();
        if (checkpoints != null)
        {
            totalCheckpoints = checkpoints.Count;
        }

        startFinish = transform.Find("start_finish");
        if (startFinish != null)
        {
            foreach (Transform child in startFinish)
            {
                if (child.name == "startingPosition")
                {
                    startingPositions.Add(child);
                }
            }
        }
    }

    public void RegisterAgent(RacingAgentTraining agent)
    {
        if (!agents.Contains(agent))
        {
            agents.Add(agent);
            agentProgress[agent] = 0;
        }
    }

    public int UpdateAgentProgress(RacingAgentTraining agent, int lapCount, int nextCheckpointIndex)
    {
        if (totalCheckpoints == 0) return 0;

        int oldProg = agentProgress.ContainsKey(agent) ? agentProgress[agent] : 0;
        int newProg = lapCount * totalCheckpoints + nextCheckpointIndex;
        agentProgress[agent] = newProg;

        int overtakes = 0;
        foreach (var otherAgent in agents)
        {
            if (otherAgent == agent) continue;

            if (agentProgress.TryGetValue(otherAgent, out int otherProgValue))
            {
                if (oldProg <= otherProgValue && newProg > otherProgValue)
                {
                    overtakes++;
                }
            }
        }
        return overtakes;
    }

    public int GetAgentPosition(RacingAgentTraining agent)
    {
        if (!agentProgress.ContainsKey(agent)) return agents.Count;

        int agentProg = agentProgress[agent];
        int position = 1;

        foreach (var kvp in agentProgress)
        {
            if (kvp.Key != agent && kvp.Value > agentProg)
            {
                position++;
            }
        }

        return position;
    }

    public int GetTotalAgents()
    {
        return agents.Count;
    }

    public void ResetAllAgents()
    {
        foreach (var agent in agents)
        {
            agentProgress[agent] = 0;
        }

        List<Transform> shuffledPositions = new List<Transform>(startingPositions);
        for (int i = 0; i < shuffledPositions.Count; i++)
        {
            Transform temp = shuffledPositions[i];
            int randomIndex = Random.Range(i, shuffledPositions.Count);
            shuffledPositions[i] = shuffledPositions[randomIndex];
            shuffledPositions[randomIndex] = temp;
        }

        for (int i = 0; i < agents.Count; i++)
        {
            if (i < shuffledPositions.Count)
            {
                agents[i].ResetAgent(shuffledPositions[i].position, shuffledPositions[i].rotation);
            }
        }
    }

    public List<GameObject> GetCheckpoints()
    {
        List<GameObject> goals = new List<GameObject>();
        foreach (Transform child in this.transform)
        {
            foreach (Transform grandchild in child)
            {
                if (grandchild.CompareTag("goal"))
                {
                    goals.Add(grandchild.gameObject);
                }
            }
        }
        return goals;
    }

    public List<RacingAgentTraining> GetAgentsByPosition()
    {
        return agents.OrderByDescending(agent => agentProgress.ContainsKey(agent) ? agentProgress[agent] : 0).ToList();
    }
}