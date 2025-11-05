using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    
    public float walkSpeed = 6f;
    public float runSpeed = 12f;
    public float jumpPower = 7f;
    public float gravity = 10f;
    // Mouse/look removed per user request - movement only
    public float defaultHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchSpeed = 3f;

    private Vector3 moveDirection = Vector3.zero;
    
    private CharacterController characterController;
    private bool canMove = true;
    
    // Input System variables
    private Vector2 moveInput;
    private bool isJumping;
    private bool isRunning;
    private bool isCrouching;
    
    [Header("Input")]
    [Tooltip("Optional: assign a PlayerInput component so the script can read actions directly if callbacks aren't used")]
    public PlayerInput playerInput;

    // Expose velocity for TerrainManager predictive chunking
    public Vector3 GetVelocity()
    {
        return new Vector3(moveDirection.x, 0f, moveDirection.z);
    }

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        // Unlock cursor / show it since mouse look is disabled
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        isJumping = value.isPressed;
    }

    public void OnSprint(InputValue value)
    {
        isRunning = value.isPressed;
    }

    public void OnCrouch(InputValue value)
    {
        isCrouching = value.isPressed;
    }

    void Update()
    {
        // Read inputs from PlayerInput actions if available (fallbacks below)
    Vector2 move2 = Vector2.zero;
        bool jump = false;
        bool sprint = false;
        bool crouch = false;

        if (playerInput != null && playerInput.actions != null)
        {
            var ma = playerInput.actions.FindAction("Move");
            if (ma != null) move2 = ma.ReadValue<Vector2>();

            // Ignore Look action - mouse/look disabled

            var ja = playerInput.actions.FindAction("Jump");
            if (ja != null) jump = ja.triggered || ja.ReadValue<float>() > 0.5f;

            var sa = playerInput.actions.FindAction("Sprint") ?? playerInput.actions.FindAction("Run");
            if (sa != null) sprint = sa.ReadValue<float>() > 0.5f || sa.triggered;

            var ca = playerInput.actions.FindAction("Crouch");
            if (ca != null) crouch = ca.ReadValue<float>() > 0.5f || ca.triggered;
        }
        else
        {
            // Low-level Input System fallback
            if (Keyboard.current != null)
            {
                // Forward/Backward movement (Z axis)
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move2.y = 1f;
                else if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move2.y = -1f;
                else move2.y = 0f;

                // Left/Right movement (X axis)
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move2.x = 1f;
                else if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move2.x = -1f;
                else move2.x = 0f;

                jump = Keyboard.current.spaceKey.isPressed;
                sprint = Keyboard.current.leftShiftKey.isPressed;
                crouch = Keyboard.current.cKey.isPressed || Keyboard.current.rKey.isPressed;
            }
            // Low-level mouse look ignored
        }

        // Move relative to world axes (no camera influence)
        float currentSpeed = canMove ? (sprint ? runSpeed : walkSpeed) : 0;
        Vector3 move = (Vector3.forward * move2.y + Vector3.right * move2.x) * currentSpeed;

        // Preserve vertical velocity
        float movementDirectionY = moveDirection.y;
        moveDirection = move;

        if (jump && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpPower;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        if (crouch && canMove)
        {
            characterController.height = crouchHeight;
            walkSpeed = crouchSpeed;
            runSpeed = crouchSpeed;
        }
        else
        {
            characterController.height = defaultHeight;
            walkSpeed = 6f;
            runSpeed = 12f;
        }

        characterController.Move(moveDirection * Time.deltaTime);

        // Rotate player to face movement direction (smooth)
        float turnSpeed = 10f; // degrees per second smoothing factor
        Vector3 planarMove = new Vector3(move.x, 0f, move.z);
        if (planarMove.sqrMagnitude > 0.001f)
        {
            Quaternion target = Quaternion.LookRotation(planarMove.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
        }
    }
}