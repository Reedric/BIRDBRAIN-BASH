using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class BallInteract : MonoBehaviour
{
    [Header("Ball Interaction Settings")]
    public float upwardForce = 500f;
    public float interactionRadius = 5f;
    
    [Header("Input Settings")]
    public InputActionAsset inputActionAsset;
    
    private Rigidbody rb;
    private InputActionMap playerActionMap;
    private InputAction interactAction;
    private Transform playerTransform;
    private GameObject ball;
    private Vector3 setLocation; // Where the ball will be set to after bumping
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        playerTransform = transform;
        setLocation = new UnityEngine.Vector3(1f, 0, 0);
        
        ball = GameObject.FindGameObjectWithTag("Ball");
        
        if (inputActionAsset != null)
        {
            playerActionMap = inputActionAsset.FindActionMap("Player");
            if (playerActionMap != null)
            {
                interactAction = playerActionMap.FindAction("Interact");
                if (interactAction != null)
                {
                    interactAction.performed += OnInteract;
                }
                else
                {
                    Debug.LogError("Interact action not found!");
                }
            }
            else
            {
                Debug.LogError("Player action map not found!");
            }
        }
        else
        {
            Debug.LogError("Input Action Asset not assigned!");
        }
    }

    void OnEnable()
    {
        if (playerActionMap != null)
        {
            playerActionMap.Enable();
        }
    }

    void OnDisable()
    {
        if (playerActionMap != null)
        {
            playerActionMap.Disable();
        }
    }

    void OnDestroy()
    {
        if (interactAction != null)
        {
            interactAction.performed -= OnInteract;
        }
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (ball == null)
        {
            Debug.LogWarning("Ball is null!");
            return;
        }
        bool nearBall = IsPlayerNearBall();
        if (nearBall)
        {
            Rigidbody ballRb = ball.GetComponent<Rigidbody>();
            if (ballRb != null)
            {
                // ballRb.AddForce(Vector3.up * upwardForce);

                BumpBall(ballRb);
            }
            else
            {
                Debug.LogWarning("Ball has no Rigidbody component!");
            }
        }
    }

    private bool IsPlayerNearBall()
    {
        if (ball == null) return false;
        
        float distance = Vector3.Distance(playerTransform.position, ball.transform.position);
        return distance <= interactionRadius;
    }
    
    void Update()
    {
        if (interactAction != null && interactAction.IsPressed())
        {
            OnInteractFallback();
        }
    }
    
    private void OnInteractFallback()
    {
        Debug.Log("Fallback interact triggered!");
        
        if (ball == null)
        {
            Debug.LogWarning("Ball is null!");
            return;
        }
        
        bool nearBall = IsPlayerNearBall();
        Debug.Log($"Player near ball: {nearBall}, Distance: {Vector3.Distance(playerTransform.position, ball.transform.position)}");
        
        if (nearBall)
        {
            Rigidbody ballRb = ball.GetComponent<Rigidbody>();
            if (ballRb != null)
            {
                // ballRb.AddForce(Vector3.up * upwardForce);

                BumpBall(ballRb);
            }
            else
            {
                Debug.LogWarning("Ball has no Rigidbody component!");
            }
        }
    }

    private void BumpBall(Rigidbody ballRb)
    {
        // Calculate the velocity in the y direction for the ball to reach a height of 5 given its current y component
        float gravity = MathF.Abs(Physics.gravity.y);
        float vyInit = MathF.Sqrt(2 * gravity * (5f - ballRb.transform.position.y));

        // Calculate time the ball will be in the air
        float vyFinal = MathF.Sqrt(10 * gravity);
        float t1 = vyInit / gravity;
        float t2 = vyFinal / gravity;
        float t = t1 + t2; 

        // Calculate the x and z velocities of the ball
        float vx = (setLocation.x - ballRb.transform.position.x) / t;
        float vz = (setLocation.z - ballRb.transform.position.z) / t;

        // Set the ball's intial velocity
        ballRb.linearVelocity = new Vector3(vx, vyInit, vz);
    }  
}
