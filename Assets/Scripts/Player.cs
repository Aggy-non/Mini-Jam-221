using UnityEngine;

public class Player : MonoBehaviour
{
    private InputManager inputManager;
    private Rigidbody2D rb;

    [Header("Player Movement")]
    [SerializeField] private float moveSpeed = 5f;
    

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private float jumpCutMultiplier = 0.9f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask groundLayer;

    private bool isGrounded;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        inputManager = GetComponent<InputManager>();
    }

    private void Update()
    {
        CheckGround();
        Jump();
    }

    private void FixedUpdate()
    {
        Move();
    }

    private void CheckGround()
    {
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    private void Move()
    {
        float targetSpeed = inputManager.MoveInput.x * moveSpeed;

        rb.linearVelocity = new Vector2(targetSpeed * moveSpeed, rb.linearVelocity.y);
    }

    private void Jump()
    {
       
        if (isGrounded && inputManager.JumpPressed)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
        }

        
        if (inputManager.JumpReleased && rb.linearVelocity.y > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.75f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}