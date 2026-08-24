using System.Collections.Generic;
using UnityEngine;

public class EagleDefensive : BirdAbility
{
	[Header("Land of the Free")]
	[SerializeField] private GameObject starPrefab;
	[SerializeField] private int starCount = 3;
	[SerializeField] private float starLifetime = 10f;
	[SerializeField] private float minimumStarSpacing = 2f;
	[SerializeField] private float starHeightOffset = 0.5f;

	private readonly List<GameObject> activeStars = new();

	private void Awake()
	{
		if (_cooldownTime <= 0f)
			_cooldownTime = 20f;
	}

	private void Update()
	{
		if (activeStars.Count > 0 && GameManager.Instance.gameState == GameManager.GameState.PointEnd)
			ClearStars();
	}

	protected override bool Activate()
	{
		if (starPrefab == null)
		{
			Debug.LogWarning("EagleDefensive: assign a star prefab before using the ability.");
			return false;
		}

		ClearStars();

		GameManager gameManager = GameManager.Instance;
		bool isLeftPlayer = gameObject == gameManager.leftPlayer1 || gameObject == gameManager.leftPlayer2;
		bool isRightPlayer = gameObject == gameManager.rightPlayer1 || gameObject == gameManager.rightPlayer2;

		if (!isLeftPlayer && !isRightPlayer)
		{
			Debug.LogWarning("EagleDefensive: Eagle is not assigned to a GameManager player slot.");
			return false;
		}

		List<Vector3> positions = FindStarPositions(isLeftPlayer);
		foreach (Vector3 position in positions)
		{
			GameObject star = Instantiate(starPrefab, position, Quaternion.identity);
			EagleStar starCollision = star.AddComponent<EagleStar>();
			starCollision.Initialize(this, star, starLifetime, true);

			Collider[] starColliders = star.GetComponentsInChildren<Collider>(true);
			if (starColliders.Length == 0)
				starColliders = new[] { star.AddComponent<SphereCollider>() };

			foreach (Collider childCollider in starColliders)
			{
				childCollider.isTrigger = true;

				if (childCollider.gameObject == star)
					continue;

				EagleStar childCollision = childCollider.gameObject.AddComponent<EagleStar>();
				childCollision.Initialize(this, star, starLifetime, false);
			}

			activeStars.Add(star);
		}

		AudioManager.PlayBirdSound(BirdType.EAGLE, SoundType.DEFENSIVE, 1.0f);
		BallInteract ballInteract = GetComponent<BallInteract>();
		HUDManager.Instance.TriggerDefensiveCooldown(ballInteract.playerID, _cooldownTime);
		return true;
	}

	public void HandleStarHit(EagleStar star)
	{
		if (star == null || !activeStars.Contains(star.SpawnedStar))
			return;

		BallInteract ballInteract = GetComponent<BallInteract>();
		if (ballInteract == null)
			return;

		PerformAutomaticBump(ballInteract);

		activeStars.Remove(star.SpawnedStar);
		EagleStar rootStar = star.SpawnedStar.GetComponent<EagleStar>();
		if (rootStar != null)
			rootStar.BeginExit();
		else
			Destroy(star.SpawnedStar);
	}

	private void PerformAutomaticBump(BallInteract ballInteract)
	{
		Rigidbody ballRigidbody = BallManager.Instance?.GetComponent<Rigidbody>();
		if (ballRigidbody == null)
			return;

		bool isLeftPlayer = gameObject == GameManager.Instance.leftPlayer1
			|| gameObject == GameManager.Instance.leftPlayer2;
		Vector3 bumpTarget = isLeftPlayer ? new Vector3(-2f, 0f, 0f) : new Vector3(2f, 0f, 0f);
		float height = Mathf.Max(5f, ballRigidbody.position.y + 3f);
		float gravity = Mathf.Abs(Physics.gravity.y);
		float initialVerticalSpeed = Mathf.Sqrt(2f * gravity * height);
		float finalVerticalSpeed = Mathf.Sqrt(10f * gravity);
		float flightTime = initialVerticalSpeed / gravity + finalVerticalSpeed / gravity;

		float horizontalSpeed = (bumpTarget.x - ballRigidbody.position.x) / flightTime;
		float depthSpeed = (bumpTarget.z - ballRigidbody.position.z) / flightTime;
		ballRigidbody.useGravity = true;
		ballRigidbody.linearVelocity = new Vector3(horizontalSpeed, initialVerticalSpeed, depthSpeed);

		BallManager.Instance.goingTo = bumpTarget;
		BallManager.Instance.offCourse = false;
		BallManager.Instance.unblockableOwner = null;
		GameManager.Instance.gameState = GameManager.GameState.Bumped;
		GameManager.Instance.lastHit = gameObject;
		GameManager.Instance.leftAttack = ballInteract.onLeft;

		AudioManager.PlayBirdSound(ballInteract.GetBirdType(), SoundType.BUMP, 1.0f);
		AudioManager.PlayBallPlayerInteractionSound();
		HitEffects.Instance.PlayEffect(HitEffects.HitType.BumpSetServe, ballInteract.playerID);
	}

	private List<Vector3> FindStarPositions(bool isLeftPlayer)
	{
		List<Vector3> positions = new();
		float sideMin = isLeftPlayer ? -7f : 0.5f;
		float sideMax = isLeftPlayer ? -0.5f : 7f;

		for (int attempt = 0; attempt < 100 && positions.Count < starCount; attempt++)
		{
			Vector3 candidate = new(
				Random.Range(sideMin, sideMax),
				starHeightOffset,
				Random.Range(-4f, 4f));

			bool spaced = true;
			foreach (Vector3 position in positions)
			{
				if (Vector2.Distance(new Vector2(candidate.x, candidate.z),
					new Vector2(position.x, position.z)) < minimumStarSpacing)
				{
					spaced = false;
					break;
				}
			}

			if (spaced)
				positions.Add(candidate);
		}

		return positions;
	}

	private void ClearStars()
	{
		foreach (GameObject star in activeStars)
		{
			if (star != null)
			{
				EagleStar starAnimation = star.GetComponent<EagleStar>();
				if (starAnimation != null)
					starAnimation.BeginExit();
				else
					Destroy(star);
			}
		}

		activeStars.Clear();
	}

	private void OnDestroy()
	{
		foreach (GameObject star in activeStars)
		{
			if (star != null)
				Destroy(star);
		}

		activeStars.Clear();
	}
}
