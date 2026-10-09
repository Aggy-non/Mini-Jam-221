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
    public int amountOfLeavesPerYLevel;
    private float platformXPosition;

    private void Update()
    {
        Timer();
    }

    public void Spawner()
    {
        if (!Spawning) return;
      
        timer += Time.deltaTime;
        if (timer >= interval)
        {
            timer -= interval;            
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

    public void Timer()
    {
        timer +=Time.deltaTime;

        if (timer >= interval)
        {
            timer -= interval;            
            SpawnLeaves(amountOfLeavesPerYLevel);
        }
    }

    public void SpawnLeaves(int numberOfLeaves)
    {
        for (int i = 0; i < numberOfLeaves; i++)
        {
            CalculateNewLeafPosition();
            CreateLeaf(spawnPoint);
        }
    }

    private void CalculateNewLeafPosition()
    {

    }

    public float GetRandomNumber(float min, float max)
    {
        return Random.Range(min, max);
    }
}
