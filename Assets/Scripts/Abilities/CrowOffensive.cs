using System.Collections;
using UnityEngine;

public class CrowOffensive : MonoBehaviour {
    [SerializeField]
    public float cooldown = 10f;
    [SerializeField]
    public float timeEnemiesAreImpacted = 3f;

    private bool onCooldown = false;

    public void crowAbility() {
        if (onCooldown) {
            Debug.Log("The crow is on cooldown and cannot activate its ability");
        }
        Debug.Log("Crow ability is activated. All enemies cannot attack for " + timeEnemiesAreImpacted);
        StartCoroutine(DisableEnemies());
        StartCoroutine(Cooldown());
    }
    IEnumerator DisableEnemies() {
        EnemyAbility[] enemies = FindObjectsOfType<EnemyAbility>();
        foreach (EnemyAbility enemy in enemies) {
            enemy.disableAbilities(true);
        }
        yield return new WaitForSeconds(timeEnemiesAreImpacted);
        foreach (EnemyAbility enemy in enemies) {
            enemy.disableAbilities(false);
        }
        Debug.Log("Abilities of enemies have been restored");
    }

    private IEnumerator Cooldown() {
        onCooldown = true;
        yield return new WaitForSeconds(cooldown);
        onCooldown = false;

    }
}
