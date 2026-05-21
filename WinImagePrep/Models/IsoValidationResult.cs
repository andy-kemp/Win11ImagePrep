using System;

namespace WinImagePrep.Models
{
    public class IsoValidationResult
    {
        public bool IsValid { get; set; }
        public bool HasBootWim { get; set; }
        public bool HasInstallWim { get; set; }
        public string Message { get; set; } = string.Empty;
        public string IsoPath { get; set; } = string.Empty;
        public string MountedDriveLetter { get; set; } = string.Empty;
    }
}
