using UnityEngine.Pool;
using UnityEngine;


public class SpawnLeaf : MonoBehaviour
{
    public GameObject prefab;
    public Transform spawnPoint;
    public float interval = 1f;
    public bool Spawning = true;
    public Transform despawnBoundaries;
    private float timer = 0f;

    private void Update()
    {
        Spawner();
    }

    public void Spawner()
    {
        if (!Spawning) return;

        timer += Time.deltaTime;          // add the time since the last frame

        if (timer >= interval)
        {
            timer -= interval;            // reset, keeping any leftover time
            CreateLeaf(spawnPoint);
        }
    }
    public void CreateLeaf(Transform leafSpawnPoint)
    {
        Instantiate(prefab, leafSpawnPoint.position, leafSpawnPoint.rotation);
    }

    public void ChangeSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
    }


}
