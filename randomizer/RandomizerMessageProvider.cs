using System.Collections.Generic;
using System.Diagnostics;

public class RandomizerMessageProvider : MessageProvider {
    public RandomizerMessageProvider() {
        Messages = new MessageDescriptor[1];
    }

    public RandomizerMessageProvider(string message) {
        Messages = new MessageDescriptor[1];
        Messages[0] = new MessageDescriptor(message);
    }

    [DebuggerHidden]
    public override IEnumerable<MessageDescriptor> GetMessages() {
        return Messages;
    }

    public void SetMessage(string message) {
        Messages[0] = new MessageDescriptor(message);
    }

    public MessageDescriptor[] Messages;
}
