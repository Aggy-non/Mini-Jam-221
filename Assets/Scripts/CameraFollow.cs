
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Player player;

    [Header("Camera Settings")]
    [SerializeField] private float followSpeed = 3f;
    [SerializeField] private float maxFollowSpeed = 10f;
    [SerializeField] private float speedIncrease = 20f;
    [SerializeField] private float speedIncreaseRate = 0.2f;

    private float highestCameraY;
    private float elapsedTime;
    private float startCameraY;
    private bool gameStarted;

    

    private void Start()
    {
        highestCameraY = transform.position.y;
        startCameraY = player.transform.position.y;
    }


    private void LateUpdate()
    {
        if (!gameStarted)
        {
            if (player.IsGrounded)
                return;

            gameStarted = true;
        }

        elapsedTime += Time.deltaTime;

        float currentSpeed = followSpeed;

        currentSpeed += Mathf.Floor(elapsedTime / speedIncrease)
                        * speedIncreaseRate;

        currentSpeed = Mathf.Min(currentSpeed, maxFollowSpeed);

        transform.position += Vector3.up * currentSpeed * Time.deltaTime;
    }


    

}
