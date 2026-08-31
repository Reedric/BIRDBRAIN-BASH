using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DodoOffensive : BirdAbility
{
	[Header("De-extinction")]
	[SerializeField] private GameObject firstDodoPrefab;
	[SerializeField] private GameObject secondDodoPrefab;
	[SerializeField] private GameObject spawnVfxPrefab;
	[SerializeField] private GameObject despawnVfxPrefab;
	[SerializeField] private float dodoLifetime = 7f;
	[SerializeField] private float spawnHeight = 10f;
	[SerializeField] private float nearNetXOffset = 1.5f;
	[SerializeField] private float farCourtXOffset = 5.5f;
	[SerializeField] private float firstDodoZ = -2f;
	[SerializeField] private float secondDodoZ = 2f;
	[SerializeField] private float minimumDodoSpacing = 4f;
	[SerializeField] private float courtMinimumZ = -3.5f;
	[SerializeField] private float courtMaximumZ = 3.5f;
	[SerializeField] private float laneGap = 1f;

	private readonly List<GameObject> activeDodos = new();
	private BallInteract ballInteract;
	private Coroutine lifetimeCoroutine;

	private void Awake()
	{
		ballInteract = GetComponent<BallInteract>();
		AbilitySlot = AbilitySlot.Offensive;
		_cooldownTime = 30f;
	}

	protected override bool Activate()
	{
		if (!GameManager.PointInProgress())
			return false;

		if (firstDodoPrefab == null || secondDodoPrefab == null)
		{
			Debug.LogWarning("DodoOffensive: assign both Dodo companion prefabs before using De-extinction.");
			return false;
		}

		if (ballInteract == null)
			ballInteract = GetComponent<BallInteract>();

		if (ballInteract == null || activeDodos.Count > 0)
			return false;

		float enemyCourtDirection = ballInteract.onLeft ? 1f : -1f;
		float laneBoundary = laneGap * 0.5f;
		float firstSpawnZ = Mathf.Clamp(firstDodoZ, courtMinimumZ, -laneBoundary);
		float secondSpawnZ = Mathf.Clamp(GetSecondSpawnZ(), laneBoundary, courtMaximumZ);
		SpawnDodo(firstDodoPrefab, new Vector3(enemyCourtDirection * nearNetXOffset, spawnHeight, firstSpawnZ), courtMinimumZ, -laneBoundary);
		SpawnDodo(secondDodoPrefab, new Vector3(enemyCourtDirection * farCourtXOffset, spawnHeight, secondSpawnZ), laneBoundary, courtMaximumZ);

		if (ballInteract.animator != null)
			ballInteract.animator.SetTrigger("OffensiveAbility");

		AudioManager.PlayBirdSound(BirdType.DODO, SoundType.OFFENSIVE, 1f);
		if (HUDManager.Instance != null)
			HUDManager.Instance.TriggerOffensiveCooldown(ballInteract.playerID, _cooldownTime);

		lifetimeCoroutine = StartCoroutine(DespawnDodosAfterLifetime());
		return true;
	}

	private void SpawnDodo(GameObject dodoPrefab, Vector3 spawnPosition, float minimumPatrolZ, float maximumPatrolZ)
	{
		GameObject dodo = Instantiate(dodoPrefab, spawnPosition, Quaternion.identity);
		DodoCompanion companion = dodo.GetComponent<DodoCompanion>();
		if (companion == null)
			companion = dodo.AddComponent<DodoCompanion>();

		companion.Initialize(!ballInteract.onLeft, minimumPatrolZ, maximumPatrolZ);
		activeDodos.Add(dodo);
		PlayVfx(spawnVfxPrefab, spawnPosition);
	}

	private IEnumerator DespawnDodosAfterLifetime()
	{
		float elapsed = 0f;
		while (elapsed < dodoLifetime && GameManager.Instance != null
			&& GameManager.Instance.gameState != GameManager.GameState.PointEnd)
		{
			elapsed += Time.deltaTime;
			yield return null;
		}

		ClearDodos();
		lifetimeCoroutine = null;
	}

	private float GetSecondSpawnZ()
	{
		if (Mathf.Abs(secondDodoZ - firstDodoZ) >= minimumDodoSpacing)
			return secondDodoZ;

		return firstDodoZ + Mathf.Max(0f, minimumDodoSpacing);
	}

	private void ClearDodos()
	{
		foreach (GameObject dodo in activeDodos)
		{
			if (dodo == null)
				continue;

			PlayVfx(despawnVfxPrefab, dodo.transform.position);
			Destroy(dodo);
		}

		activeDodos.Clear();
	}

	private void PlayVfx(GameObject vfxPrefab, Vector3 position)
	{
		if (vfxPrefab == null)
			return;

		GameObject vfx = Instantiate(vfxPrefab, position, Quaternion.identity);
		Destroy(vfx, 5f);
	}

	private void OnDestroy()
	{
		if (lifetimeCoroutine != null)
			StopCoroutine(lifetimeCoroutine);

		ClearDodos();
	}
}
