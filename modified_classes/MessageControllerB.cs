using Game;
using UnityEngine;

public class MessageControllerB : MonoBehaviour
{
	public bool AnyAbilityPickupStoryMessagesVisible => m_currentMessageBox;

	public GameObject ShowMessageBox(GameObject messageBoxPrefab, MessageProvider messageProvider, Vector3 position, float duration = 3f)
	{
		if (messageProvider == null)
		{
			return null;
		}
		if (SeinUI.DebugHideUI)
		{
			return null;
		}
		var expr_25 = InstantiateUtility.Instantiate(messageBoxPrefab, position, Quaternion.identity) as GameObject;
		var componentInChildren = expr_25.GetComponentInChildren<MessageBox>();
		if (componentInChildren.Visibility)
		{
			componentInChildren.SetWaitDuration(duration);
		}
		componentInChildren.SetMessageProvider(messageProvider);
		return expr_25;
	}

	public MessageBox ShowHintMessage(MessageProvider messageProvider, Vector3 position, float duration = 3f)
	{
		var gameObject = ShowMessageBox(HintMessage, messageProvider, position, duration);
		return !gameObject ? null : gameObject.GetComponentInChildren<MessageBox>();
	}

	public MessageBox ShowMessageBoxB(GameObject messageBoxPrefab, MessageProvider messageProvider, Vector3 position, float duration = 3f)
	{
		if (!Characters.Sein.IsSuspended)
		{
			return null;
		}
		var gameObject = ShowMessageBox(messageBoxPrefab, messageProvider, position, duration);
		if (gameObject)
		{
			return gameObject.GetComponentInChildren<MessageBox>();
		}
		return null;
	}

	public MessageBox ShowAreaMessage(MessageProvider messageProvider)
	{
		m_currentMessageBox = ShowMessageBoxB(AreaMessage, messageProvider, Vector3.zero);
		return m_currentMessageBox;
	}

	public MessageBox ShowAbilityMessage(MessageProvider messageProvider, GameObject avatar)
	{
		UI.Hints.HideExistingHint();
		var messageBox = ShowMessageBoxB(AbilityMessage, messageProvider, new Vector3(0f, 2f), float.PositiveInfinity);
		if (messageBox && avatar)
		{
			messageBox.SetAvatar(avatar);
		}
		m_currentMessageBox = messageBox;
		return messageBox;
	}

	public MessageBox ShowPickupMessage(MessageProvider messageProvider, GameObject avatar)
	{
		UI.Hints.HideExistingHint();
		var messageBox = ShowMessageBoxB(PickupMessage, messageProvider, new Vector3(0f, 2f), float.PositiveInfinity);
		if (messageBox && avatar)
		{
			messageBox.SetAvatar(avatar);
		}
		m_currentMessageBox = messageBox;
		return messageBox;
	}

	public MessageBox ShowStoryMessage(MessageProvider messageProvider)
	{
		UI.Hints.HideExistingHint();
		var messageBox = ShowMessageBoxB(StoryMessage, messageProvider, Vector3.zero, float.PositiveInfinity);
		m_currentMessageBox = messageBox;
		return messageBox;
	}

	public MessageBox ShowHelpMessage(MessageProvider messageProvider, GameObject avatar)
	{
		UI.Hints.HideExistingHint();
		var messageBox = ShowMessageBoxB(HelpMessage, messageProvider, Vector3.zero, float.PositiveInfinity);
		if (messageBox && avatar)
		{
			messageBox.SetAvatar(avatar);
		}
		return messageBox;
	}

	public GameObject ShowSpiritTreeTextMessage(MessageProvider messageProvider, Vector3 position)
	{
		return ShowMessageBox(SpiritTreeText, messageProvider, position, 0f);
	}

	public MessageBox ShowEnhancedSpiritFlameMessage(MessageProvider messageProvider)
	{
		UI.Hints.HideExistingHint();

		if (!RandomizerBonus.EnhancedSpiritFlame || RandomizerBonus.SuppressEnhancedSpiritFlame)
		{
			m_currentMessageBox = null;
		}
		else
		{
			var gameObject = ShowMessageBox(StoryMessage, messageProvider, Vector3.zero, float.PositiveInfinity);
			m_currentMessageBox = !gameObject ? null : gameObject.GetComponentInChildren<MessageBox>();
		}
		
		return m_currentMessageBox;
	}

	public float DefaultDuration;

	public GameObject AreaMessage;

	public GameObject AbilityMessage;

	public GameObject HintMessage;

	public GameObject PickupMessage;

	public GameObject StoryMessage;

	public GameObject HelpMessage;

	public GameObject SpiritTreeText;

	private MessageBox m_currentMessageBox;
}
