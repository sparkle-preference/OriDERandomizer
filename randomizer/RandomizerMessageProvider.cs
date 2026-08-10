using System.Collections.Generic;
using System.Diagnostics;

public class RandomizerMessageProvider : MessageProvider {
    public RandomizerMessageProvider() {
        messages = new MessageDescriptor[1];
    }

    public RandomizerMessageProvider(string message) {
        messages = new MessageDescriptor[1];
        messages[0] = new MessageDescriptor(message);
    }

    [DebuggerHidden]
    public override IEnumerable<MessageDescriptor> GetMessages() {
        return messages;
    }

    public void SetMessage(string message) {
        messages[0] = new MessageDescriptor(message);
    }

    public MessageDescriptor[] messages;
}
