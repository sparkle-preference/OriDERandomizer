using UnityEngine;

public class TitleScreenPressStartLogic : MonoBehaviour
{
	public void FixedUpdate()
	{
		XboxLiveController.Instance.StartPressedOnMainMenu(OnStartPressedCallback);
	}

	public void OnStartPressedCallback()
	{
		GameStateMachine.Instance.SetToTitleScreen();
		OnPressed.Perform(null);
	}

	public ActionMethod OnPressed;
}
