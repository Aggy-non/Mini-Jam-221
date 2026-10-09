using UnityEngine;

public class LeafCatcher : MonoBehaviour
{
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Leaf"))
        {
            Destroy(other.gameObject);
        }
    }
}
