using System;
using UnityEngine;
using Object = UnityEngine.Object;

[Serializable]
public class GameMapTeleporter
{
	public GameMapTeleporter(SceneMetaData.Teleporter teleporter, SceneMetaData sceneMetaData)
	{
		Identifier = teleporter.Identifier;
		WorldPosition = teleporter.SceneLocalPosition + sceneMetaData.RootPosition;
	}

	public void Show()
	{
		var instance = AreaMapUI.Instance;
		if (m_worldMapIconGameObject)
		{
			m_worldMapIconGameObject.SetActive(true);
		}
		else
		{
			var gameObject = Object.Instantiate(instance.TeleportPrefab);
			m_worldMapIconTransform = gameObject.transform;
			m_worldMapIconGameObject = m_worldMapIconTransform.gameObject;
			m_worldMapIconHighlightAnimator = m_worldMapIconGameObject.transform.FindChild("highlight").GetComponentInChildren<TransparencyAnimator>();
			m_worldMapIconTransform.position = WorldMapUI.Instance.WorldToUIPosition(WorldPosition);
			m_worldMapIconTransform.parent = WorldMapUI.Instance.FadeOutGroup;
			TransparencyAnimator.Register(m_worldMapIconTransform);
			if (Name.GetType() == typeof(RandomizerMessageProvider))
			{
				var componentsInChildren = m_worldMapIconGameObject.GetComponentsInChildren<Renderer>();
				int[] multiplicative = {0, 10, 11, 12};
				int[] others = {1, 2, 3, 4, 5, 6, 7, 8, 9};
				foreach (var index in multiplicative)
				{
					var originalColor = componentsInChildren[index].material.color;
					var newColor = new Color(RandomizerSettings.Customization.WarpTeleporterColor.Value.r * originalColor.r,
						RandomizerSettings.Customization.WarpTeleporterColor.Value.g * originalColor.g,
						RandomizerSettings.Customization.WarpTeleporterColor.Value.b * originalColor.b,
						originalColor.a);
					componentsInChildren[index].material.color = newColor;
				}
				foreach (var index2 in others)
				{
					var originalColor2 = componentsInChildren[index2].material.color;
					var newColor2 = new Color(RandomizerSettings.Customization.WarpTeleporterColor.Value.r,
						RandomizerSettings.Customization.WarpTeleporterColor.Value.g,
						RandomizerSettings.Customization.WarpTeleporterColor.Value.b,
						originalColor2.a);
					componentsInChildren[index2].material.color = newColor2;
				}
			}
		}
		if (m_areaMapIconGameObject)
		{
			m_areaMapIconGameObject.SetActive(true);
		}
		else
		{
			var gameObject2 = Object.Instantiate(instance.TeleportPrefab);
			m_areaMapIconTransform = gameObject2.transform;
			m_areaMapIconGameObject = m_areaMapIconTransform.gameObject;
			m_areaMapIconHighlightAnimator = m_areaMapIconGameObject.transform.FindChild("highlight").GetComponentInChildren<TransparencyAnimator>();
			m_areaMapIconTransform.position = AreaMapUI.Instance.Navigation.WorldToMapPosition(WorldPosition + Vector3.up * 4f);
			m_areaMapIconTransform.parent = AreaMapUI.Instance.FadeOutGroup;
			TransparencyAnimator.Register(m_areaMapIconTransform);
			if (Name.GetType() == typeof(RandomizerMessageProvider))
			{
				var componentsInChildren2 = m_areaMapIconGameObject.GetComponentsInChildren<Renderer>();
				int[] multiplicative2 = {0, 10, 11, 12};
				int[] others2 = {1, 2, 3, 4, 5, 6, 7, 8, 9};
				foreach (var index3 in multiplicative2)
				{
					var originalColor3 = componentsInChildren2[index3].material.color;
					var newColor3 = new Color(RandomizerSettings.Customization.WarpTeleporterColor.Value.r * originalColor3.r,
						RandomizerSettings.Customization.WarpTeleporterColor.Value.g * originalColor3.g,
						RandomizerSettings.Customization.WarpTeleporterColor.Value.b * originalColor3.b,
						originalColor3.a);
					componentsInChildren2[index3].material.color = newColor3;
				}
				foreach (var index4 in others2)
				{
					var originalColor4 = componentsInChildren2[index4].material.color;
					var newColor4 = new Color(RandomizerSettings.Customization.WarpTeleporterColor.Value.r,
						RandomizerSettings.Customization.WarpTeleporterColor.Value.g,
						RandomizerSettings.Customization.WarpTeleporterColor.Value.b,
						originalColor4.a);
					componentsInChildren2[index4].material.color = newColor4;
				}
			}
		}
	}

