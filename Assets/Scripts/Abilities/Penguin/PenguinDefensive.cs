using System.Collections;
using UnityEngine;

public class PenguinDefensive : BirdAbility
{
    [Header("Dash Ability")]
    public float dashDuration = 1.0f;
    public float dashSpeed = 10.0f; // Forward movement speed during dash
    public float rotationSpeed = 8.0f; // How fast penguin rotates
    [HideInInspector] public bool isDashing = false;
    private bool isReturningUpright = false; // Ensure penguin returns to upright after dash
    private CharacterMovement cM; // Track movement from character movement script
    private Rigidbody rb;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        cM = GetComponent<CharacterMovement>();
    }

    // Update is called once per frame
    void Update()
    {
        // Handle returning to upright position
        if (isReturningUpright && cM != null)
        {
            float angleFromUpright = Vector3.Angle(transform.up, Vector3.up);
            if (angleFromUpright < 5.0f)
            {
                isReturningUpright = false;
                cM.overrideRotation = false;
            }
        }
    }

    protected override bool Activate()
    {
        if (cM.grounded)
        {
            StartCoroutine(StartDash());
            return true;
        }

        return false;
    }

    IEnumerator StartDash()
    {
        cM.controlMovement(false, false); // christofort: makes canJump false
        isDashing = true;

        // Apply forward force in the direction penguin is currently facing, accounting for rotation offset (sideways prefab)
        Vector3 slideDirection;
        Vector3 offset = cM.rotationOffsetEuler;

        // Add 180 degrees to Y to invert the direction
        offset.y += 180f;
        slideDirection = transform.rotation * Quaternion.Euler(offset) * Vector3.forward;
        rb.AddForce(slideDirection.normalized * dashSpeed, ForceMode.Impulse);

        // Trigger dash animation if animator exists
        if (cM.animator != null)
        {
            cM.animator.SetTrigger("Dash");
        }

        // Override CharacterMovement rotation to do belly slide
        cM.overrideRotation = true;

        // Play slide sound
        AudioManager.PlayBirdSound(BirdType.PENGUIN, SoundType.DEFENSIVE, 1.0f);

        // Do the dash for however long its supposed to be done
        yield return new WaitForSeconds(dashDuration);

        int playerID = GetComponent<BallInteract>().playerID;
        HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);

        // End the dash
        EndDash();
    }

    void EndDash()
    {
        isDashing = false;
        cM.controlMovement(true, true); // christofort: sets canJump back to True

        // Start transition back to upright position
        Vector3 currentEuler = transform.eulerAngles;
        Quaternion uprightRotation = Quaternion.Euler(0, currentEuler.y, 0);
        cM.targetRotation = uprightRotation;
        isReturningUpright = true;
    }
}
