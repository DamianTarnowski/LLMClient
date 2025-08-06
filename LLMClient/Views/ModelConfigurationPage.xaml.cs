using LLMClient.ViewModels;

namespace LLMClient.Views
{
    public partial class ModelConfigurationPage : ContentPage
    {
        public ModelConfigurationPage(ModelConfigurationViewModel viewModel)
        {
            InitializeComponent();
            BindingContext = viewModel;
            
            // Subscribe to changes in SelectedModelForEdit for mobile navigation
            if (viewModel != null)
            {
                viewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ModelConfigurationViewModel.SelectedModelForEdit))
            {
                UpdateMobileLayout();
            }
        }

        private void UpdateMobileLayout()
        {
            // Na mobile (screen width < 700) zarządzaj widocznością paneli
            if (Width < 700 && Width > 0) // Width > 0 zapewnia że layout jest zainicjalizowany
            {
                var hasSelectedModel = (BindingContext as ModelConfigurationViewModel)?.SelectedModelForEdit != null;
                
                // Na mobile: pokaż tylko jeden panel na raz
                ModelsPanel.IsVisible = !hasSelectedModel;
                FormPanel.IsVisible = hasSelectedModel;
                MobileHeader.IsVisible = hasSelectedModel;
            }
            else
            {
                // Na desktop/tablet: pokaż oba panele
                ModelsPanel.IsVisible = true;
                FormPanel.IsVisible = true;
                MobileHeader.IsVisible = false;
            }
        }

        private void MobileBackButton_Clicked(object sender, EventArgs e)
        {
            // Na mobile: wróć do listy modeli
            if (BindingContext is ModelConfigurationViewModel viewModel)
            {
                viewModel.SelectedModelForEdit = null;
            }
        }

        protected override void OnSizeAllocated(double width, double height)
        {
            base.OnSizeAllocated(width, height);
            UpdateMobileLayout();
        }
    }
}