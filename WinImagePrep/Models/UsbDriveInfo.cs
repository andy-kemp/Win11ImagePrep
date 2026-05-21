using System;

namespace WinImagePrep.Models
{
    public class UsbDriveInfo
    {
        public uint DiskNumber { get; set; }
        public string FriendlyName { get; set; } = string.Empty;
        public ulong SizeBytes { get; set; }
        public double SizeGB => Math.Round(SizeBytes / 1024.0 / 1024.0 / 1024.0, 2);
        public string BusType { get; set; } = string.Empty;
        public string InterfaceType { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public string PartitionStyle { get; set; } = string.Empty;
        public string FileSystem { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool IsRemovable { get; set; }
        public string OperationalStatus { get; set; } = string.Empty;

        public string DisplayName => $"{DiskNumber}: {FriendlyName} - {SizeGB} GB";

        public override string ToString() => DisplayName;
    }
}
