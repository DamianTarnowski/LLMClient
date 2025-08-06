using Microsoft.Maui.Controls;
using System.Windows.Input;

namespace LLMClient.Behaviors
{
    public class EditorKeyboardBehavior : Behavior<Editor>
    {
        public static readonly BindableProperty SendCommandProperty = BindableProperty.Create(
            nameof(SendCommand), typeof(ICommand), typeof(EditorKeyboardBehavior));

        public ICommand SendCommand
        {
            get => (ICommand)GetValue(SendCommandProperty);
            set => SetValue(SendCommandProperty, value);
        }

        private Editor? _editor;

        protected override void OnAttachedTo(Editor bindable)
        {
            _editor = bindable;
            bindable.HandlerChanged += OnHandlerChanged;
            base.OnAttachedTo(bindable);
        }

        protected override void OnDetachingFrom(Editor bindable)
        {
            bindable.HandlerChanged -= OnHandlerChanged;
            _editor = null;
            base.OnDetachingFrom(bindable);
        }

        private void OnHandlerChanged(object? sender, EventArgs e)
        {
            if (_editor?.Handler != null)
            {
                SetupPlatformKeyboardHandlers();
            }
        }

        private void SetupPlatformKeyboardHandlers()
        {
#if ANDROID
            SetupAndroidKeyboardHandlers();
#elif IOS
            SetupIOSKeyboardHandlers();
#elif WINDOWS
            SetupWindowsKeyboardHandlers();
#elif MACCATALYST
            SetupMacCatalystKeyboardHandlers();
#endif
        }

#if ANDROID
        private void SetupAndroidKeyboardHandlers()
        {
            if (_editor?.Handler?.PlatformView is Android.Widget.EditText editText)
            {
                editText.KeyPress += (sender, e) =>
                {
                    if (e.KeyCode == Android.Views.Keycode.Enter)
                    {
                        if (e.Event?.HasModifiers(Android.Views.MetaKeyStates.CtrlOn) == true)
                        {
                            // Ctrl+Enter - insert new line
                            var currentText = _editor.Text ?? "";
                            var cursorPosition = _editor.CursorPosition;
                            var newText = currentText.Insert(cursorPosition, Environment.NewLine);
                            _editor.Text = newText;
                            _editor.CursorPosition = cursorPosition + Environment.NewLine.Length;
                            e.Handled = true;
                        }
                        else
                        {
                            // Enter - send message
                            SendCommand?.Execute(null);
                            e.Handled = true;
                        }
                    }
                };
            }
        }
#endif

#if IOS
        private void SetupIOSKeyboardHandlers()
        {
            if (_editor?.Handler?.PlatformView is UIKit.UITextView textView)
            {
                textView.ShouldChangeText = (textView, range, text) =>
                {
                    if (text == "\n")
                    {
                        // Check if Ctrl key is pressed (this is complex on iOS)
                        // For now, let's use a simple approach
                        SendCommand?.Execute(null);
                        return false; // Don't insert the newline
                    }
                    return true;
                };
            }
        }
#endif

#if WINDOWS
        private void SetupWindowsKeyboardHandlers()
        {
            if (_editor?.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox textBox)
            {
                textBox.KeyDown += (sender, e) =>
                {
                    if (e.Key == Windows.System.VirtualKey.Enter)
                    {
                        var ctrlPressed = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
                        
                        if (ctrlPressed)
                        {
                            // Ctrl+Enter - insert new line
                            var currentText = _editor.Text ?? "";
                            var cursorPosition = _editor.CursorPosition;
                            var newText = currentText.Insert(cursorPosition, Environment.NewLine);
                            _editor.Text = newText;
                            _editor.CursorPosition = cursorPosition + Environment.NewLine.Length;
                            e.Handled = true;
                        }
                        else
                        {
                            // Enter - send message
                            SendCommand?.Execute(null);
                            e.Handled = true;
                        }
                    }
                };
            }
        }
#endif

#if MACCATALYST
        private void SetupMacCatalystKeyboardHandlers()
        {
            if (_editor?.Handler?.PlatformView is UIKit.UITextView textView)
            {
                textView.ShouldChangeText = (textView, range, text) =>
                {
                    if (text == "\n")
                    {
                        // For Mac Catalyst, we'll implement similar logic to iOS
                        SendCommand?.Execute(null);
                        return false;
                    }
                    return true;
                };
            }
        }
#endif
    }
} 