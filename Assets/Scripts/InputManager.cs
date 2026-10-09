using UnityEngine;

public class InputManager : MonoBehaviour
{
    
    private PlayerInput inputActions;
    private Vector2 moveInput;
    private bool isJumping;
    private bool isJumpHold;

    public Vector2 MoveInput => moveInput;
    public bool IsJumping => isJumping;
    public bool IsJumpHold => isJumpHold;


    private void Awake()
    {
        inputActions=new PlayerInput();
    }

    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }
    void Update()
    {
        moveInput=inputActions.Player.Move.ReadValue<Vector2>();
        isJumping = inputActions.Player.Jump.WasPerformedThisFrame();
        isJumpHold=inputActions.Player.Jump.IsPressed();

    }
}
