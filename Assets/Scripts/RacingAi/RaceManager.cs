using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class RaceManager : MonoBehaviour
{
    private List<RacingAgent> agents = new List<RacingAgent>();
    private Dictionary<RacingAgent, int> agentProgress = new Dictionary<RacingAgent, int>();

    public CarCheckpointTracker playerCheckpointTracker;
    public TMP_Text countdownText;

    private List<Vector3> startingPositions = new List<Vector3>();
    private Transform startFinish;
    private int totalCheckpoints;

    public GameObject raceFinishedPanel;
    public TMP_Text raceFinishedMessageText;
    public Button mainMenuButton;
    private bool playerHasFinishedRace = false;

    private bool isCountdownActive = true;
    private float countdownTimer = 5f;

    public class CarPositionInfo
    {
        public string Name { get; set; }
        public int LapCount { get; set; }
        public int NextCheckpoint { get; set; }
        public float DistanceToNextCheckpoint { get; set; }
        public int OverallProgress { get; set; }

        public CarPositionInfo(string name, int laps, int checkpoint, float distance, int totalCheckpointsInLap)
        {
            Name = name;
            LapCount = laps;
            NextCheckpoint = checkpoint;
            DistanceToNextCheckpoint = distance;
            OverallProgress = laps * totalCheckpointsInLap + checkpoint;
        }
    }

    private void Start()
    {
        raceFinishedPanel.SetActive(false);
        mainMenuButton.gameObject.SetActive(false);
        raceFinishedMessageText.text = string.Empty;

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
                    startingPositions.Add(child.position);
                }
            }
        }
        else
        {
            Debug.LogError("start_finish object not found");
        }
    }

    private void Update()
    {
        if (isCountdownActive)
        {
            countdownTimer -= Time.deltaTime;

            if (countdownText != null)
            {
                int secondsRemaining = Mathf.CeilToInt(countdownTimer);
                countdownText.text = secondsRemaining.ToString();
            }

            if (countdownTimer <= 0f)
            {
                isCountdownActive = false;
                if (countdownText != null)
                {
                    countdownText.gameObject.SetActive(false);
                }
            }
        }

        if (!playerHasFinishedRace && playerCheckpointTracker != null && playerCheckpointTracker.lapCount >= 3)
        {
            playerHasFinishedRace = true;
            ShowRaceFinishedScreen();
        }
    }

    void ShowRaceFinishedScreen()
    {
        if (raceFinishedPanel != null && raceFinishedMessageText != null)
        {
            int playerRank = GetPlayerRank();
            raceFinishedMessageText.text = $"Race Finished!\nYour Place: {playerRank}";
            raceFinishedPanel.SetActive(true);
            mainMenuButton.gameObject.SetActive(true);

            Time.timeScale = 0f;
            if (playerCheckpointTracker != null && playerCheckpointTracker.GetComponent<CarController>() != null)
            {
                playerCheckpointTracker.GetComponent<CarController>().enabled = false;
            }
        }
    }

    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public bool IsCountdownActive()
    {
        return isCountdownActive;
    }

    public void RegisterAgent(RacingAgent agent)
    {
        if (!agents.Contains(agent))
        {
            agents.Add(agent);
            agentProgress[agent] = 0;
        }
    }

    public int UpdateAgentProgress(RacingAgent agent, int lapCount, int nextCheckpointIndex)
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

    // for training
    public void ResetAllAgents()
    {
        foreach (var agent in agents)
        {
            agentProgress[agent] = 0;
        }
        for (int i = 0; i < agents.Count; i++)
        {
            if (i < startingPositions.Count)
            {
                agents[i].ResetAgent(startingPositions[i], startFinish.rotation);
            }
        }
    }

    public void ResetAgent(RacingAgent agent)
    {
        if (agents.Contains(agent))
        {
            int index = agents.IndexOf(agent);
            Vector3 startPos = startingPositions[index];
            Quaternion startRot = startFinish.rotation;
            agent.ResetAgent(startPos, startRot);
            agentProgress[agent] = 0;
        }
        else
        {
            Debug.LogWarning($"Agent {agent.name} not registered in RaceManager.");
        }
    }

    public List<CarPositionInfo> GetCurrentPositions()
    {
        if (totalCheckpoints == 0)
        {
            Debug.LogWarning("totalCheckpoints == 0");
            return new List<CarPositionInfo>();
        }

        var standings = new List<CarPositionInfo>();

        foreach (var agent in agents)
        {
            if (agent == null) continue;

            int agentLaps = agent.GetCurrentLap();
            int agentNextCheckpoint = agent.GetCurrentCheckpoint();
            float agentDistance = GetDistanceToNextCheckpoint(agent.transform, agentNextCheckpoint);

            standings.Add(new CarPositionInfo(agent.name, agentLaps, agentNextCheckpoint, agentDistance, totalCheckpoints));
        }

        if (playerCheckpointTracker != null)
        {
            float playerDistance = GetDistanceToNextCheckpoint(playerCheckpointTracker.transform, playerCheckpointTracker.nextCheckpoint);
            standings.Add(new CarPositionInfo("Player", playerCheckpointTracker.lapCount, playerCheckpointTracker.nextCheckpoint, playerDistance, totalCheckpoints));
        }

        standings = standings.OrderByDescending(s => s.OverallProgress)
                             .ThenBy(s => s.DistanceToNextCheckpoint)
                             .ToList();

        return standings;
    }

    private float GetDistanceToNextCheckpoint(Transform participantTransform, int checkpointIndex)
    {
        if (GetCheckpoints() == null || GetCheckpoints().Count == 0) return 9999f;
        List<GameObject> checkpoints = GetCheckpoints();

        return Vector3.Distance(participantTransform.position, checkpoints[checkpointIndex].transform.position);
    }

    public int GetPlayerRank()
    {
        if (playerCheckpointTracker == null)
        {
            return -1;
        }

        var standings = GetCurrentPositions();
        for (int i = 0; i < standings.Count; i++)
        {
            if (standings[i].Name == "Player")
            {
                return i + 1;
            }
        }
        return -1;
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

    public int GetAgentPosition(RacingAgent agent)
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
        return agents.Count + 1;
    }
}