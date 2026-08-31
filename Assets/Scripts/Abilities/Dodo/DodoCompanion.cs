using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DodoCompanion : MonoBehaviour
{
	[SerializeField] private float walkSpeed = 3f;
	[SerializeField] private float pushStrength = 3f;
	[SerializeField] private float minimumZ = -3.5f;
	[SerializeField] private float maximumZ = 3.5f;

	private Rigidbody rigidbodyComponent;
	private Animator animator;
	private bool movesTowardPositiveZ = true;
	private bool enemyIsOnLeft;

	public void Initialize(bool enemyCourtIsLeft, float patrolMinimumZ, float patrolMaximumZ)
	{
		enemyIsOnLeft = enemyCourtIsLeft;
		minimumZ = patrolMinimumZ;
		maximumZ = patrolMaximumZ;
		DisablePlayerBehaviours();
		IgnoreBallCollisions();
	}

	private void Awake()
	{
		rigidbodyComponent = GetComponent<Rigidbody>();
		animator = GetComponentInChildren<Animator>();
	}

	private void FixedUpdate()
	{
		if (rigidbodyComponent == null)
			return;

		if (transform.position.z >= maximumZ)
			movesTowardPositiveZ = false;
		else if (transform.position.z <= minimumZ)
			movesTowardPositiveZ = true;

		float zVelocity = movesTowardPositiveZ ? walkSpeed : -walkSpeed;
		rigidbodyComponent.linearVelocity = new Vector3(0f, rigidbodyComponent.linearVelocity.y, zVelocity);

		Vector3 facingDirection = movesTowardPositiveZ ? Vector3.forward : Vector3.back;
		transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facingDirection), 12f * Time.fixedDeltaTime);
		if (animator != null)
			animator.SetBool("isWalking", true);
	}

	private void OnCollisionStay(Collision collision)
	{
		GameObject player = GetPlayerRoot(collision.gameObject);
		if (player == null || !IsEnemyPlayer(player))
			return;

		Rigidbody playerRigidbody = player.GetComponent<Rigidbody>();
		if (playerRigidbody == null || playerRigidbody.isKinematic)
			return;

		Vector3 pushDirection = player.transform.position - transform.position;
		pushDirection.y = 0f;
		if (pushDirection.sqrMagnitude < 0.01f)
			pushDirection = movesTowardPositiveZ ? Vector3.forward : Vector3.back;

		playerRigidbody.AddForce(pushDirection.normalized * pushStrength, ForceMode.VelocityChange);
	}

	private void DisablePlayerBehaviours()
	{
		foreach (BallInteract ballInteract in GetComponentsInChildren<BallInteract>(true))
			ballInteract.enabled = false;

		foreach (AIBehavior aiBehaviour in GetComponentsInChildren<AIBehavior>(true))
			aiBehaviour.enabled = false;

		foreach (CharacterMovement characterMovement in GetComponentsInChildren<CharacterMovement>(true))
			characterMovement.enabled = false;

		foreach (BirdAbility ability in GetComponentsInChildren<BirdAbility>(true))
			ability.enabled = false;
	}

	private void IgnoreBallCollisions()
	{
		BallManager ballManager = BallManager.Instance;
		if (ballManager == null)
			return;

		Collider[] dodoColliders = GetComponentsInChildren<Collider>(true);
		Collider[] ballColliders = ballManager.GetComponentsInChildren<Collider>(true);
		foreach (Collider dodoCollider in dodoColliders)
		{
			foreach (Collider ballCollider in ballColliders)
				Physics.IgnoreCollision(dodoCollider, ballCollider, true);
		}
	}

	private static GameObject GetPlayerRoot(GameObject collidedObject)
	{
		GameManager gameManager = GameManager.Instance;
		if (gameManager == null)
			return null;

		foreach (GameObject player in new[]
		{
			gameManager.leftPlayer1,
			gameManager.leftPlayer2,
			gameManager.rightPlayer1,
			gameManager.rightPlayer2
		})
		{
			if (player != null && (collidedObject == player || collidedObject.transform.IsChildOf(player.transform)))
				return player;
		}

		return null;
	}

	private bool IsEnemyPlayer(GameObject player)
	{
		GameManager gameManager = GameManager.Instance;
		return enemyIsOnLeft
			? player == gameManager.leftPlayer1 || player == gameManager.leftPlayer2
			: player == gameManager.rightPlayer1 || player == gameManager.rightPlayer2;
	}
}