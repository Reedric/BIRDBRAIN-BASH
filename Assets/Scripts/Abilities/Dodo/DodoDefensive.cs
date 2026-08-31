using UnityEngine;

public class DodoDefensive : BirdAbility
{
	public Animator animator;

	private void Awake()
	{
		_cooldownTime = 2f;

		if (animator == null)
			animator = GetComponent<Animator>();
	}

	protected override bool Activate()
	{
		if (animator != null)
			animator.SetTrigger("DefensiveAbility");

		AudioManager.PlayBirdSound(BirdType.DODO, SoundType.DEFENSIVE, 1.0f);

		return true;
	}
}
