using CommunityToolkit.Mvvm.ComponentModel;

namespace EDM.ViewModels
{
    /// <summary>
    /// Base class for all ViewModels in the EDM application.
    /// Provides a centralized foundation for MVVM property change notification using CommunityToolkit.
    /// 
    /// Inheriting from this class automatically provides:
    /// - INotifyPropertyChanged implementation
    /// - INotifyPropertyChanging implementation
    /// - SetProperty() method for property setters
    /// - OnPropertyChanged() method for manual notifications
    /// 
    /// Usage:
    /// public partial class MyViewModel : ViewModelBase
    /// {
    ///     [ObservableProperty]
    ///     private string myProperty = "default value";
    /// }
    /// 
    /// The [ObservableProperty] attribute automatically generates public property with change notification.
    /// </summary>
    public abstract class ViewModelBase : ObservableObject
    {
        /// <summary>
        /// Optional display name for the ViewModel (useful for logging and debugging).
        /// </summary>
        public virtual string DisplayName { get; set; } = nameof(ViewModelBase);
    }
}
