using UnityEngine;

public class GameMapTransitionManager : MonoBehaviour
{
	public bool IsTransitioning => m_zoomTime != 0f && m_zoomTime < 1f;

	public bool InWorldMapMode => Mathf.Approximately(m_zoomTime, 0f);

	public bool InAreaMapMode => m_zoomTime >= 1f;

	public void Awake()
	{
		Instance = this;
	}

	public void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public float ZoomTime => m_zoomTime;

	public void ZoomToWorldMap()
	{
        if (!GameMapUI.Instance.ShowingTeleporters)
		{
			return;
		}
		if (ZoomOutSound)
		{
			ZoomOutSound.Play();
		}
		if (InAreaMapZoomOutSound)
		{
			InAreaMapZoomOutSound.Stop();
		}
		GoToWorldMap();
	}

	public void ZoomToAreaMap()
	{
		if (ZoomInSound)
		{
			ZoomInSound.Play();
		}
		GoToAreaMap();
	}

	public void Update()
	{
		m_mouseWheel += Input.GetAxis("Mouse ScrollWheel");
	}

	public void Advance()
	{
		if (!GameMapUI.Instance.ShowingObjective && !GameMapUI.Instance.RevealingMap)
		{
			bool flag = Core.Input.ZoomOut.Pressed;
			bool flag2 = Core.Input.ZoomIn.Pressed;
			float num = m_mouseWheel * 50f;
			m_mouseWheel = 0f;
			m_zoomSpeed = Mathf.Lerp(m_zoomSpeed, num, 0.5f);
			if (flag || flag2)
			{
				m_zoomSpeed = (!flag2 ? 0 : 1) - (!flag ? 0 : 1);
				m_zeroZoom = true;
			}
			else if (m_zeroZoom)
			{
				m_zoomSpeed = 0f;
				m_zeroZoom = false;
			}
			if (num > 0f)
			{
				flag2 = true;
			}
			else if (num < 0f)
			{
				flag = true;
			}
			if (flag)
			{
				if (m_areaMode && m_zoomTime <= 1f)
				{
					ZoomToWorldMap();
				}
			}
			else if (m_zoomSpeed >= 0.05f && InAreaMapZoomOutSound)
			{
				InAreaMapZoomOutSound.Stop();
			}
			if (flag2)
			{
				if (!m_areaMode)
				{
					ZoomToAreaMap();
				}
			}
			else if (m_zoomSpeed <= -0.05f && InAreaMapZoomInSound)
			{
				InAreaMapZoomInSound.Stop();
			}
			if (m_areaMode)
			{
				if (m_zoomTime >= 1f)
				{
					if (m_zoomSpeed < -0.05f)
					{
						if (InAreaMapZoomOutSound && !InAreaMapZoomOutSound.IsPlaying)
						{
							InAreaMapZoomOutSound.Play();
						}
						m_zoomTime += Time.deltaTime * m_zoomSpeed;
					}
					else if (m_zoomSpeed > 0.05f)
					{
						if (InAreaMapZoomInSound && !InAreaMapZoomInSound.IsPlaying)
						{
							InAreaMapZoomInSound.Play();
						}
						m_zoomTime += Time.deltaTime * m_zoomSpeed;
						m_zoomTime = Mathf.Clamp(m_zoomTime, 1f, 2f);
					}
				}
			}
			else if (Core.Input.ActionButtonA.OnPressed && !Core.Input.ActionButtonA.Used)
			{
				Core.Input.ActionButtonA.Used = true;
				ZoomToAreaMap();
			}
		}
		if (m_areaMode && m_zoomTime < 1f)
		{
			m_zoomTime += 1f / ZoomDuration * Time.deltaTime;
			m_zoomTime = Mathf.Clamp01(m_zoomTime);
			if (m_zoomTime == 1f)
			{
				WorldMapUI.Instance.Deactivate();
			}
		}
		else if (!m_areaMode)
		{
			m_zoomTime -= 1f / ZoomDuration * Time.deltaTime;
			m_zoomTime = Mathf.Clamp01(m_zoomTime);
			if (m_zoomTime == 0f)
			{
				AreaMapUI.Instance.Hide();
			}
		}
	}

	public void GoToWorldMap()
	{
		WorldMapUI.Instance.Activate();
		m_areaMode = false;
		AreaMapUI.Instance.FadeOutAnimator.Initialize();
		AreaMapUI.Instance.FadeOutAnimator.AnimatorDriver.ContinueForward();
		WorldMapUI.Instance.CrossFade.Initialize();
		WorldMapUI.Instance.CrossFade.AnimatorDriver.ContinueForward();
	}

	public void GoToAreaMap()
	{
		AreaMapUI.Instance.ResetMaps();
		m_areaMode = true;
		AreaMapUI.Instance.Show();
		AreaMapUI.Instance.Init();
		AreaMapUI.Instance.FadeOutAnimator.Initialize();
		AreaMapUI.Instance.FadeOutAnimator.AnimatorDriver.ContinueBackwards();
		WorldMapUI.Instance.CrossFade.Initialize();
		WorldMapUI.Instance.CrossFade.AnimatorDriver.ContinueBackwards();
	}

	public void GoToAreaMapInstantly()
	{
		m_areaMode = true;
		m_zoomTime = 1f;
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

	public void GoToWorldMapInstantly()
	{
		m_areaMode = false;
		m_zoomTime = 0f;
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

	private float m_zoomTime = 1f;

	public SoundSource ZoomInSound;

	public SoundSource ZoomOutSound;

	public SoundSource InAreaMapZoomInSound;

	public SoundSource InAreaMapZoomOutSound;

	private bool m_areaMode = true;

	public float ZoomDuration = 1f;

	private float m_mouseWheelSmooth;

	private float m_zoomSpeed;

	private bool m_zeroZoom;

	private float m_mouseWheel;
}