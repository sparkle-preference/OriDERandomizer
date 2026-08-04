using Core;
using UnityEngine;

public class DestroyWhenOutsideActiveBoundaries : MonoBehaviour {
    public void FixedUpdate() {
        index++;
        if (index != 5) {
            return;
        }

        index = 0;
        if (!Scenes.Manager.SceneVisibleAtPosition(transform.position)) {
            InstantiateUtility.Destroy(gameObject);
        }
    }

    private int index;
}
