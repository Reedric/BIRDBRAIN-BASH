using UnityEngine;

public class FollowObject : MonoBehaviour
{
    [Header("Follow Settings")]
    public Transform target; // Object to follow (assign in inspector)
    public Vector3 offset = Vector3.zero; // Offset from the target position
    public float fixedYPosition = 0.0f; // Fixed Y position (ground level)
    
    private Quaternion initialRotation; // Store initial rotation to maintain it
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Store the initial rotation to maintain it
        initialRotation = transform.rotation;
        
        // If no fixed Y position is set, use current Y position
        // if (fixedYPosition == 0.0f)
        // {
        //     fixedYPosition = transform.position.y;
        // }
    }

    // Update is called once per frame
    void Update()
    {
        // Only follow if target is assigned
        if (target != null)
        {
            // Calculate target position with offset, but lock Y to fixed position
            Vector3 targetPosition = new Vector3(
                target.position.x + offset.x,
                fixedYPosition + offset.y,
                target.position.z + offset.z
            );
            
            transform.position = targetPosition;
        }
        
        transform.rotation = initialRotation;
    }
}
