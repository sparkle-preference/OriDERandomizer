using Game;
using UnityEngine;

public class ShowEnhancedSpiritFlameTextAction : PerformingAction {
    public override void Perform(IContext context) {
        if (FreezeGame) {
            SuspensionManager.SuspendAll();
        }

        if (Messages == null) {
            return;
        }

        var messageProvider = ScriptableObject.CreateInstance<RandomizerMessageProvider>();
        messageProvider.Messages = Messages;
        m_messageBox = UI.MessageController.ShowEnhancedSpiritFlameMessage(messageProvider);
        if (m_messageBox) {
            m_messageBox.OnMessageScreenHide += OnMessageScreenHide;
            Characters.Ori.StartTwinkle();
        } else if (FreezeGame) {
            SuspensionManager.ResumeAll();
        }
    }

    public void OnMessageScreenHide() {
        if (FreezeGame) {
            SuspensionManager.ResumeAll();
        }

        if (m_messageBox) {
            m_messageBox.OnMessageScreenHide -= OnMessageScreenHide;
        }

        Characters.Ori.StopTwinkle();
    }

    public override void Stop() {
    }

    public override bool IsPerforming => m_messageBox;

    public MessageDescriptor[] Messages;

    private MessageBox m_messageBox;

    public bool FreezeGame;
}
