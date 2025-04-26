using System.Collections.Generic;
using UnityEngine;

public class TrackCheckpoints : MonoBehaviour
{
    private static List<GameObject> goals = new List<GameObject>();
    public static List<GameObject> GetCheckpoints(Transform parent)
    {
        foreach (Transform child in parent)
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

    void Start()
    {
    }

}
