using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class GameWorldArea : MonoBehaviour
{
	private const float PIXELS_PER_UNIT = 5f;
	public List<GameWorldArea.WorldMapIcon> Icons = new List<GameWorldArea.WorldMapIcon>();
	public MessageProvider AreaName;
	public MessageProvider LowerAreaName;
	public string AreaNameString;
	public CageStructureTool CageStructureTool;
	public Transform BoundingTransform;
	public Texture WorldMapTexture;
	public string AreaIdentifier = string.Empty;
	public CageStructureTool BoundaryCage;
	public Condition VisitableCondition;

	public Bounds Bounds
	{
		get => new Bounds(this.BoundingTransform.position, this.BoundingTransform.localScale);
	}

	public Rect BoundingRect
	{
		get
		{
			return new Rect()
			{
				width = this.BoundingTransform.lossyScale.x,
				height = this.BoundingTransform.lossyScale.y,
				center = (Vector2) this.BoundingTransform.position
			};
		}
	}

	public bool InsideFace(Vector3 worldPosition)
	{
		return this.BoundaryCage.FindFaceAtPositionFaster(this.BoundaryCage.transform.InverseTransformPoint(worldPosition)) != null;
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
			this.Guid = new MoonGuid(worldMapIcon.Guid);
			this.Position = worldMapIcon.Position;
			this.Icon = worldMapIcon.Icon;
			this.IsSecret = worldMapIcon.IsSecret;
		}

		public WorldMapIcon()
		{
		}
	}
}
