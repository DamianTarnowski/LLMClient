using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WPFLLM.Models;
using WPFLLM.ViewModels;

namespace WPFLLM.Views;

public partial class ChatView : UserControl
{
    private bool _sidebarCollapsed = false;

    public ChatView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChatViewModel vm)
        {
            vm.Messages.CollectionChanged += OnMessagesChanged;
        }
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            MessagesScrollViewer.ScrollToEnd();
        });
    }

    private void ToggleSidebar_Click(object sender, MouseButtonEventArgs e)
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        
        if (_sidebarCollapsed)
        {
            SidebarColumn.Width = new GridLength(0);
            Sidebar.Visibility = Visibility.Collapsed;
            ToggleIcon.Text = "▶";
            ToggleIcon.ToolTip = "Pokaż konwersacje";
        }
        else
        {
            SidebarColumn.Width = new GridLength(280);
            Sidebar.Visibility = Visibility.Visible;
            ToggleIcon.Text = "◀";
            ToggleIcon.ToolTip = "Ukryj konwersacje";
        }
    }

    private void SearchResult_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && 
            element.DataContext is SearchResultViewModel result &&
            DataContext is ChatViewModel vm)
        {
            vm.GoToSearchResultCommand.Execute(result);
        }
    }

    private void ConversationTitle_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement element && element.DataContext is Conversation conv)
        {
            conv.IsEditing = true;
        }
    }

    private void ConversationTitleEdit_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is Conversation conv)
        {
            if (e.Key == Key.Enter)
            {
                conv.IsEditing = false;
                SaveConversationTitle(conv);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                conv.IsEditing = false;
                e.Handled = true;
            }
        }
    }

    private void ConversationTitleEdit_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is Conversation conv && conv.IsEditing)
        {
            conv.IsEditing = false;
            SaveConversationTitle(conv);
        }
    }

    private async void SaveConversationTitle(Conversation conv)
    {
        if (DataContext is ChatViewModel vm)
        {
            await vm.UpdateConversationTitleCommand.ExecuteAsync(conv);
        }
    }
}
