using System.Collections.Generic;
using System.Linq;

namespace WinImagePrep.Models
{
    /// <summary>
    /// Result of settings validation containing success status and messages
    /// </summary>
    public class SettingsValidationResult
    {
        /// <summary>
        /// Whether the validation passed
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Collection of error messages (blocking issues)
        /// </summary>
        public List<string> Errors { get; set; } = new List<string>();

        /// <summary>
        /// Collection of warning messages (non-blocking issues)
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>
        /// Collection of informational messages
        /// </summary>
        public List<string> Info { get; set; } = new List<string>();

        /// <summary>
        /// Whether there are any errors
        /// </summary>
        public bool HasErrors => Errors.Any();

        /// <summary>
        /// Whether there are any warnings
        /// </summary>
        public bool HasWarnings => Warnings.Any();

        /// <summary>
        /// Whether there are any messages at all
        /// </summary>
        public bool HasMessages => HasErrors || HasWarnings || Info.Any();

        /// <summary>
        /// Gets a formatted string of all validation messages
        /// </summary>
        public string GetAllMessages()
        {
            var messages = new List<string>();

            if (HasErrors)
            {
                messages.Add("ERRORS:");
                messages.AddRange(Errors.Select(e => $"  ✗ {e}"));
                messages.Add("");
            }

            if (HasWarnings)
            {
                messages.Add("WARNINGS:");
                messages.AddRange(Warnings.Select(w => $"  ⚠ {w}"));
                messages.Add("");
            }

            if (Info.Any())
            {
                messages.Add("INFORMATION:");
                messages.AddRange(Info.Select(i => $"  ℹ {i}"));
            }

            return string.Join("\n", messages);
        }

        /// <summary>
        /// Gets a formatted string of errors only
        /// </summary>
        public string GetErrorMessages()
        {
            return HasErrors ? string.Join("\n", Errors.Select(e => $"✗ {e}")) : string.Empty;
        }

        /// <summary>
        /// Gets a formatted string of warnings only
        /// </summary>
        public string GetWarningMessages()
        {
            return HasWarnings ? string.Join("\n", Warnings.Select(w => $"⚠ {w}")) : string.Empty;
        }

        /// <summary>
        /// Adds an error message and marks validation as failed
        /// </summary>
        public void AddError(string message)
        {
            Errors.Add(message);
            IsValid = false;
        }

        /// <summary>
        /// Adds a warning message (doesn't affect IsValid)
        /// </summary>
        public void AddWarning(string message)
        {
            Warnings.Add(message);
        }

        /// <summary>
        /// Adds an informational message
        /// </summary>
        public void AddInfo(string message)
        {
            Info.Add(message);
        }

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        public static SettingsValidationResult Success()
        {
            return new SettingsValidationResult { IsValid = true };
        }

        /// <summary>
        /// Creates a failed validation result with an error message
        /// </summary>
        public static SettingsValidationResult Failure(string errorMessage)
        {
            var result = new SettingsValidationResult { IsValid = false };
            result.AddError(errorMessage);
            return result;
        }

        /// <summary>
        /// Creates a successful validation result with a warning
        /// </summary>
        public static SettingsValidationResult SuccessWithWarning(string warningMessage)
        {
            var result = new SettingsValidationResult { IsValid = true };
            result.AddWarning(warningMessage);
            return result;
        }
    }
}
