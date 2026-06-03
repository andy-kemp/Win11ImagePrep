using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace WinImagePrep.Models
{
    public class WindowsApp : INotifyPropertyChanged
    {
        private bool _isSelected;

        /// <summary>
        /// Primary package name (kept for backward compatibility)
        /// </summary>
        public string PackageName { get; set; } = string.Empty;

        /// <summary>
        /// All package names for this app (supports multiple architectures)
        /// </summary>
        public List<string> PackageNames { get; set; } = new List<string>();

        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// User-friendly architecture display (e.g., "x64, arm64")
        /// </summary>
        public string Architectures
        {
            get
            {
                if (!PackageNames.Any()) return "";

                var archs = new List<string>();
                foreach (var pkg in PackageNames)
                {
                    if (pkg.Contains("_x64_")) archs.Add("x64");
                    else if (pkg.Contains("_arm64_")) archs.Add("arm64");
                    else if (pkg.Contains("_neutral_")) archs.Add("neutral");
                }

                return archs.Any() ? string.Join(", ", archs.Distinct()) : "";
            }
        }

        /// <summary>
        /// Display text with architecture info
        /// </summary>
        public string DisplayNameWithArch
        {
            get
            {
                var arch = Architectures;
                return string.IsNullOrEmpty(arch) ? DisplayName : $"{DisplayName} ({arch})";
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
