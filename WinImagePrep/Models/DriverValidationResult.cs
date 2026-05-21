using System;
using System.Collections.Generic;

namespace WinImagePrep.Models
{
    public class DriverValidationResult
    {
        public bool IsValid { get; set; }
        public int DriverCount { get; set; }
        public int SignedCount { get; set; }
        public int UnsignedCount { get; set; }
        public List<DriverInfo> Drivers { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    public class DriverInfo
    {
        public string Path { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsSigned { get; set; }
        public string DriverClass { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
    }
}
