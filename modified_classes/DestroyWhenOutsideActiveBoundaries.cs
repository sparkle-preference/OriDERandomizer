using Core;
using UnityEngine;

public class DestroyWhenOutsideActiveBoundaries : MonoBehaviour {
    public void FixedUpdate() {
        m_index++;
        if (m_index != 5) {
            return;
        }

        m_index = 0;
        if (!Scenes.Manager.SceneVisibleAtPosition(transform.position))
            // a captured enemy is held outside the active scene on purpose
            // TODO: this code doesn't actually work.  
            // if (RandomizerBonusSkill.CapturedEnemy && RandomizerBonusSkill.CapturedEnemy.gameObject == base.gameObject)
            // {
            // 	Randomizer.LogError("Despawn prevented!");
            // }
            // else
        {
            InstantiateUtility.Destroy(gameObject);
        }
    }

    private int m_index;
}
