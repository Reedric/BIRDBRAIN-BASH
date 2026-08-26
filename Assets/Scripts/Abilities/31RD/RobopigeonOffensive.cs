using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobopigeonOffensive : BirdAbility
{
	[Header("Poops On You Settings")]
	[SerializeField] private GameObject strikeRegionPrefab;
	[SerializeField] private GameObject hitmarkerVfxPrefab;
	[SerializeField] private float strikeDelay = 2f;
	[SerializeField] private float stunDuration = 5f;
	[SerializeField] private float strikeRadius = 4f;
	[SerializeField] private float regionLifetime = 3f;
	[SerializeField] private float courtMinX = 0.5f;
	[SerializeField] private float courtMaxX = 7f;
	[SerializeField] private float courtMinZ = -3f;
	[SerializeField] private float courtMaxZ = 3f;

	private BallInteract ballInteract;
	private readonly Dictionary<GameObject, GameObject> activeHitmarkers = new();

	private void Awake()
	{
		ballInteract = GetComponent<BallInteract>();
		AbilitySlot = AbilitySlot.Offensive;
		_cooldownTime = 40f;
	}

	protected override bool Activate()
	{
		if (ballInteract == null)
			ballInteract = GetComponent<BallInteract>();

		if (ballInteract == null || BuffsDebuffs.Instance == null)
			return false;

		Vector3 strikePosition = GetStrikePosition();
		GameObject region = CreateStrikeRegion(strikePosition);
		StartCoroutine(TrackStrikeTargets(strikePosition));
		StartCoroutine(ResolveStrike(strikePosition, region));

		if (ballInteract.animator != null)
			ballInteract.animator.SetTrigger("OffensiveAbility");

		AudioManager.PlayBirdSound(BirdType.ROBOPIGEON, SoundType.OFFENSIVE, 1f);
		if (HUDManager.Instance != null)
			HUDManager.Instance.TriggerOffensiveCooldown(ballInteract.playerID, _cooldownTime);

		return true;
	}

	private Vector3 GetStrikePosition()
	{
		bool enemyCourtIsLeft = !ballInteract.onLeft;
		GameObject ball = BallManager.Instance != null ? BallManager.Instance.gameObject : null;

		if (ball != null && (ball.transform.position.x < 0f) == enemyCourtIsLeft)
		{
			Vector3 ballPosition = ball.transform.position;
			ballPosition.y = 0.1f;
			return ballPosition;
		}

		float minX = enemyCourtIsLeft ? -courtMaxX : courtMinX;
		float maxX = enemyCourtIsLeft ? -courtMinX : courtMaxX;
		return new Vector3(Random.Range(minX, maxX), 0.1f, Random.Range(courtMinZ, courtMaxZ));
	}

	private GameObject CreateStrikeRegion(Vector3 position)
	{
		if (strikeRegionPrefab == null)
			return null;

		GameObject region = Instantiate(strikeRegionPrefab, position, Quaternion.identity);
		float diameter = strikeRadius * 2f;
		region.transform.localScale = new Vector3(diameter, region.transform.localScale.y, diameter);
		return region;
	}

	private IEnumerator ResolveStrike(Vector3 strikePosition, GameObject region)
	{
		yield return new WaitForSeconds(strikeDelay);
		ClearHitmarkers();

		GameManager gameManager = GameManager.Instance;
		if (gameManager != null && gameManager.gameState == GameManager.GameState.PointEnd)
		{
			RefundCooldown(region);
			yield break;
		}

		if (gameManager != null)
		{
			GameObject[] opponents = ballInteract.onLeft
				? new[] { gameManager.rightPlayer1, gameManager.rightPlayer2 }
				: new[] { gameManager.leftPlayer1, gameManager.leftPlayer2 };

			foreach (GameObject opponent in opponents)
			{
				if (opponent == null || HorizontalDistance(strikePosition, opponent.transform.position) > strikeRadius)
					continue;

				BallInteract opponentBall = opponent.GetComponent<BallInteract>();
				BirdType birdType = opponentBall != null
					? opponentBall.GetBirdType()
					: opponent.GetComponent<AIBehavior>()?.GetBirdType() ?? BirdType.SEAGULL;

				if (birdType == BirdType.OSTRICH)
					continue;

				bool opponentIsOnLeft = opponentBall != null
					? opponentBall.onLeft
					: opponent.GetComponent<AIBehavior>()?.onLeft ?? false;

				BuffsDebuffs.Instance.ApplyEffect(
					BuffsDebuffs.EffectType.Stun,
					opponent,
					stunDuration,
					opponentIsOnLeft);

			}
		}

		if (region != null)
			Destroy(region, Mathf.Max(0f, regionLifetime - strikeDelay));
	}

	private IEnumerator TrackStrikeTargets(Vector3 strikePosition)
	{
		float elapsed = 0f;

		while (elapsed < strikeDelay)
		{
			GameManager gameManager = GameManager.Instance;
			if (gameManager != null && gameManager.gameState == GameManager.GameState.PointEnd)
			{
				ClearHitmarkers();
				yield break;
			}

			HashSet<GameObject> targetsInRadius = new();
			foreach (GameObject opponent in GetOpponents(gameManager))
			{
				if (IsStunnableTarget(opponent)
					&& HorizontalDistance(strikePosition, opponent.transform.position) <= strikeRadius
					&& !BuffsDebuffs.Instance.IsEffectActive(BuffsDebuffs.EffectType.Stun, opponent))
				{
					targetsInRadius.Add(opponent);
					AddHitmarker(opponent);
				}
			}

			foreach (GameObject target in new List<GameObject>(activeHitmarkers.Keys))
			{
				if (!targetsInRadius.Contains(target))
					RemoveHitmarker(target);
			}

			elapsed += Time.deltaTime;
			yield return null;
		}
	}

	private GameObject[] GetOpponents(GameManager gameManager)
	{
		if (gameManager == null)
			return System.Array.Empty<GameObject>();

		return ballInteract.onLeft
			? new[] { gameManager.rightPlayer1, gameManager.rightPlayer2 }
			: new[] { gameManager.leftPlayer1, gameManager.leftPlayer2 };
	}

	private bool IsStunnableTarget(GameObject opponent)
	{
		if (opponent == null)
			return false;

		BallInteract opponentBall = opponent.GetComponent<BallInteract>();
		BirdType birdType = opponentBall != null
			? opponentBall.GetBirdType()
			: opponent.GetComponent<AIBehavior>()?.GetBirdType() ?? BirdType.SEAGULL;
		return birdType != BirdType.OSTRICH;
	}

	private void RefundCooldown(GameObject region)
	{
		if (region != null)
			Destroy(region);

		_cooldownRemaining = 0f;
		if (HUDManager.Instance != null && ballInteract != null
			&& ballInteract.playerID >= 0 && ballInteract.playerID < 4)
		{
			HUDManager.Instance.ResetOffensiveCooldown(ballInteract.playerID);
		}
	}

	private static float HorizontalDistance(Vector3 first, Vector3 second)
	{
		Vector2 firstPoint = new(first.x, first.z);
		Vector2 secondPoint = new(second.x, second.z);
		return Vector2.Distance(firstPoint, secondPoint);
	}

	private void AddHitmarker(GameObject bird)
	{
		if (hitmarkerVfxPrefab == null || activeHitmarkers.ContainsKey(bird))
			return;

		GameObject hitmarker = Instantiate(hitmarkerVfxPrefab, bird.transform);
		hitmarker.transform.localPosition = Vector3.zero;
		activeHitmarkers[bird] = hitmarker;
	}

	private void RemoveHitmarker(GameObject bird)
	{
		if (!activeHitmarkers.TryGetValue(bird, out GameObject hitmarker))
			return;

		if (hitmarker != null)
			Destroy(hitmarker);
		activeHitmarkers.Remove(bird);
	}

	private void ClearHitmarkers()
	{
		foreach (GameObject bird in new List<GameObject>(activeHitmarkers.Keys))
			RemoveHitmarker(bird);
	}
}
