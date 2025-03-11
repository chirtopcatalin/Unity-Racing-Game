using UnityEngine;

public class CollisionDetector : MonoBehaviour
{
    public int goalReached = 0;
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("goal"))
        {
            goalReached = 1;
        }
    }
}
