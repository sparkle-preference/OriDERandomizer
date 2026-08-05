using System.Collections.Generic;
using Game;
using UnityEngine;
using Input = Core.Input;

public class DebugMenu : MonoBehaviour {
    private static void SuspendGameplay() {
        SuspensionManager.GetSuspendables(SuspendablesToIgnoreForGameplay, UI.Cameras.Current.GameObject);
        SuspensionManager.SuspendExcluding(SuspendablesToIgnoreForGameplay);
    }

    public static bool DashOrGrenadeEnabled => Characters.Sein && (Characters.Sein.PlayerAbilities.Dash.HasAbility || Characters.Sein.PlayerAbilities.Grenade.HasAbility);

    private static void ResumeGameplay() {
        SuspensionManager.ResumeExcluding(SuspendablesToIgnoreForGameplay);
        SuspendablesToIgnoreForGameplay.Clear();
    }

    public void FixedUpdate() {
        if (XboxLiveController.IsContentPackage) {
        }

        if (GameController.FreezeFixedUpdate) {
            return;
        }

        if (Characters.Current as Component && !UI.MainMenuVisible && !GameController.Instance.GameInTitleScreen) {
            if (DebugMenuB.DebugControlsEnabled && !Input.RightShoulder.Used && Input.RightShoulder.IsPressed && !DashOrGrenadeEnabled && !DebugMenuB.Active) {
                if (!m_noClipParamsEnabled) {
                    m_noClipGhost = (GameObject)InstantiateUtility.Instantiate(NoClipGhostPrefab);
                    m_noClipGhost.transform.position = Characters.Current.Position;
                    UI.Cameras.Current.ChangeTarget(m_noClipGhost.transform);
                    SuspendGameplay();
                    m_noClipParamsEnabled = true;
                    if (UberPostProcess.Instance != null) {
                        m_doMotionBlur = UberPostProcess.Instance.DoMotionBlur;
                        UberPostProcess.Instance.DoMotionBlur = false;
                    }
                }

                var vector = MoonMath.Vector.ApplyRectangleDeadzone(Input.Axis, 0.15f, 0.15f);
                m_noClipGhost.transform.position += (Vector3)vector.normalized * AxisToSpeedCurve.Evaluate(vector.magnitude) * Time.deltaTime;
            }

            if (m_noClipParamsEnabled && !Input.RightShoulder.IsPressed) {
                Characters.Current.Position = m_noClipGhost.transform.position;
                Characters.Current.Speed = Vector2.zero;
                if (Characters.Ori) {
                    Characters.Ori.MoveOriBackToPlayer();
                }

                UI.Cameras.Current.ChangeTargetToCurrentCharacter();
                InstantiateUtility.Destroy(m_noClipGhost);
                ResumeGameplay();
                m_noClipParamsEnabled = false;
                if (UberPostProcess.Instance != null) {
                    UberPostProcess.Instance.DoMotionBlur = m_doMotionBlur;
                }
            }
        }
    }

    public GameObject NoClipGhostPrefab;

    public AnimationCurve AxisToSpeedCurve;

    private GameObject m_noClipGhost;

    private bool m_noClipParamsEnabled;

    private static readonly HashSet<ISuspendable> SuspendablesToIgnoreForGameplay = new HashSet<ISuspendable>();

    private bool m_doMotionBlur;
}
