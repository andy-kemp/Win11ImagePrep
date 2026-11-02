using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using WinImagePrep.Helpers;

namespace WinImagePrep.Services
{
    /// <summary>
    /// One prerequisite and whether it is met.
    /// </summary>
    public record PrerequisiteStatus(string Name, bool IsMet, string Detail, bool CanInstall = false);

    /// <summary>
    /// Checks what the app needs and installs the Windows ADK Deployment Tools (oscdimg) when asked.
    /// </summary>
    public class PrerequisiteService
    {
        /// <summary>
        /// Microsoft's ADK download link (ADK 10.1.28000.3481, October 2026). Any ADK version with
        /// Deployment Tools provides oscdimg, so this only needs changing if Microsoft retires the link;
        /// it can be overridden with AdkSetupUrl in settings.json.
        /// </summary>
        public const string DefaultAdkSetupUrl = "https://go.microsoft.com/fwlink/?linkid=2337875";
        public const string AdkDownloadPage = "https://learn.microsoft.com/windows-hardware/get-started/adk-install";

        private readonly IsoService _isoService;

        public PrerequisiteService(IsoService isoService)
        {
            _isoService = isoService;
        }

        public IReadOnlyList<PrerequisiteStatus> Check()
        {
            var list = new List<PrerequisiteStatus>();

            var dism = Path.Combine(Environment.SystemDirectory, "dism.exe");
            list.Add(new PrerequisiteStatus("DISM", File.Exists(dism),
                File.Exists(dism) ? "Built into Windows" : "dism.exe not found in System32"));

            var oscdimg = _isoService.FindOscdimgPath();
            list.Add(new PrerequisiteStatus("Windows ADK Deployment Tools (for ISO files)", oscdimg != null,
                oscdimg ?? "Not installed. Needed only to create ISO files; USB creation works without it.",
                CanInstall: oscdimg == null));

            var admin = AdminHelper.IsRunningAsAdministrator();
            list.Add(new PrerequisiteStatus("Administrator rights", true,
                admin ? "Running as administrator" : "Asked for when a build starts"));

            return list;
        }

        /// <summary>
        /// Download Microsoft's ADK setup, check it is signed by Microsoft, and install only the
        /// Deployment Tools silently. Windows asks for administrator rights if the app isn't elevated.
        /// Returns the path to oscdimg.exe, or null if it isn't there afterwards.
        /// </summary>
        public async Task<string?> InstallAdkDeploymentToolsAsync(
            string? setupUrl,
            IProgress<string>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var url = string.IsNullOrWhiteSpace(setupUrl) ? DefaultAdkSetupUrl : setupUrl!;
            var workDir = Path.Combine(Path.GetTempPath(), "Win11ImagePrep-ADK");
            Directory.CreateDirectory(workDir);
            var setupPath = Path.Combine(workDir, "adksetup.exe");
            var logPath = Path.Combine(workDir, "adksetup.log");

            progress?.Report("Downloading Windows ADK setup from Microsoft...");
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Win11ImagePrep");
                using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var fs = File.Create(setupPath);
                await response.Content.CopyToAsync(fs, cancellationToken);
            }
            progress?.Report($"✓ Downloaded adksetup.exe ({new FileInfo(setupPath).Length / 1024} KB)");

            // Only run it if Windows trusts the signature and the signer is Microsoft
            var signer = VerifyMicrosoftSignature(setupPath);
            progress?.Report($"✓ Signature verified: {signer}");

            progress?.Report("Installing Deployment Tools (a few minutes; Windows may ask for permission)...");
            var psi = new ProcessStartInfo
            {
                FileName = setupPath,
                Arguments = $"/quiet /norestart /ceip off /features OptionId.DeploymentTools /log \"{logPath}\"",
                UseShellExecute = true,          // lets Windows show the UAC prompt
                Verb = AdminHelper.IsRunningAsAdministrator() ? "" : "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process? process;
            try
            {
                process = Process.Start(psi);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                throw new OperationCanceledException("Administrator permission was declined.");
            }
            if (process == null) throw new InvalidOperationException("ADK setup did not start.");

            using (process)
            using (cancellationToken.Register(() => { try { process.Kill(true); } catch { } }))
            {
                await process.WaitForExitAsync(cancellationToken);
                // 0 = success, 3010 = success but a restart is pending (oscdimg still works)
                if (process.ExitCode != 0 && process.ExitCode != 3010)
                {
                    progress?.Report($"✗ ADK setup exited with code {process.ExitCode}. Log: {logPath}");
                    return null;
                }
            }

            var oscdimg = _isoService.FindOscdimgPath();
            progress?.Report(oscdimg != null
                ? $"✓ Deployment Tools installed: {oscdimg}"
                : $"✗ Setup finished but oscdimg.exe wasn't found. Log: {logPath}");
            return oscdimg;
        }

        // ------------------------------------------------------------------
        // Authenticode check: WinVerifyTrust for integrity and chain, then the signer's name
        // ------------------------------------------------------------------
        private static string VerifyMicrosoftSignature(string path)
        {
            var result = WinTrust.Verify(path);
            if (result != 0)
                throw new InvalidOperationException($"adksetup.exe failed signature verification (0x{result:X8}); it was not run.");

            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            var org = cert.GetNameInfo(X509NameType.SimpleName, false);
            if (!cert.Subject.Contains("O=Microsoft Corporation", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"adksetup.exe is signed by '{cert.Subject}', not Microsoft; it was not run.");
            return org;
        }

        private static class WinTrust
        {
            private static readonly Guid ActionGenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct WINTRUST_FILE_INFO
            {
                public uint cbStruct;
                public string pcwszFilePath;
                public IntPtr hFile;
                public IntPtr pgKnownSubject;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            private struct WINTRUST_DATA
            {
                public uint cbStruct;
                public IntPtr pPolicyCallbackData;
                public IntPtr pSIPClientData;
                public uint dwUIChoice;          // 2 = WTD_UI_NONE
                public uint fdwRevocationChecks; // 1 = WTD_REVOKE_WHOLECHAIN
                public uint dwUnionChoice;       // 1 = WTD_CHOICE_FILE
                public IntPtr pFile;
                public uint dwStateAction;       // 0 = WTD_STATEACTION_IGNORE
                public IntPtr hWVTStateData;
                public IntPtr pwszURLReference;
                public uint dwProvFlags;
                public uint dwUIContext;
                public IntPtr pSignatureSettings;
            }

            [DllImport("wintrust.dll", CharSet = CharSet.Unicode, SetLastError = false)]
            private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, ref WINTRUST_DATA data);

            public static int Verify(string path)
            {
                var file = new WINTRUST_FILE_INFO { cbStruct = (uint)Marshal.SizeOf<WINTRUST_FILE_INFO>(), pcwszFilePath = path };
                var filePtr = Marshal.AllocHGlobal(Marshal.SizeOf<WINTRUST_FILE_INFO>());
                try
                {
                    Marshal.StructureToPtr(file, filePtr, false);
                    var data = new WINTRUST_DATA
                    {
                        cbStruct = (uint)Marshal.SizeOf<WINTRUST_DATA>(),
                        dwUIChoice = 2,
                        fdwRevocationChecks = 1,
                        dwUnionChoice = 1,
                        pFile = filePtr
                    };
                    return WinVerifyTrust(IntPtr.Zero, ActionGenericVerifyV2, ref data);
                }
                finally
                {
                    Marshal.DestroyStructure<WINTRUST_FILE_INFO>(filePtr);
                    Marshal.FreeHGlobal(filePtr);
                }
            }
        }
    }
}
