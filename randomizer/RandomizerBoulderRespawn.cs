using Game;
using UnityEngine;

// Restores the broken Blackroot boulder while that well is unactivated, never with Ori in the cave behind it.
public class RandomizerBoulderRespawn : MonoBehaviour {
    public EntityDamageReciever Boulder;
    public GameObject WallCollider;
    // the rolling boulder, off for good once the chase ends
    public GameObject Chaser;
    // set by a load or a death restore: respawn on the next tick even if the spot is on screen
    public bool CheckNow;

    // the cave and the spot the boulder rests on
    private static readonly Rect s_cave = Rect.MinMaxRect(420f, -331f, 441f, -300f);

    // a load can leave a broken boulder inactive at full health, so health alone can't tell
    private bool Broken => !Chaser.activeSelf && !Boulder.gameObject.activeSelf;

    public void FixedUpdate() {
        if (!Broken) {
            CheckNow = false;
            return;
        }

        if (Characters.Sein == null || UI.Cameras.Current == null) {
            return;
        }

        if (!CheckNow && UI.Cameras.Current.IsOnScreenPadded(Boulder.transform.position, 8f)) {
            return;
        }

        CheckNow = false;
        if (s_cave.Contains(Characters.Sein.Position) || WellActivated()) {
            return;
        }

        Boulder.Health = Boulder.MaxHealth;
        Boulder.UpdateActive();
        WallCollider.SetActive(true);
    }

    private static bool WellActivated() {
        if (TeleporterController.Instance == null) {
            return true;
        }

        foreach (var well in TeleporterController.Instance.Teleporters) {
            if (well.Identifier == "mangroveFalls") {
                return well.Activated;
            }
        }

        return true;
    }
}
