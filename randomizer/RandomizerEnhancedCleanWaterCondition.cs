using System;
using Game;
using UnityEngine;

public class RandomizerEnhancedCleanWaterCondition : Condition
{
	public override bool Validate(IContext context)
	{
        return RandomizerBonus.EnhancedCleanWater;
	}
}
