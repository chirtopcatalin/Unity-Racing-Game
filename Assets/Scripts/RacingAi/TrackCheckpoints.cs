using System.Collections.Generic;
using UnityEngine;

public class TrackCheckpoints : MonoBehaviour
{
    public  List<GameObject> GetCheckpoints()
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

    void Start()
    {
    }

}
