using Game;
using UnityEngine;

public class Naru : MonoBehaviour, ICharacter
{
	public void Awake()
	{
		Characters.Naru = this;
		Characters.Current = this;
	}

	public void OnDestroy()
	{
		Randomizer.onNaruDestroyed();
		if (Characters.Naru == this)
		{
			Characters.Naru = null;
		}
		if (Characters.Current == this)
		{
			Characters.Current = null;
		}
	}

	public Vector3 Position
	{
		get
		{
			return transform.position;
		}
		set
		{
			transform.position = value;
		}
	}

	public void Activate(bool active)
	{
		gameObject.SetActive(active);
	}

	public GameObject GameObject
	{
		get
		{
			return gameObject;
		}
	}

	public bool FaceLeft
	{
		get
		{
			return Animation.SpriteMirror.FaceLeft;
		}
		set
		{
			Animation.SpriteMirror.FaceLeft = value;
		}
	}

	public Vector3 Speed
	{
		get
		{
			return PlatformBehaviour.PlatformMovement.LocalSpeed;
		}
		set
		{
			PlatformBehaviour.PlatformMovement.LocalSpeed = value;
		}
	}

	public Transform Transform
	{
		get
		{
			return transform;
		}
	}

	public bool IsOnGround
	{
		get
		{
			return PlatformBehaviour.PlatformMovement.IsOnGround;
		}
	}

	public void PlaceOnGround()
	{
		PlatformBehaviour.PlatformMovement.PlaceOnGround(0.5f, 0f);
	}

	public CharacterAnimationSystem Animation;

	public NaruController Controller;

	public PlatformBehaviour PlatformBehaviour;

	public bool SeinNaruComboEnabled;

	public NaruSounds Sounds;
}
