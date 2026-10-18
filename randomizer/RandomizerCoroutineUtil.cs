using UnityEngine;

public class RandomizerCoroutineUtil : MonoBehaviour {
    /// <summary>
    /// An always-active MonoBehaviour coroutines can be attached to.
    /// </summary>
    public static RandomizerCoroutineUtil Instance => field ??= CreateInstance();

    private static RandomizerCoroutineUtil CreateInstance() {
        var go = new GameObject("Randomizer Coroutine Util") {
            hideFlags = HideFlags.HideAndDontSave,
        };
        DontDestroyOnLoad(go);

        return go.AddComponent<RandomizerCoroutineUtil>();
    }
}
