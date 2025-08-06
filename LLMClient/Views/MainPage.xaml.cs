using LLMClient.ViewModels;

namespace LLMClient;

public partial class MainPage : ContentPage
{
    public MainPage(MainPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Subscribe to scroll messages
        MessagingCenter.Subscribe<MainPageViewModel>(this, "ScrollToBottom", (sender) =>
        {
            ScrollToBottom();
        });

        MessagingCenter.Subscribe<MainPageViewModel, object>(this, "ScrollToMessage", (sender, message) =>
        {
            ScrollToMessage(message);
        });

        // Keyboard handling is now done via EditorKeyboardBehavior
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ScrollToBottom();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        MessagingCenter.Unsubscribe<MainPageViewModel>(this, "ScrollToBottom");
        MessagingCenter.Unsubscribe<MainPageViewModel, object>(this, "ScrollToMessage");
    }

    private void ScrollToBottom()
    {
        if (BindingContext is MainPageViewModel viewModel &&
            viewModel.SelectedConversation?.Messages?.Count > 0)
        {
            // Ensure the UI has updated before scrolling
            Dispatcher.Dispatch(async () =>
            {
                await Task.Delay(100); // Small delay to allow UI to render new messages
                MessagesScrollView.ScrollToAsync(0, MessagesScrollView.ContentSize.Height, false);
            });
        }
    }

    private void ScrollToMessage(object message)
    {
        if (message != null)
        {
            Dispatcher.Dispatch(async () =>
            {
                // Wait for UI to be updated and check if item exists in collection
                await Task.Delay(200);
                
                try
                {
                    // Verify the message exists in the current FilteredMessages collection
                    if (BindingContext is MainPageViewModel viewModel)
                    {
                        var messageExists = viewModel.FilteredMessages.Contains(message);
                        if (messageExists)
                        {
                            MessagesCollectionView.ScrollTo(message, position: ScrollToPosition.Center, animate: true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Fallback - try scrolling without verification
                    try
                    {
                        MessagesCollectionView.ScrollTo(message, position: ScrollToPosition.Center, animate: true);
                    }
                    catch
                    {
                        // If all else fails, ignore the error
                    }
                }
            });
        }
    }

    private void MessageEntry_Completed(object sender, EventArgs e)
    {
        if (BindingContext is MainPageViewModel viewModel)
        {
            viewModel.SendMessageCommand.Execute(null);
        }
    }



    private void HamburgerButton_Clicked(object sender, EventArgs e)
    {
        // Show conversations overlay on mobile
        if (FindByName("ConversationsOverlay") is Border overlay)
        {
            overlay.IsVisible = true;
        }
    }

    private void CloseConversationsOverlay_Clicked(object sender, EventArgs e)
    {
        // Hide conversations overlay on mobile
        if (FindByName("ConversationsOverlay") is Border overlay)
        {
            overlay.IsVisible = false;
        }
    }

    private void ConversationSelected_Tapped(object sender, TappedEventArgs e)
    {
        // Hide overlay after selecting a conversation on mobile
        if (FindByName("ConversationsOverlay") is Border overlay)
        {
            overlay.IsVisible = false;
        }
    }

    private void NewConversation_Clicked(object sender, EventArgs e)
    {
        // Hide overlay after creating new conversation on mobile
        if (FindByName("ConversationsOverlay") is Border overlay)
        {
            overlay.IsVisible = false;
        }
    }
}