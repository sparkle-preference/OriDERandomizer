using System.Collections;
using Game;
using UnityEngine;

[Category("World Map")]
public class LegacyDiscoverWorldMapAreasAction : ActionMethod {
    public override void Perform(IContext context) {
        m_isInstant = false;
        StartCoroutine(ShowWorldMap());
    }

    public override void PerformInstantly(IContext context) {
        m_isInstant = true;
        StartCoroutine(ShowWorldMap());
    }

    public IEnumerator ShowWorldMap() {
        yield return new WaitForFixedUpdate();
        RuntimeGameWorldArea currentArea = World.CurrentArea;
        if (currentArea != null) {
            currentArea.DiscoverAllAreas();
            AreaMapCanvas canvas = AreaMapUI.Instance.FindCanvas(currentArea.Area);
            canvas.UpdateAreaMaskTextureB();
            AreaMapUI.Instance.Navigation.UpdateScrollLimits();
            AreaMapUI.Instance.IconManager.ShowAreaIcons();
            StartCoroutine(ReleaseTexture(canvas));
        }

        if (OnClosedAction) {
            if (m_isInstant) {
                OnClosedAction.PerformInstantly(null);
            } else {
                OnClosedAction.Perform(null);
            }
        }
    }

    public IEnumerator ReleaseTexture(AreaMapCanvas canvas) {
        yield return new WaitForSeconds(1f);
        canvas.ReleaseAreaMaskTextureB();
        yield break;
    }

    public ActionMethod OnClosedAction;

    public float FadeDelay;

    public float MoveDuration = 1f;

    public float FadeDuration;

    public SoundProvider RevealSound;

    public Transform RevealPosition;

    private bool m_isInstant;
}
