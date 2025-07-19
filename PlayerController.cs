using UnityEngine;


public class PlayerController : MonoBehaviour
{
    // Movement Parameters
    public float sprintSpeed;
    public float runSpeed;

    private float moveSpeed;
    private Vector3 move;
    private CharacterController controller;
    private Vector3 moveDir;

    // Camera Parameters
    public Transform cameraTrans;
    private float smoothTurnVelocity;
    public float smoothTurnAngle = 0.02f;

    // Animator Parameters
    private Animator anim;

    // Jump Parameters
    public Transform groundPoint;
    public LayerMask ground;
    public float jumpForce;
    private float verticalVelocity;
    public float gravityModifier;
    private bool isGrounded;
    private bool canDoubleJump;

    // Crouch Parameters
    private bool isCrouching = false;

    // Wall Run Parameters
    public LayerMask wallLayer;
    public float wallRunForce;
    private bool isWallRight, isWallLeft;
    private bool isWallRunning;
    public float horizontalRayCastDistance;
    public Transform raycastPoint;
    private Vector3 wallRunDirection;

    
    // Final Movement Vector
    private Vector3 finalMovement;
    



    void Start()
    {
        controller = GetComponent<CharacterController>();
        anim = GetComponent<Animator>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    // Update is called once per frame
    void Update()
    {
        WallRun();
        PlayerMovement();
        Jump();
        CrouchOrSlide(); 
        
        // Single movement call
        ApplyMovement();
    }

    void PlayerMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        move = new Vector3(horizontal, 0f, vertical);
        move.Normalize();

        if (Input.GetKey(KeyCode.LeftShift))
        {
            moveSpeed = sprintSpeed;
            anim.SetBool("Sprint", true);
        }
        else
        {
            moveSpeed = runSpeed;
            anim.SetBool("Sprint", false);
        }

        // Make player moves towrds camera direction
        if(move.magnitude >= 0.1f){
            float angleInWorld = Mathf.Atan2(move.x, move.z) * Mathf.Rad2Deg + cameraTrans.eulerAngles.y;
            float targetAngle = Mathf.SmoothDampAngle(transform.eulerAngles.y, angleInWorld, ref smoothTurnVelocity, smoothTurnAngle);
            transform.rotation = Quaternion.Euler(0f, targetAngle, 0f);
            moveDir = Quaternion.Euler(0f, angleInWorld, 0f) * Vector3.forward;
        } else {
            moveDir = Vector3.zero;
        }

        // Store horizontal movement for final movement calculation
        finalMovement = moveDir * moveSpeed * Time.deltaTime;
        anim.SetFloat("moveSpeed", move.magnitude);
    }

    void Jump()
    {
        isGrounded = Physics.OverlapSphere(groundPoint.position, .2f, ground).Length > 0;
        anim.SetBool("isGrounded", isGrounded);
        if (isGrounded && !isWallRunning)
        {
            canDoubleJump = false;
            if (Input.GetKeyDown(KeyCode.Space))
            {
                verticalVelocity = jumpForce;
                canDoubleJump = true;
            }
        }
        else if (!isGrounded && canDoubleJump && Input.GetKeyDown(KeyCode.Space) && !isWallRunning)
        {
            // Double jump when in air
            verticalVelocity = jumpForce;
            canDoubleJump = false;

        }
        else if (!isWallRunning)
        {
                verticalVelocity += Physics.gravity.y * gravityModifier * Time.deltaTime;
        }
        anim.SetBool("canDoubleJump", canDoubleJump);
        // Add vertical movement to final movement
        finalMovement.y = verticalVelocity * Time.deltaTime;
    }

    void CrouchOrSlide()
    {
        if (Input.GetKey(KeyCode.C))
        {
            isCrouching = true;
        }
        else if (isGrounded && isCrouching && Input.GetKeyDown(KeyCode.Space))
        {
            verticalVelocity = 0f;
            jumpForce = 0f;
        }
        else
        {
            isCrouching = false;
        }
        anim.SetBool("isCrouching", isCrouching);
    }


    void WallRun()
    {
        isWallRight = Physics.Raycast(raycastPoint.position, transform.right, horizontalRayCastDistance, wallLayer);
        isWallLeft = Physics.Raycast(raycastPoint.position, -transform.right, horizontalRayCastDistance, wallLayer);

        wallRunDirection = transform.forward;

        if (isWallRight && !isGrounded && Input.GetKey(KeyCode.W))
        {
            isWallRunning = true;
            anim.SetBool("isWallRunning", true);
            verticalVelocity = 0f;
            wallRunDirection += transform.right * 0.5f; // Adjust to stick to the wall
            wallRunDirection.Normalize();
            // Override final movement with wall run movement
            finalMovement = wallRunDirection * wallRunForce * Time.deltaTime;
            anim.SetBool("wallRunRight", true);
        }
        else if (isWallLeft && !isGrounded && Input.GetKey(KeyCode.W))
        {
            isWallRunning = true;
            anim.SetBool("isWallRunning", true);
            verticalVelocity = 0f;
            wallRunDirection += -transform.right * 0.5f;
            wallRunDirection.Normalize();
            // Override final movement with wall run movement
            finalMovement = wallRunDirection * wallRunForce * Time.deltaTime;
            anim.SetBool("wallRunLeft", true);
        }
        else
        {
            isWallRunning = false;
            wallRunDirection = Vector3.zero;
            anim.SetBool("wallRunRight", false);
            anim.SetBool("wallRunLeft", false);
            anim.SetBool("isWallRunning", false); // Add this line for debugging purposes ("IsWallRunning: False)
        }
    }

    void ApplyMovement()
    {
        // Single controller.Move() call with final movement vector
        controller.Move(finalMovement);
    }
}
