using UnityEngine;

public class GameMapTransitionManager : MonoBehaviour {
    public bool IsTransitioning => zoomTime != 0f && zoomTime < 1f;

    public bool InWorldMapMode => Mathf.Approximately(zoomTime, 0f);

    public bool InAreaMapMode => zoomTime >= 1f;

    public void Awake() {
        Instance = this;
    }

    public void OnDestroy() {
        if (Instance == this) {
            Instance = null;
        }
    }

    public float ZoomTime => zoomTime;

    public void ZoomToWorldMap() {
        if (!GameMapUI.Instance.ShowingTeleporters) {
            return;
        }

        if (ZoomOutSound) {
            ZoomOutSound.Play();
        }

        if (InAreaMapZoomOutSound) {
            InAreaMapZoomOutSound.Stop();
        }

        GoToWorldMap();
    }

    public void ZoomToAreaMap() {
        if (ZoomInSound) {
            ZoomInSound.Play();
        }

        GoToAreaMap();
    }

    public void Update() {
        mouseWheel += Input.GetAxis("Mouse ScrollWheel");
    }

    public void Advance() {
        if (!GameMapUI.Instance.ShowingObjective && !GameMapUI.Instance.RevealingMap) {
            var flag = Core.Input.ZoomOut.Pressed;
            var flag2 = Core.Input.ZoomIn.Pressed;
            var num = mouseWheel * 50f;
            mouseWheel = 0f;
            zoomSpeed = Mathf.Lerp(zoomSpeed, num, 0.5f);
            if (flag || flag2) {
                zoomSpeed = (!flag2 ? 0 : 1) - (!flag ? 0 : 1);
                zeroZoom = true;
            } else if (zeroZoom) {
                zoomSpeed = 0f;
                zeroZoom = false;
            }

            if (num > 0f) {
                flag2 = true;
            } else if (num < 0f) {
                flag = true;
            }

            if (flag) {
                if (areaMode && zoomTime <= 1f) {
                    ZoomToWorldMap();
                }
            } else if (zoomSpeed >= 0.05f && InAreaMapZoomOutSound) {
                InAreaMapZoomOutSound.Stop();
            }

            if (flag2) {
                if (!areaMode) {
                    ZoomToAreaMap();
                }
            } else if (zoomSpeed <= -0.05f && InAreaMapZoomInSound) {
                InAreaMapZoomInSound.Stop();
            }

            if (areaMode) {
                if (zoomTime >= 1f) {
                    if (zoomSpeed < -0.05f) {
                        if (InAreaMapZoomOutSound && !InAreaMapZoomOutSound.IsPlaying) {
                            InAreaMapZoomOutSound.Play();
                        }

                        zoomTime += Time.deltaTime * zoomSpeed;
                    } else if (zoomSpeed > 0.05f) {
                        if (InAreaMapZoomInSound && !InAreaMapZoomInSound.IsPlaying) {
                            InAreaMapZoomInSound.Play();
                        }

                        zoomTime += Time.deltaTime * zoomSpeed;
                        zoomTime = Mathf.Clamp(zoomTime, 1f, 2f);
                    }
                }
            } else if (Core.Input.ActionButtonA.OnPressed && !Core.Input.ActionButtonA.Used) {
                Core.Input.ActionButtonA.Used = true;
                ZoomToAreaMap();
            }
        }

        if (areaMode && zoomTime < 1f) {
            zoomTime += 1f / ZoomDuration * Time.deltaTime;
            zoomTime = Mathf.Clamp01(zoomTime);
            if (zoomTime == 1f) {
                WorldMapUI.Instance.Deactivate();
            }
        } else if (!areaMode) {
            zoomTime -= 1f / ZoomDuration * Time.deltaTime;
            zoomTime = Mathf.Clamp01(zoomTime);
            if (zoomTime == 0f) {
                AreaMapUI.Instance.Hide();
            }
        }
    }

    public void GoToWorldMap() {
        WorldMapUI.Instance.Activate();
        areaMode = false;
        AreaMapUI.Instance.FadeOutAnimator.Initialize();
        AreaMapUI.Instance.FadeOutAnimator.AnimatorDriver.ContinueForward();
        WorldMapUI.Instance.CrossFade.Initialize();
        WorldMapUI.Instance.CrossFade.AnimatorDriver.ContinueForward();
    }

    public void GoToAreaMap() {
        AreaMapUI.Instance.ResetMaps();
        areaMode = true;
        AreaMapUI.Instance.Show();
        AreaMapUI.Instance.Init();
        AreaMapUI.Instance.FadeOutAnimator.Initialize();
        AreaMapUI.Instance.FadeOutAnimator.AnimatorDriver.ContinueBackwards();
        WorldMapUI.Instance.CrossFade.Initialize();
        WorldMapUI.Instance.CrossFade.AnimatorDriver.ContinueBackwards();
    }

    public void GoToAreaMapInstantly() {
        areaMode = true;
        zoomTime = 1f;
        WorldMapUI.Instance.Deactivate();
        WorldMapUI.Instance.CrossFade.Initialize();
        WorldMapUI.Instance.CrossFade.AnimatorDriver.GoToStart();
        WorldMapUI.Instance.CrossFade.AnimatorDriver.Pause();
        AreaMapUI.Instance.FadeOutAnimator.Initialize();
        AreaMapUI.Instance.FadeOutAnimator.AnimatorDriver.GoToStart();
        AreaMapUI.Instance.FadeOutAnimator.AnimatorDriver.Pause();
        AreaMapUI.Instance.Show();
        AreaMapUI.Instance.Init();
    }

    public void GoToWorldMapInstantly() {
        areaMode = false;
        zoomTime = 0f;
        AreaMapUI.Instance.Hide();
        WorldMapUI.Instance.Activate();
        WorldMapUI.Instance.CrossFade.Initialize();
        WorldMapUI.Instance.CrossFade.AnimatorDriver.GoToEnd();
        WorldMapUI.Instance.CrossFade.AnimatorDriver.Pause();
        AreaMapUI.Instance.FadeOutAnimator.Initialize();
        AreaMapUI.Instance.FadeOutAnimator.AnimatorDriver.GoToEnd();
        AreaMapUI.Instance.FadeOutAnimator.AnimatorDriver.Pause();
    }

    public static GameMapTransitionManager Instance;

    private float zoomTime = 1f;

    public SoundSource ZoomInSound;

    public SoundSource ZoomOutSound;

    public SoundSource InAreaMapZoomInSound;

    public SoundSource InAreaMapZoomOutSound;

    private bool areaMode = true;

    public float ZoomDuration = 1f;

    private float zoomSpeed;

    private bool zeroZoom;

    private float mouseWheel;
}
