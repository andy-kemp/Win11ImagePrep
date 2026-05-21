using System;

namespace WinImagePrep.Models
{
    public class WimEdition
    {
        public int ImageIndex { get; set; }
        public string ImageName { get; set; } = string.Empty;
        public ulong ImageSize { get; set; }
        public string ImageSizeDisplay => FormatBytes(ImageSize);
        public string Description { get; set; } = string.Empty;
        public bool IsSelected { get; set; }

        private static string FormatBytes(ulong bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public override string ToString() => $"{ImageIndex}: {ImageName} ({ImageSizeDisplay})";
    }
}
