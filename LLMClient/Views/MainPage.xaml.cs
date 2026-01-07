using System.Linq;
using LLMClient.ViewModels;
using LLMClient.Services;
using CommunityToolkit.Mvvm.Messaging;
using LLMClient.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace LLMClient;

public partial class MainPage : ContentPage
{
    private bool _initialized;

    public MainPage()
    {
        InitializeComponent();
    }

    public MainPage(MainPageViewModel viewModel, LocalModelStatusViewModel localModelStatusViewModel)
    {
        InitializeComponent();
        InitializePage(viewModel, localModelStatusViewModel);
        _initialized = true;
    }

    private void InitializePage(MainPageViewModel viewModel, LocalModelStatusViewModel localModelStatusViewModel)
    {
        BindingContext = viewModel;
        
        // Set up LocalModelStatusView
        LocalModelStatus.BindingContext = localModelStatusViewModel;

        // Subscribe to scroll messages via WeakReferenceMessenger
        WeakReferenceMessenger.Default.Register<ScrollToBottomMessage>(this, (r, m) =>
        {
            ScrollToBottom();
        });

        WeakReferenceMessenger.Default.Register<ScrollToMessageMessage>(this, (r, m) =>
        {
            ScrollToMessage(m.Value);
        });

        // Setup language menu
        SetupLanguageMenu();

        // Keyboard handling is now done via EditorKeyboardBehavior
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (!_initialized)
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            var mainVm = services?.GetService<MainPageViewModel>();
            var statusVm = services?.GetService<LocalModelStatusViewModel>();
            if (mainVm != null && statusVm != null)
            {
                InitializePage(mainVm, statusVm);
                _initialized = true;
            }
        }

        ScrollToBottom();
        
        // Check for onboarding - run on dispatcher to avoid blocking
        Dispatcher.DispatchAsync(async () => await CheckOnboardingAsync());
    }
    
    private bool _onboardingChecked = false;
    
    private async Task CheckOnboardingAsync()
    {
        // Prevent multiple checks
        if (_onboardingChecked) return;
        _onboardingChecked = true;
        
        try
        {
            // Check if this is first run
            var isFirstRun = !Preferences.ContainsKey("OnboardingCompleted");
            if (!isFirstRun) return; // Skip if already completed
            
            if (BindingContext is MainPageViewModel viewModel)
            {
                // Wait for UI and models to be ready
                await Task.Delay(1500);
                
                var hasNoModels = viewModel.AiConfiguration?.Models == null || 
                                  viewModel.AiConfiguration.Models.Count == 0;
                var hasNoSelectedModel = viewModel.AiConfiguration?.SelectedModel == null;
                
                // Only show onboarding if truly needed
                if (hasNoModels && hasNoSelectedModel)
                {
                    var result = await DisplayAlert(
                        "Witaj w LLMClient!",
                        "Aby rozpocząć rozmowę z AI, musisz skonfigurować model.\n\n" +
                        "Możesz użyć modelu lokalnego (Phi-4) który działa offline, " +
                        "lub skonfigurować API (OpenAI, Gemini, itp.).\n\n" +
                        "Co chcesz zrobić?",
                        "Skonfiguruj API",
                        "Użyj modelu lokalnego");
                    
                    if (result)
                    {
                        viewModel.SettingsCommand.Execute(null);
                    }
                    else
                    {
                        viewModel.EnableLocalModelCommand.Execute(null);
                    }
                }
                
                // Mark onboarding as completed
                Preferences.Set("OnboardingCompleted", true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainPage] Onboarding error: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        WeakReferenceMessenger.Default.Unregister<ScrollToBottomMessage>(this);
        WeakReferenceMessenger.Default.Unregister<ScrollToMessageMessage>(this);
    }

    private void ScrollToBottom()
    {
        if (BindingContext is MainPageViewModel viewModel &&
            viewModel.FilteredMessages?.Count > 0)
        {
            // Use CollectionView.ScrollTo for better performance and virtualization support
            _ = Dispatcher.DispatchAsync(async () =>
            {
                await Task.Delay(100); // Small delay to allow UI to render
                var lastItem = viewModel.FilteredMessages.LastOrDefault();
                if (lastItem != null)
                {
                    MessagesCollectionView.ScrollTo(lastItem, position: ScrollToPosition.End, animate: false);
                }
            });
        }
    }

    private void ScrollToMessage(object message)
    {
        if (message != null)
        {
            _ = Dispatcher.DispatchAsync(async () =>
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
                catch
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

    private void SetupLanguageMenu()
    {
        // Language menu is now handled by LanguageToolbarItem_Clicked
    }

    private async void MoreOptionsButton_Clicked(object sender, EventArgs e)
    {
        if (BindingContext is MainPageViewModel viewModel)
        {
            var result = await DisplayActionSheet(
                "Więcej opcji",
                "Anuluj",
                null,
                " Pamięć AI",
                " Ustawienia modeli",
                " Konfiguracja API",
                " Dokumenty RAG",
                " Pomoc",
                " Diagnostyka i testy");

            switch (result)
            {
                case " Pamięć AI":
                    viewModel.GoToMemoryCommand.Execute(null);
                    break;
                case " Ustawienia modeli":
                    viewModel.ModelSettingsCommand.Execute(null);
                    break;
                case " Konfiguracja API":
                    viewModel.SettingsCommand.Execute(null);
                    break;
                case " Dokumenty RAG":
                    viewModel.GoToRagCommand.Execute(null);
                    break;
                case " Pomoc":
                    await Shell.Current.GoToAsync(nameof(Views.HelpPage));
                    break;
                case " Diagnostyka i testy":
                    viewModel.GoToDiagnosticsCommand.Execute(null);
                    break;
            }
        }
    }

    private async void LanguageToolbarItem_Clicked(object sender, EventArgs e)
    {
        if (BindingContext is MainPageViewModel viewModel)
        {
            try
            {
                var languageNames = viewModel.AvailableLanguages.Select(l => l.NativeName).ToArray();
                var currentLanguage = viewModel.SelectedLanguage?.NativeName ?? "English";
                
                System.Diagnostics.Debug.WriteLine($"[MainPage] Current language: {currentLanguage}");
                System.Diagnostics.Debug.WriteLine($"[MainPage] Available languages: {string.Join(", ", languageNames)}");
                System.Diagnostics.Debug.WriteLine($"[MainPage] SelectLanguage text: {viewModel.L["SelectLanguage"]}");
                System.Diagnostics.Debug.WriteLine($"[MainPage] Cancel text: {viewModel.L["Cancel"]}");
                
                var result = await DisplayActionSheetAsync(
                    viewModel.L["SelectLanguage"],
                    viewModel.L["Cancel"],
                    null,
                    languageNames);
                    
                System.Diagnostics.Debug.WriteLine($"[MainPage] User selected: {result}");
                
                if (result != null && result != viewModel.L["Cancel"] && !string.IsNullOrEmpty(result))
                {
                    var selectedLanguage = viewModel.AvailableLanguages.FirstOrDefault(l => l.NativeName == result);
                    if (selectedLanguage != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainPage] Setting language to: {selectedLanguage.Code} ({selectedLanguage.NativeName})");
                        viewModel.SelectedLanguage = selectedLanguage;
                        System.Diagnostics.Debug.WriteLine($"[MainPage] Language set successfully");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainPage] Could not find language for result: {result}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainPage] Error in language selection: {ex.Message}");
                await DisplayAlertAsync("Error", $"Error changing language: {ex.Message}", "OK");
            }
        }
    }
}