using CommunityToolkit.Mvvm.Messaging.Messages;

namespace LLMClient.Messaging
{
    // Simple notifications
    public sealed class LocalModelLoadedMessage : ValueChangedMessage<bool>
    {
        public LocalModelLoadedMessage() : base(true) {}
    }

    public sealed class LocalModelUnloadedMessage : ValueChangedMessage<bool>
    {
        public LocalModelUnloadedMessage() : base(true) {}
    }

    // State change
    public sealed class LocalModelActiveChangedMessage : ValueChangedMessage<bool>
    {
        public LocalModelActiveChangedMessage(bool isActive) : base(isActive) {}
    }

    // UI scrolling
    public sealed class ScrollToBottomMessage : ValueChangedMessage<bool>
    {
        public ScrollToBottomMessage() : base(true) {}
    }

    public sealed class ScrollToMessageMessage : ValueChangedMessage<object>
    {
        public ScrollToMessageMessage(object message) : base(message) {}
    }

    // Models list changed
    public sealed class ModelsChangedMessage : ValueChangedMessage<bool>
    {
        public ModelsChangedMessage() : base(true) {}
    }

    // GGUF Model selection changed (LLamaSharp)
    public sealed class GgufModelSelectedMessage : ValueChangedMessage<string>
    {
        public GgufModelSelectedMessage(string modelId) : base(modelId) {}
    }
}
