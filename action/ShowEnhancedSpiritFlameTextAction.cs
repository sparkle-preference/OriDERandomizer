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
        messageProvider.messages = Messages;
        messageBox = UI.MessageController.ShowEnhancedSpiritFlameMessage(messageProvider);
        if (messageBox) {
            messageBox.OnMessageScreenHide += OnMessageScreenHide;
            Characters.Ori.StartTwinkle();
        } else if (FreezeGame) {
            SuspensionManager.ResumeAll();
        }
    }

    public void OnMessageScreenHide() {
        if (FreezeGame) {
            SuspensionManager.ResumeAll();
        }

        if (messageBox) {
            messageBox.OnMessageScreenHide -= OnMessageScreenHide;
        }

        Characters.Ori.StopTwinkle();
    }

    public override void Stop() {
    }

    public override bool IsPerforming => messageBox;

    public MessageDescriptor[] Messages;

    private MessageBox messageBox;

    public bool FreezeGame;
}
