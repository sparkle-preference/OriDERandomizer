using System;
using System.Collections.Generic;
using Game;
using UnityEngine;

public class SeinLevel : SaveSerialize, ISeinReceiver
{
	public int TotalExperience
	{
		get
		{
			return Experience + ConsumedExperience;
		}
	}

	public int TotalExperienceForNextLevel
	{
		get
		{
			return ExperienceForNextLevel + ConsumedExperience;
		}
	}

	public int ExperienceNeedForNextLevel
	{
		get
		{
			return ExperienceForNextLevel - Experience;
		}
	}

	public float ExperienceVisualMinNormalized
	{
		get
		{
			return ExperienceVisualMin / ExperienceForNextLevel;
		}
	}

	public float ExperienceVisualMaxNormalized
	{
		get
		{
			return ExperienceVisualMax / ExperienceForNextLevel;
		}
	}

	public int ExperienceForNextLevel
	{
		get
		{
			return Mathf.RoundToInt(ExperienceRequiredPerLevel.Evaluate(Current));
		}
	}

	public int ConsumedExperience
	{
		get
		{
			int num = 0;
			for (int i = Current - 1; i >= 0; i--)
			{
				num += Mathf.RoundToInt(ExperienceRequiredPerLevel.Evaluate(i));
			}
			return num;
		}
	}

	public void GainExperience(int amount)
	{
		Experience += amount;
		ExperienceVisualMax = Experience;
	}

	public void Update()
	{
	}

	public void FixedUpdate()
	{
		if (m_sein.IsSuspended)
		{
			return;
		}
		float maxDelta = Time.deltaTime * ExperienceGainPerSecond * ExperienceForNextLevel;
		ExperienceVisualMax = Mathf.MoveTowards(ExperienceVisualMax, Experience, maxDelta);
		ExperienceVisualMin = Mathf.MoveTowards(ExperienceVisualMin, Experience, maxDelta);
		if (ExperienceVisualMin >= ExperienceForNextLevel)
		{
			LevelUp();
		}
	}

	public void LevelUp()
	{
		Experience -= ExperienceForNextLevel;
		ExperienceVisualMin = 0f;
		ExperienceVisualMax = Experience;
		if (Current < 99)
		{
			Current++;
			SkillPoints++;
		}
		AttemptInstantiateLevelUp();
	}

	public void LoseExperience(int amount)
	{
		Experience -= amount;
		ExperienceVisualMin = Experience;
		if (Experience < 0)
		{
			Experience = 0;
		}
	}

	public override void Serialize(Archive ar)
	{
		ar.Serialize(ref Current);
		ar.Serialize(ref Experience);
		ar.Serialize(ref SkillPoints);
		ar.Serialize(ref HasSpentSkillPoint);
		if (ar.Reading)
		{
			ExperienceVisualMax = ExperienceVisualMin = Current;
		}
	}

	public float ApplyLevelingToDamage(float damage)
	{
		return damage + damage * m_sein.PlayerAbilities.OriStrength * 0.5f;
	}

	public float CalculateLevelBasedMaxHealth(int level, float health)
	{
		return Mathf.RoundToInt(health * DamageMultiplierPerOriStrength.Evaluate(level));
	}

	public void SetReferenceToSein(SeinCharacter sein)
	{
		m_sein = sein;
	}

	public void GainSkillPoint()
	{
		SkillPoints++;
	}

	public void AttemptInstantiateLevelUp()
	{
		if (OnLevelUpGameObject)
		{
			GameObject obj = (GameObject)InstantiateUtility.Instantiate(OnLevelUpGameObject, Characters.Sein.Position, Quaternion.identity);
			TargetPositionFollower target = obj.GetComponent<TargetPositionFollower>();
			target.Target = Characters.Sein.Transform;
		}
	}

	public int SkillPoints;

	public int Current;

	public AnimationCurve DamageMultiplierPerOriStrength;

	public int Experience;

	public float ExperienceVisualMin;

	public float ExperienceVisualMax;

	public AnimationCurve ExperienceRequiredPerLevel;

	public GameObject OnLevelUpGameObject;

	public static bool HasSpentSkillPoint;

	public float ExperienceGainPerSecond = 30f;

	private static readonly HashSet<string> CollectablesToSerialize = new HashSet<string>
	{
		"largeExpOrbPlaceholder",
		"mediumExpOrbPlaceholder",
		"smallExpOrbPlaceholder"
	};

	private static HashSet<Type> TypesToSerialize = new HashSet<Type>
	{
		typeof(ExpOrbPickup)
	};

	private SeinCharacter m_sein;
}
