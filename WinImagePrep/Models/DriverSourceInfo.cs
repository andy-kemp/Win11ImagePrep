using System.IO;

namespace WinImagePrep.Models
{
    /// <summary>
    /// Represents a driver source (MSI, ZIP, or folder)
    /// </summary>
    public class DriverSourceInfo
    {
        public string Path { get; set; } = string.Empty;
        public DriverSourceType Type { get; set; }

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(Path))
                    return string.Empty;

                return System.IO.Path.GetFileName(Path);
            }
        }

        public string TypeLabel
        {
            get
            {
                return Type switch
                {
                    DriverSourceType.Msi => "MSI",
                    DriverSourceType.Zip => "ZIP",
                    DriverSourceType.Folder => "Folder",
                    _ => "Unknown"
                };
            }
        }

        public string FullDisplayText
        {
            get
            {
                return $"{DisplayName} ({TypeLabel})";
            }
        }
    }
}
