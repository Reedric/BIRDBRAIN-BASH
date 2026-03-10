using UnityEngine;

public class EnemyAbility : MonoBehaviour {
    private bool abilitiesDisabled = false;
    public void disableAbilities(bool disabledOrNot) {
        abilitiesDisabled = disabledOrNot;
    }
    public bool canUseAbilities() {
        return !abilitiesDisabled;
    }
}