	public void Update()
	{
		if (m_worldMapIconTransform)
		{
			m_worldMapIconTransform.position = WorldMapUI.Instance.WorldToUIPosition(WorldPosition);
		}
		if (m_areaMapIconTransform)
		{
			m_areaMapIconTransform.position = AreaMapUI.Instance.Navigation.WorldToMapPosition(WorldPosition + Vector3.up * 4f);
		}
	}

	public Vector2 WorldMapIconPosition => m_worldMapIconTransform.position;

	public Vector2 AreaMapIconPosition => m_areaMapIconTransform.position;

	public Vector2 WorldProjectedPositon => WorldMapUI.Instance.WorldToProjectedPosition(WorldPosition);

	public RuntimeGameWorldArea Area => GameWorld.Instance.FindRuntimeArea(GameWorld.Instance.FindAreaFromPosition(WorldPosition));

	public void Hide()
	{
		if (m_worldMapIconGameObject)
		{
			m_worldMapIconGameObject.SetActive(false);
		}
		if (m_areaMapIconGameObject)
		{
			m_areaMapIconGameObject.SetActive(false);
		}
	}

	public void Highlight()
	{
		if (m_worldMapIconHighlightAnimator)
		{
			m_worldMapIconHighlightAnimator.AnimatorDriver.ContinueForward();
		}
		if (m_areaMapIconHighlightAnimator)
		{
			m_areaMapIconHighlightAnimator.AnimatorDriver.ContinueForward();
		}
	}

	public void Dehighlight()
	{
		if (m_worldMapIconHighlightAnimator)
		{
			m_worldMapIconHighlightAnimator.AnimatorDriver.ContinueBackwards();
		}
		if (m_areaMapIconHighlightAnimator)
		{
			m_areaMapIconHighlightAnimator.AnimatorDriver.ContinueBackwards();
		}
	}

    public GameMapTeleporter(string name, float x, float y)
	{
		Identifier = name;
		WorldPosition = new Vector3(x, y, 0f);
		Activated = false;
		var randomizerMessageProvider = (RandomizerMessageProvider)ScriptableObject.CreateInstance(typeof(RandomizerMessageProvider));
		randomizerMessageProvider.SetMessage(name);
		Name = randomizerMessageProvider;
	}

	public GameMapTeleporter(string name, Vector3 position, bool activated)
	{
		Identifier = name;
		WorldPosition = position;
		Activated = activated;
		var randomizerMessageProvider = (RandomizerMessageProvider)ScriptableObject.CreateInstance(typeof(RandomizerMessageProvider));
		randomizerMessageProvider.SetMessage(name);
		Name = randomizerMessageProvider;
	}

	public void SetInfo(string name, Vector3 position, bool activated)
	{
		if (Identifier != name) {
			Identifier = name;
			var randomizerMessageProvider = (RandomizerMessageProvider)ScriptableObject.CreateInstance(typeof(RandomizerMessageProvider));
			randomizerMessageProvider.SetMessage(name);
			Name = randomizerMessageProvider;
		}
		WorldPosition = position;
		Activated = activated;
	}

	public string Identifier;

	public Vector3 WorldPosition;

	public bool Activated;

	public MessageProvider Name;

	private TransparencyAnimator m_worldMapIconHighlightAnimator;

	private Transform m_worldMapIconTransform;

	private GameObject m_worldMapIconGameObject;

	private TransparencyAnimator m_areaMapIconHighlightAnimator;

	private Transform m_areaMapIconTransform;

	private GameObject m_areaMapIconGameObject;
}
