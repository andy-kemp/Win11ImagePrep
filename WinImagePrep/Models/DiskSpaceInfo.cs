using System;

namespace WinImagePrep.Models
{
    public class DiskSpaceInfo
    {
        public bool HasEnoughSpace { get; set; }
        public double FreeSpaceGB { get; set; }
        public double RequiredGB { get; set; }
        public string DriveLetter { get; set; } = string.Empty;
    }
}
