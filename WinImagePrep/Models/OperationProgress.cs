using System;

namespace WinImagePrep.Models
{
    public class OperationProgress
    {
        public int PercentComplete { get; set; }
        public string CurrentOperation { get; set; } = string.Empty;
        public string StatusMessage { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsIndeterminate { get; set; }
        public OperationStage Stage { get; set; }
    }

    public enum OperationStage
    {
        Initializing,
        ValidatingISO,
        ExtractingISO,
        ExtractingDrivers,
        InjectingDriversWinPE,
        InjectingDriversSetup,
        InjectingDriversInstall,
        InjectingDriversWinRE,
        SplittingWim,
        CreatingUSB,
        Finalizing,
        Complete,
        Error,
        Cancelled
    }
}
