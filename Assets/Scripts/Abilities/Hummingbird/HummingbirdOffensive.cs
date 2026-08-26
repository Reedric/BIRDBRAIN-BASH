using System.Collections;
using UnityEngine;

[RequireComponent(typeof(BallInteract))]
public class HummingbirdOffensive : BirdAbility
{
	[Header("Sweet Tongue")]
	[SerializeField] private GameObject nectarPrefab;
	[SerializeField] private GameObject birdNectarVfxPrefab;
	[SerializeField] private float nectarLifetime = 10f;
	[SerializeField] private float slowAmount = 4f;
	[SerializeField] private float jumpReduction = 4f;
	[SerializeField] private float lingerDuration = 1f;
	[SerializeField] private float nectarFadeDuration = 0.5f;

	[Header("Tongue Visual")]
	[SerializeField] private Color tongueColor = new Color(1f, 0.2f, 0.65f, 1f);
	[SerializeField] private float tongueWidth = 0.12f;
	[SerializeField] private float tongueExtendDuration = 0.25f;
	[SerializeField] private float tongueHoldDuration = 0.1f;
	[SerializeField] private float tongueRetractDuration = 0.25f;

	private void Awake()
	{
		if (_cooldownTime <= 0f)
			_cooldownTime = 15f;
	}

	protected override bool Activate()
	{
		if (nectarPrefab == null)
		{
			Debug.LogWarning("HummingbirdOffensive: assign a nectar prefab before using Sweet Tongue.");
			return false;
		}

		GameManager gameManager = GameManager.Instance;
		BallInteract hummingbird = GetComponent<BallInteract>();
		bool isLeft = gameObject == gameManager.leftPlayer1 || gameObject == gameManager.leftPlayer2;
		GameObject opponent = FindNearestOpponent(isLeft, gameManager);
		if (opponent == null)
		{
			Debug.LogWarning("HummingbirdOffensive: could not find an opposing bird.");
			return false;
		}

		Vector3 spawnPosition = opponent.transform.position;
		GameObject nectar = Instantiate(nectarPrefab, spawnPosition, Quaternion.identity);
		HummingbirdNectar nectarZone = nectar.GetComponent<HummingbirdNectar>();
		if (nectarZone == null)
			nectarZone = nectar.AddComponent<HummingbirdNectar>();

		nectarZone.Initialize(
			slowAmount,
			jumpReduction,
			lingerDuration,
			nectarLifetime,
			nectarFadeDuration,
			birdNectarVfxPrefab);

		StartCoroutine(AnimateTongue(spawnPosition));

		if (hummingbird.animator != null)
			hummingbird.animator.SetTrigger("OffensiveAbility");

		AudioManager.PlayBirdSound(BirdType.HUMMINGBIRD, SoundType.OFFENSIVE, 1.0f);
		HUDManager.Instance.TriggerOffensiveCooldown(hummingbird.playerID, _cooldownTime);
		return true;
	}

	private GameObject FindNearestOpponent(bool isLeft, GameManager gameManager)
	{
		GameObject[] opponents = isLeft
			? new[] { gameManager.rightPlayer1, gameManager.rightPlayer2 }
			: new[] { gameManager.leftPlayer1, gameManager.leftPlayer2 };

		GameObject nearest = null;
		float nearestDistance = float.MaxValue;
		foreach (GameObject opponent in opponents)
		{
			if (opponent == null)
				continue;

			float distance = (opponent.transform.position - transform.position).sqrMagnitude;
			if (distance < nearestDistance)
			{
				nearest = opponent;
				nearestDistance = distance;
			}
		}

		return nearest;
	}

	private IEnumerator AnimateTongue(Vector3 targetPosition)
	{
		GameObject tongueObject = new GameObject("HummingbirdTongue");
		LineRenderer tongue = tongueObject.AddComponent<LineRenderer>();
		tongue.material = new Material(Shader.Find("Sprites/Default"));
		tongue.startColor = tongueColor;
		tongue.endColor = tongueColor;
		tongue.startWidth = tongueWidth;
		tongue.endWidth = tongueWidth;
		tongue.positionCount = 2;
		tongue.useWorldSpace = true;

		Vector3 tongueStart = transform.position;
		tongue.SetPosition(0, tongueStart);
		tongue.SetPosition(1, tongueStart);

		float elapsed = 0f;
		while (elapsed < tongueExtendDuration)
		{
			elapsed += Time.deltaTime;
			float progress = Mathf.Clamp01(elapsed / tongueExtendDuration);
			tongue.SetPosition(0, transform.position);
			tongue.SetPosition(1, Vector3.Lerp(tongueStart, targetPosition, progress));
			yield return null;
		}

		tongue.SetPosition(1, targetPosition);
		yield return new WaitForSeconds(tongueHoldDuration);

		elapsed = 0f;
		while (elapsed < tongueRetractDuration)
		{
			elapsed += Time.deltaTime;
			float progress = Mathf.Clamp01(elapsed / tongueRetractDuration);
			tongue.SetPosition(0, transform.position);
			tongue.SetPosition(1, Vector3.Lerp(targetPosition, transform.position, progress));
			yield return null;
		}

		Destroy(tongueObject);
	}
}
