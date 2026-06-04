using System;
using System.IO;
using System.Text;
using System.Xml;
using WinImagePrep.Helpers;
using WinImagePrep.Models;

namespace WinImagePrep.Services
{
    /// <summary>
    /// Service for generating unattended Windows installation answer files (autounattend.xml)
    /// </summary>
    public class UnattendedInstallService
    {
        /// <summary>
        /// Generates an autounattend.xml file based on the provided configuration
        /// </summary>
        /// <param name="config">Unattended installation configuration</param>
        /// <param name="outputPath">Full path where the autounattend.xml should be saved</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool GenerateAutounattendXml(UnattendedConfig config, string outputPath)
        {
            try
            {
                Logger.Info($"Generating autounattend.xml at: {outputPath}");

                var xml = BuildAutounattendXml(config);

                // Save with UTF-8 encoding
                File.WriteAllText(outputPath, xml, Encoding.UTF8);

                Logger.Info("✓ Autounattend.xml generated successfully");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to generate autounattend.xml: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Builds the complete autounattend.xml content
        /// </summary>
        private string BuildAutounattendXml(UnattendedConfig config)
        {
            var sb = new StringBuilder();

            sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            sb.AppendLine("<unattend xmlns=\"urn:schemas-microsoft-com:unattend\">");

            // Pass 1: windowsPE - Runs during Windows Setup before installation
            sb.AppendLine("    <settings pass=\"windowsPE\">");
            sb.AppendLine("        <component name=\"Microsoft-Windows-International-Core-WinPE\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
            sb.AppendLine($"            <SetupUILanguage>");
            sb.AppendLine($"                <UILanguage>{config.UILanguage}</UILanguage>");
            sb.AppendLine($"            </SetupUILanguage>");
            sb.AppendLine($"            <InputLocale>{config.InputLocale}</InputLocale>");
            sb.AppendLine($"            <SystemLocale>{config.SystemLocale}</SystemLocale>");
            sb.AppendLine($"            <UILanguage>{config.UILanguage}</UILanguage>");
            sb.AppendLine($"            <UserLocale>{config.UserLocale}</UserLocale>");
            sb.AppendLine("        </component>");

            // Windows Setup configuration
            sb.AppendLine("        <component name=\"Microsoft-Windows-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");

            // User data (accept EULA, etc.)
            sb.AppendLine("            <UserData>");
            sb.AppendLine($"                <AcceptEula>{config.HideEULA.ToString().ToLower()}</AcceptEula>");
            sb.AppendLine("                <FullName>User</FullName>");
            sb.AppendLine("                <Organization></Organization>");
            sb.AppendLine("            </UserData>");

            // Disk configuration - Auto-partition disk 0
            if (config.AutoPartitionDisk)
            {
                sb.AppendLine("            <DiskConfiguration>");
                sb.AppendLine("                <Disk wcm:action=\"add\">");
                sb.AppendLine($"                    <DiskID>{config.TargetDiskId}</DiskID>");
                sb.AppendLine("                    <WillWipeDisk>true</WillWipeDisk>");
                sb.AppendLine("                    <CreatePartitions>");

                // EFI System Partition (ESP) - 100MB
                sb.AppendLine("                        <CreatePartition wcm:action=\"add\">");
                sb.AppendLine("                            <Order>1</Order>");
                sb.AppendLine("                            <Type>EFI</Type>");
                sb.AppendLine("                            <Size>100</Size>");
                sb.AppendLine("                        </CreatePartition>");

                // MSR (Microsoft Reserved) Partition - 16MB
                sb.AppendLine("                        <CreatePartition wcm:action=\"add\">");
                sb.AppendLine("                            <Order>2</Order>");
                sb.AppendLine("                            <Type>MSR</Type>");
                sb.AppendLine("                            <Size>16</Size>");
                sb.AppendLine("                        </CreatePartition>");

                // Windows partition - Use remaining space
                sb.AppendLine("                        <CreatePartition wcm:action=\"add\">");
                sb.AppendLine("                            <Order>3</Order>");
                sb.AppendLine("                            <Type>Primary</Type>");
                sb.AppendLine("                            <Extend>true</Extend>");
                sb.AppendLine("                        </CreatePartition>");

                sb.AppendLine("                    </CreatePartitions>");
                sb.AppendLine("                    <ModifyPartitions>");

                // Format ESP
                sb.AppendLine("                        <ModifyPartition wcm:action=\"add\">");
                sb.AppendLine("                            <Order>1</Order>");
                sb.AppendLine("                            <PartitionID>1</PartitionID>");
                sb.AppendLine("                            <Label>System</Label>");
                sb.AppendLine("                            <Format>FAT32</Format>");
                sb.AppendLine("                        </ModifyPartition>");

                // MSR doesn't need formatting
                sb.AppendLine("                        <ModifyPartition wcm:action=\"add\">");
                sb.AppendLine("                            <Order>2</Order>");
                sb.AppendLine("                            <PartitionID>2</PartitionID>");
                sb.AppendLine("                        </ModifyPartition>");

                // Format Windows partition
                sb.AppendLine("                        <ModifyPartition wcm:action=\"add\">");
                sb.AppendLine("                            <Order>3</Order>");
                sb.AppendLine("                            <PartitionID>3</PartitionID>");
                sb.AppendLine("                            <Label>Windows</Label>");
                sb.AppendLine("                            <Letter>C</Letter>");
                sb.AppendLine("                            <Format>NTFS</Format>");
                sb.AppendLine("                        </ModifyPartition>");

                sb.AppendLine("                    </ModifyPartitions>");
                sb.AppendLine("                </Disk>");
                sb.AppendLine("            </DiskConfiguration>");

                // Image install - Install to partition 3 (Windows partition)
                sb.AppendLine("            <ImageInstall>");
                sb.AppendLine("                <OSImage>");
                sb.AppendLine("                    <InstallTo>");
                sb.AppendLine($"                        <DiskID>{config.TargetDiskId}</DiskID>");
                sb.AppendLine("                        <PartitionID>3</PartitionID>");
                sb.AppendLine("                    </InstallTo>");

                // Install specific edition if specified
                if (!string.IsNullOrWhiteSpace(config.TargetEdition))
                {
                    sb.AppendLine("                    <InstallToAvailablePartition>false</InstallToAvailablePartition>");
                    sb.AppendLine("                    <InstallFrom>");
                    sb.AppendLine("                        <MetaData wcm:action=\"add\">");
                    sb.AppendLine("                            <Key>/IMAGE/NAME</Key>");
                    sb.AppendLine($"                            <Value>{config.TargetEdition}</Value>");
                    sb.AppendLine("                        </MetaData>");
                    sb.AppendLine("                    </InstallFrom>");
                }

                sb.AppendLine("                </OSImage>");
                sb.AppendLine("            </ImageInstall>");
            }

            sb.AppendLine("        </component>");
            sb.AppendLine("    </settings>");

            // Pass 7: oobeSystem - Runs during OOBE (Out of Box Experience)
            sb.AppendLine("    <settings pass=\"oobeSystem\">");

            // International settings
            sb.AppendLine("        <component name=\"Microsoft-Windows-International-Core\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
            sb.AppendLine($"            <InputLocale>{config.InputLocale}</InputLocale>");
            sb.AppendLine($"            <SystemLocale>{config.SystemLocale}</SystemLocale>");
            sb.AppendLine($"            <UILanguage>{config.UILanguage}</UILanguage>");
            sb.AppendLine($"            <UserLocale>{config.UserLocale}</UserLocale>");
            sb.AppendLine("        </component>");

            // Shell setup - User accounts and OOBE settings
            sb.AppendLine("        <component name=\"Microsoft-Windows-Shell-Setup\" processorArchitecture=\"amd64\" publicKeyToken=\"31bf3856ad364e35\" language=\"neutral\" versionScope=\"nonSxS\" xmlns:wcm=\"http://schemas.microsoft.com/WMIConfig/2002/State\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");

            // Time zone
            sb.AppendLine($"            <TimeZone>{config.TimeZone}</TimeZone>");

            // Computer name (optional)
            if (!string.IsNullOrWhiteSpace(config.ComputerName))
            {
                sb.AppendLine($"            <ComputerName>{config.ComputerName}</ComputerName>");
            }

            // OOBE settings
            sb.AppendLine("            <OOBE>");
            sb.AppendLine($"                <HideEULAPage>{config.HideEULA.ToString().ToLower()}</HideEULAPage>");

            // For Autopilot: don't hide wireless setup (needed for Azure AD join)
            if (!config.AutopilotMode)
            {
                sb.AppendLine($"                <HideWirelessSetupInOOBE>{config.HideWirelessSetup.ToString().ToLower()}</HideWirelessSetupInOOBE>");
            }

            // NetworkLocation: 1=Home, 2=Work, 3=Public (skip network location selection)
            sb.AppendLine("                <NetworkLocation>1</NetworkLocation>");

            // ProtectYourPC: 1=Recommended, 3=Not now (skip privacy/telemetry screens)
            // For Autopilot: Let Autopilot policies control this
            // For standard unattended: Skip these screens
            if (!config.AutopilotMode)
            {
                sb.AppendLine("                <ProtectYourPC>3</ProtectYourPC>");
            }

            // Hide OOBE privacy/telemetry screens (location, diagnostics, speech, inking)
            if (!config.AutopilotMode)
            {
                sb.AppendLine("                <HideOEMRegistrationScreen>true</HideOEMRegistrationScreen>");
                sb.AppendLine("                <HideOnlineAccountScreens>true</HideOnlineAccountScreens>");
                sb.AppendLine("                <HideLocalAccountScreen>true</HideLocalAccountScreen>");
            }

            // SkipMachineOOBE and SkipUserOOBE control whether OOBE runs
            // For Autopilot: NEVER skip OOBE (Autopilot needs it for enrollment)
            if (config.SkipOOBE && !config.AutopilotMode)
            {
                sb.AppendLine("                <SkipMachineOOBE>true</SkipMachineOOBE>");
                sb.AppendLine("                <SkipUserOOBE>true</SkipUserOOBE>");
            }

            sb.AppendLine("            </OOBE>");

            // User accounts - Only create local admin if NOT in Autopilot mode
            if (!config.AutopilotMode)
            {
                sb.AppendLine("            <UserAccounts>");
                sb.AppendLine("                <LocalAccounts>");
                sb.AppendLine("                    <LocalAccount wcm:action=\"add\">");
                sb.AppendLine($"                        <Name>{config.AdminUsername}</Name>");
                sb.AppendLine("                        <Group>Administrators</Group>");
                sb.AppendLine($"                        <DisplayName>{config.AdminUsername}</DisplayName>");

                if (!string.IsNullOrWhiteSpace(config.AdminPassword))
                {
                    sb.AppendLine("                        <Password>");
                    sb.AppendLine($"                            <Value>{config.AdminPassword}</Value>");
                    sb.AppendLine("                            <PlainText>true</PlainText>");
                    sb.AppendLine("                        </Password>");
                }

                sb.AppendLine("                    </LocalAccount>");
                sb.AppendLine("                </LocalAccounts>");
                sb.AppendLine("            </UserAccounts>");
            }

            // Add FirstLogonCommands to disable privacy screens via registry
            // These are especially important for Autopilot to prevent the privacy screens
            sb.AppendLine("            <FirstLogonCommands>");

            // Disable privacy experience (location, diagnostics, speech, inking, etc.)
            sb.AppendLine("                <SynchronousCommand wcm:action=\"add\">");
            sb.AppendLine("                    <Order>1</Order>");
            sb.AppendLine("                    <CommandLine>reg add HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\OOBE /v DisablePrivacyExperience /t REG_DWORD /d 1 /f</CommandLine>");
            sb.AppendLine("                </SynchronousCommand>");

            // Skip machine OOBE registry key (backup method)
            if (!config.AutopilotMode)
            {
                sb.AppendLine("                <SynchronousCommand wcm:action=\"add\">");
                sb.AppendLine("                    <Order>2</Order>");
                sb.AppendLine("                    <CommandLine>reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE /v SkipMachineOOBE /t REG_DWORD /d 1 /f</CommandLine>");
                sb.AppendLine("                </SynchronousCommand>");

                sb.AppendLine("                <SynchronousCommand wcm:action=\"add\">");
                sb.AppendLine("                    <Order>3</Order>");
                sb.AppendLine("                    <CommandLine>reg add HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\OOBE /v SkipUserOOBE /t REG_DWORD /d 1 /f</CommandLine>");
                sb.AppendLine("                </SynchronousCommand>");
            }

            sb.AppendLine("            </FirstLogonCommands>");

            sb.AppendLine("        </component>");
            sb.AppendLine("    </settings>");

            sb.AppendLine("</unattend>");

            return sb.ToString();
        }
    }
}
