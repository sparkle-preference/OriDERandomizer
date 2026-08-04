using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class GameWorldArea : MonoBehaviour
{
	private const float PIXELS_PER_UNIT = 5f;
	public List<WorldMapIcon> Icons = new List<WorldMapIcon>();
	public MessageProvider AreaName;
	public MessageProvider LowerAreaName;
	public string AreaNameString;
	public CageStructureTool CageStructureTool;
	public Transform BoundingTransform;
	public Texture WorldMapTexture;
	public string AreaIdentifier = string.Empty;
	public CageStructureTool BoundaryCage;
	public Condition VisitableCondition;

	public Bounds Bounds => new Bounds(BoundingTransform.position, BoundingTransform.localScale);

	public Rect BoundingRect =>
		new Rect
		{
			width = BoundingTransform.lossyScale.x,
			height = BoundingTransform.lossyScale.y,
			center = BoundingTransform.position
		};

	public bool InsideFace(Vector3 worldPosition)
	{
		return BoundaryCage.FindFaceAtPositionFaster(BoundaryCage.transform.InverseTransformPoint(worldPosition)) != null;
	}

	[Serializable]
	public class WorldMapIcon
	{
		public MoonGuid Guid;
		public WorldMapIconType Icon;
		public Vector2 Position;
		public bool IsSecret;

		public WorldMapIcon(SceneMetaData.WorldMapIcon worldMapIcon)
		{
			Guid = new MoonGuid(worldMapIcon.Guid);
			Position = worldMapIcon.Position;
			Icon = worldMapIcon.Icon;
			IsSecret = worldMapIcon.IsSecret;
		}

		public WorldMapIcon()
		{
		}
	}
}
