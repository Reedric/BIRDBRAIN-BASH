using UnityEngine;

/// <summary>
/// Helping Hand reduces both ability cooldowns for Shima Enaga and its teammate.
/// Active cooldowns lose 30% of their full duration; ready abilities receive the
/// same reduction on their next activation.
/// </summary>
public class ShimaEnagaDefensive : BirdAbility
{
	[SerializeField, Range(0f, 1f)] private float cooldownReduction = 0.30f;

	private void Awake()
	{
		AbilitySlot = AbilitySlot.Defensive;
		if (_cooldownTime <= 0f)
			_cooldownTime = 20f;
	}

	protected override bool Activate()
	{
		ApplyCooldownReductionToBird(gameObject, true);

		GameObject teammate = GetTeammate();
		if (teammate != null)
			ApplyCooldownReductionToBird(teammate, false);

		BallInteract ballInteract = GetComponent<BallInteract>();
		int playerID = ballInteract != null ? ballInteract.playerID : GetComponent<AIBehavior>()?.playerID ?? -1;
		if (playerID >= 0 && HUDManager.Instance != null)
			HUDManager.Instance.TriggerDefensiveCooldown(playerID, _cooldownTime);

		AudioManager.PlayBirdSound(BirdType.SHIMAENAGA, SoundType.DEFENSIVE, 1f);
		return true;
	}

	private void ApplyCooldownReductionToBird(GameObject bird, bool skipHelpingHand)
	{
		foreach (BirdAbility ability in bird.GetComponentsInChildren<BirdAbility>(true))
		{
			if (ability.AbilitySlot != AbilitySlot.Offensive && ability.AbilitySlot != AbilitySlot.Defensive)
				continue;

			if (skipHelpingHand && ability == this)
				continue;

			ability.ApplyCooldownReduction(cooldownReduction);
		}
	}

	private GameObject GetTeammate()
	{
		GameManager gameManager = GameManager.Instance;
		if (gameManager == null) return null;

		if (gameObject == gameManager.leftPlayer1) return gameManager.leftPlayer2;
		if (gameObject == gameManager.leftPlayer2) return gameManager.leftPlayer1;
		if (gameObject == gameManager.rightPlayer1) return gameManager.rightPlayer2;
		if (gameObject == gameManager.rightPlayer2) return gameManager.rightPlayer1;

		return null;
	}
}
