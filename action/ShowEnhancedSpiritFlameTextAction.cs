using System;
using Game;
using UnityEngine;

public class ShowEnhancedSpiritFlameTextAction : PerformingAction
{
	public override void Perform(IContext context)
	{
		if (this.FreezeGame)
		{
			SuspensionManager.SuspendAll();
		}
		if (this.StoryMessage == null)
		{
			return;
		}
		RandomizerMessageProvider messageProvider = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
		messageProvider.messages = this.Messages;
		this.m_messageBox = UI.MessageController.ShowEnhancedSpiritFlameMessage(messageProvider);
		if (this.m_messageBox)
		{
			this.m_messageBox.OnMessageScreenHide += this.OnMessageScreenHide;
			Characters.Ori.StartTwinkle();
		}
		else if (this.FreezeGame)
		{
			SuspensionManager.ResumeAll();
		}
	}

	public void OnMessageScreenHide()
	{
		if (this.FreezeGame)
		{
			SuspensionManager.ResumeAll();
		}
		if (this.m_messageBox)
		{
			this.m_messageBox.OnMessageScreenHide -= this.OnMessageScreenHide;
		}
		Characters.Ori.StopTwinkle();
	}

	public override void Stop()
	{
	}

	public override bool IsPerforming
	{
		get
		{
			return this.m_messageBox;
		}
	}

	public MessageDescriptor[] Messages;

	private MessageBox m_messageBox;

	public bool FreezeGame;
}
