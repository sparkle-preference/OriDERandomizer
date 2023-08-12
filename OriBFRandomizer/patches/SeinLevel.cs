using Game;
using UnityEngine;

namespace OriBFRandomizer.patches
{
	public static class SeinLevelExt
	{
		public static void AttemptInstantiateLevelUp(this SeinLevel _this)
		{
			if (_this.OnLevelUpGameObject)
			{
				((GameObject)InstantiateUtility.Instantiate(_this.OnLevelUpGameObject, Characters.Sein.Position, Quaternion.identity)).GetComponent<TargetPositionFollower>().Target = Characters.Sein.Transform;
			}
		}
	}
}
