#requires -RunAsAdministrator
Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"
$script:operationCancelled = $false

# Hide console window for cleaner appearance
Add-Type -Name Window -Namespace Console -MemberDefinition '
[DllImport("Kernel32.dll")]
public static extern IntPtr GetConsoleWindow();

[DllImport("user32.dll")]
public static extern bool ShowWindow(IntPtr hWnd, Int32 nCmdShow);
'

$consolePtr = [Console.Window]::GetConsoleWindow()
[Console.Window]::ShowWindow($consolePtr, 0) # 0 = hide

# --- Enhanced Error Handler with Cleanup ---
trap {
    $errMsg = $_.Exception.Message
    Invoke-Cleanup
    [System.Windows.MessageBox]::Show("Critical Error: $errMsg`n`nThe application will attempt cleanup and exit.", "Error", "OK", "Error")
    exit
}

# --- Global Cleanup Function ---
function Invoke-Cleanup {
    Write-Host "Running cleanup procedures..." -ForegroundColor Yellow
    
    # Unmount any stuck WIM images
    $mountedImages = @()
    try {
        $mountedImages = Get-WindowsImage -Mounted -ErrorAction SilentlyContinue
    } catch {}
    
    foreach ($img in $mountedImages) {
        try {
            Dismount-WindowsImage -Path $img.Path -Discard -ErrorAction SilentlyContinue
        } catch {}
    }
    
    # Dismount any mounted ISOs
    try {
        $mountedISOs = Get-DiskImage | Where-Object { $_.Attached -eq $true }
        foreach ($iso in $mountedISOs) {
            Dismount-DiskImage -ImagePath $iso.ImagePath -ErrorAction SilentlyContinue
        }
    } catch {}
}

# --- Silent Command Execution ---
function Invoke-SilentCommand {
    param(
        [string]$FilePath,
        [string]$ArgumentList,
        [switch]$Wait
    )
    
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FilePath
    $psi.Arguments = $ArgumentList
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    $process.Start() | Out-Null
    
    if ($Wait) {
        $process.WaitForExit()
        return $process.ExitCode
    }
    
    return $process
}

# --- Utility Functions ---
function Test-DiskSpace {
    param([string]$Path, [long]$RequiredGB = 25)
    
    $drive = (Get-Item $Path -ErrorAction SilentlyContinue).PSDrive.Name + ":"
    if (-not $drive) { $drive = "C:" }
    $freeSpace = (Get-PSDrive $drive.TrimEnd(':')).Free / 1GB
    
    return @{
        HasSpace = $freeSpace -ge $RequiredGB
        FreeSpaceGB = [math]::Round($freeSpace, 2)
        RequiredGB = $RequiredGB
    }
}

function Test-ISOIntegrity {
    param([string]$ISOPath)
    
    $result = @{
        Valid = $false
        BootWim = $false
        InstallWim = $false
        Message = ""
    }
    
    try {
        $mount = Mount-DiskImage -ImagePath $ISOPath -PassThru -StorageType ISO
        Start-Sleep -Seconds 2
        $vol = ($mount | Get-Volume)
        $driveLetter = $vol.DriveLetter
        
        if (-not $driveLetter) {
            $result.Message = "Failed to mount ISO"
            Dismount-DiskImage -ImagePath $ISOPath -ErrorAction SilentlyContinue
            return $result
        }
        
        $bootWimPath = "${driveLetter}:\Sources\boot.wim"
        $installWimPath = "${driveLetter}:\Sources\install.wim"
        
        $result.BootWim = Test-Path $bootWimPath
        $result.InstallWim = Test-Path $installWimPath
        $result.Valid = $result.BootWim -and $result.InstallWim
        
        if (-not $result.Valid) {
            $result.Message = "Missing required WIM files"
        } else {
            $result.Message = "ISO is valid"
        }
        
        Dismount-DiskImage -ImagePath $ISOPath -ErrorAction SilentlyContinue
    } catch {
        $result.Message = "Error validating ISO: $($_.Exception.Message)"
    }
    
    return $result
}

function Get-WimEditions {
    param([string]$WimPath)
    
    try {
        $info = Get-WindowsImage -ImagePath $WimPath
        return $info | Select-Object ImageIndex, ImageName, ImageSize
    } catch {
        return @()
    }
}

function Test-DriverValidity {
    param([string]$DriverPath)
    
    $infFiles = Get-ChildItem -Path $DriverPath -Recurse -Filter *.inf -ErrorAction SilentlyContinue
    
    $result = @{
        Valid = $false
        DriverCount = 0
        SignedCount = 0
        UnsignedCount = 0
        Drivers = @()
    }
    
    if (-not $infFiles) {
        return $result
    }
    
    $result.DriverCount = $infFiles.Count
    
    foreach ($inf in $infFiles) {
        $driverInfo = @{
            Path = $inf.FullName
            Name = $inf.Name
            Signed = $false
        }
        
        # Check if driver is signed (basic check)
        $catFile = $inf.FullName -replace '\.inf$', '.cat'
        if (Test-Path $catFile) {
            $driverInfo.Signed = $true
            $result.SignedCount++
        } else {
            $result.UnsignedCount++
        }
        
        $result.Drivers += $driverInfo
    }
    
    $result.Valid = $result.DriverCount -gt 0
    return $result
}

function Get-USBDriveInfo {
    param([int]$DiskNumber)
    
    try {
        $disk = Get-Disk -Number $DiskNumber
        $partition = Get-Partition -DiskNumber $DiskNumber -ErrorAction SilentlyContinue | Select-Object -First 1
        $volume = if ($partition) { Get-Volume -Partition $partition -ErrorAction SilentlyContinue } else { $null }
        
        return @{
            Number = $disk.Number
            FriendlyName = $disk.FriendlyName
            Size = $disk.Size
            SizeGB = [math]::Round($disk.Size/1GB, 2)
            BusType = $disk.BusType
            MediaType = $disk.MediaType
            OperationalStatus = $disk.OperationalStatus
            PartitionStyle = $disk.PartitionStyle
            FileSystem = if ($volume) { $volume.FileSystem } else { "Unknown" }
            Label = if ($volume) { $volume.FileSystemLabel } else { "" }
        }
    } catch {
        return $null
    }
}

# --- Enhanced Progress Dialog ---
function Show-ProgressDialog {
    param(
        [string]$Title = "Processing...",
        [string]$Message = "Please wait..."
    )
    
    $xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="$Title" Height="240" Width="520" WindowStartupLocation="CenterScreen" 
        ResizeMode="NoResize" Topmost="True" WindowStyle="None" Background="White"
        BorderBrush="#0078D4" BorderThickness="1">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <TextBlock Text="$Title" Grid.Row="0" FontSize="18" FontWeight="Bold" Margin="0,0,0,15" Foreground="#0078D4"/>
        <TextBlock x:Name="txtMessage" Text="$Message" Grid.Row="1" FontSize="13" Margin="0,0,0,15" TextWrapping="Wrap"/>
        <ProgressBar x:Name="progressBar" Grid.Row="2" Height="30" Minimum="0" Maximum="100" Value="0" Margin="0,0,0,10"/>
        <TextBlock x:Name="txtPercent" Grid.Row="3" Text="0%" HorizontalAlignment="Center" FontSize="14" FontWeight="Bold" Margin="0,0,0,10" Foreground="#0078D4"/>
        <Button x:Name="btnCancel" Content="Cancel Operation" Grid.Row="4" Width="140" Height="32" Background="#E81123" Foreground="White" FontWeight="Bold"/>
    </Grid>
</Window>
"@
    
    $reader = [System.Xml.XmlReader]::Create([System.IO.StringReader]$xaml)
    $window = [Windows.Markup.XamlReader]::Load($reader)
    $window.Tag = @{
        ProgressBar = $window.FindName("progressBar")
        Message = $window.FindName("txtMessage")
        Percent = $window.FindName("txtPercent")
        Cancelled = $false
    }
    
    $btnCancel = $window.FindName("btnCancel")
    $btnCancel.Add_Click({
        $window.Tag.Cancelled = $true
        $script:operationCancelled = $true
        $window.Close()
    })
    
    return $window
}

function Update-Progress {
    param(
        [System.Windows.Window]$Window,
        [int]$Percent,
        [string]$Message
    )
    
    if (-not $Window -or $Window.Tag.Cancelled) { return }
    
    $Window.Dispatcher.Invoke([action]{
        $Window.Tag.ProgressBar.Value = $Percent
        $Window.Tag.Percent.Text = "$Percent%"
        if ($Message) {
            $Window.Tag.Message.Text = $Message
        }
    }, [System.Windows.Threading.DispatcherPriority]::Normal)
}

# --- Custom Choice Dialog ---
function Show-CustomChoiceDialog {
    param(
        [string]$Message,
        [string]$Title = "Choose an Option",
        [string]$Button1 = "Quit",
        [string]$Button2 = "Start Again"
    )
    $xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="$Title" Height="170" Width="340" WindowStartupLocation="CenterScreen" ResizeMode="NoResize" Topmost="True" WindowStyle="ToolWindow">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>
        <TextBlock Text="$Message" Grid.Row="0" TextWrapping="Wrap" FontSize="15" Margin="0,0,0,12" />
        <StackPanel Grid.Row="1" Orientation="Horizontal" HorizontalAlignment="Center">
            <Button x:Name="btn1" Content="$Button1" Width="110" Margin="0,0,12,0" IsDefault="True"/>
            <Button x:Name="btn2" Content="$Button2" Width="110"/>
        </StackPanel>
    </Grid>
</Window>
"@
    $reader = [System.Xml.XmlReader]::Create([System.IO.StringReader]$xaml)
    $form = [Windows.Markup.XamlReader]::Load($reader)
    $btn1 = $form.FindName("btn1")
    $btn2 = $form.FindName("btn2")
    $choice = $null
    $btn1.Add_Click({ $choice = 1; $form.Close() })
    $btn2.Add_Click({ $choice = 2; $form.Close() })
    $form.ShowDialog() | Out-Null
    return $choice
}

# --- Repair/Cleanup Dialog ---
function Show-RepairDialog {
    $result = [System.Windows.MessageBox]::Show(
        "This will force unmount all mounted WIM images and cleanup temporary files. Continue?",
        "Repair & Cleanup",
        [System.Windows.MessageBoxButton]::YesNo,
        [System.Windows.MessageBoxImage]::Question
    )
    
    if ($result -eq [System.Windows.MessageBoxResult]::Yes) {
        Invoke-Cleanup
        
        # Clean temp directories
        $tempDirs = @(
            "C:\WinImagePrep\ISO_Temp",
            "C:\WinImagePrep\Mount"
        )
        
        foreach ($dir in $tempDirs) {
            if (Test-Path $dir) {
                try {
                    Remove-Item -Path "$dir\*" -Recurse -Force -ErrorAction Continue
                    [System.Windows.MessageBox]::Show("Cleanup completed successfully!", "Success", "OK", "Info")
                } catch {
                    [System.Windows.MessageBox]::Show("Cleanup completed with some errors: $($_.Exception.Message)", "Partial Success", "OK", "Warning")
                }
            }
        }
    }
}

# --- Edition Selection Dialog ---
function Show-EditionSelectionDialog {
    param([array]$Editions)
    
    $xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Select Windows Editions" Height="400" Width="600" WindowStartupLocation="CenterScreen" 
        ResizeMode="NoResize" Topmost="True" WindowStyle="SingleBorderWindow">
    <Grid Margin="15">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <TextBlock Grid.Row="0" Text="Select which Windows editions to inject drivers into:" 
                   FontSize="14" Margin="0,0,0,10" TextWrapping="Wrap"/>
        <ListBox x:Name="lstEditions" Grid.Row="1" SelectionMode="Multiple" Margin="0,0,0,10">
        </ListBox>
        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Center">
            <Button x:Name="btnSelectAll" Content="Select All" Width="100" Margin="0,0,10,0"/>
            <Button x:Name="btnOK" Content="OK" Width="100" Margin="0,0,10,0"/>
            <Button x:Name="btnCancel" Content="Cancel" Width="100"/>
        </StackPanel>
    </Grid>
</Window>
"@
    
    $reader = [System.Xml.XmlReader]::Create([System.IO.StringReader]$xaml)
    $window = [Windows.Markup.XamlReader]::Load($reader)
    $lstEditions = $window.FindName("lstEditions")
    $btnSelectAll = $window.FindName("btnSelectAll")
    $btnOK = $window.FindName("btnOK")
    $btnCancel = $window.FindName("btnCancel")
    
    foreach ($edition in $Editions) {
        $item = New-Object System.Windows.Controls.ListBoxItem
        $item.Content = "[$($edition.ImageIndex)] $($edition.ImageName) - $([math]::Round($edition.ImageSize/1GB,2)) GB"
        $item.Tag = $edition.ImageIndex
        $lstEditions.Items.Add($item)
    }
    
    # Select all by default
    $lstEditions.SelectAll()
    
    $btnSelectAll.Add_Click({
        if ($lstEditions.SelectedItems.Count -eq $lstEditions.Items.Count) {
            $lstEditions.UnselectAll()
            $btnSelectAll.Content = "Select All"
        } else {
            $lstEditions.SelectAll()
            $btnSelectAll.Content = "Deselect All"
        }
    })
    
    $result = $null
    $btnOK.Add_Click({
        $result = @()
        foreach ($item in $lstEditions.SelectedItems) {
            $result += $item.Tag
        }
        $window.Tag = $result
        $window.Close()
    })
    
    $btnCancel.Add_Click({ $window.Close() })
    
    $window.ShowDialog() | Out-Null
    return $window.Tag
}

# --- Saved Image Creation Dialog ---
function Show-CreateFromSavedImageForm {
    $savedImagesDir = "C:\WinImagePrep\SavedImages"
    if (-not (Test-Path $savedImagesDir)) {
        [System.Windows.MessageBox]::Show("No saved images folder found.", "Error", "OK", "Error")
        return
    }
    $folders = Get-ChildItem $savedImagesDir -Directory | Select-Object -ExpandProperty Name
    if (-not $folders) {
        [System.Windows.MessageBox]::Show("No saved images found in $savedImagesDir.", "No Images", "OK", "Warning")
        return
    }

    $xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Create USB from Saved Image" Height="470" Width="880" WindowStartupLocation="CenterScreen" ResizeMode="NoResize" Topmost="True" WindowStyle="SingleBorderWindow">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,10">
            <TextBlock Text="Select saved image:" VerticalAlignment="Center" Width="180"/>
            <ComboBox x:Name="cmbSaved" Width="510"/>
        </StackPanel>
        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,0,0,10">
            <TextBlock Text="Image label:" VerticalAlignment="Center" Width="180"/>
            <TextBlock x:Name="lblImgLabel" Width="510" VerticalAlignment="Center" FontWeight="Bold"/>
        </StackPanel>
        <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="0,0,0,10">
            <TextBlock Text="Select USB Drive:" VerticalAlignment="Center" Width="180"/>
            <ComboBox x:Name="cmbUSB" Width="390"/>
            <Button Content="Refresh USB List" x:Name="btnRefreshUSB" Margin="15,0,0,0" Width="150" Height="24"/>
        </StackPanel>
        <StackPanel Grid.Row="3" Orientation="Horizontal" Margin="0,0,0,10" HorizontalAlignment="Center">
            <Button Content="Create USB" x:Name="btnCreateUSB" Width="280" Height="24" Margin="0,0,36,0"/>
            <Button Content="Cancel" x:Name="btnCancel" Width="120" Height="24"/>
        </StackPanel>
        <TextBox x:Name="txtSavedLog"
                 Grid.Row="4"
                 Background="Black"
                 Foreground="Lime"
                 FontWeight="Bold"
                 Padding="5,2"
                 FontSize="15"
                 FontFamily="Consolas"
                 HorizontalAlignment="Stretch"
                 VerticalAlignment="Stretch"
                 IsReadOnly="True"
                 BorderThickness="1"
                 BorderBrush="Gray"
                 VerticalScrollBarVisibility="Auto"
                 TextWrapping="Wrap"/>
    </Grid>
</Window>
"@

    $reader = [System.Xml.XmlReader]::Create([System.IO.StringReader]$xaml)
    $form = [Windows.Markup.XamlReader]::Load($reader)
    $cmbSaved = $form.FindName("cmbSaved")
    $lblImgLabel = $form.FindName("lblImgLabel")
    $cmbUSB = $form.FindName("cmbUSB")
    $btnRefreshUSB = $form.FindName("btnRefreshUSB")
    $btnCreateUSB = $form.FindName("btnCreateUSB")
    $btnCancel = $form.FindName("btnCancel")
    $txtSavedLog = $form.FindName("txtSavedLog")

    $folders | ForEach-Object { $cmbSaved.Items.Add($_) }
    $cmbSaved.SelectedIndex = 0

    function Write-SavedStatus([string]$text) {
        $timestamp = Get-Date -Format "HH:mm:ss"
        $txtSavedLog.Dispatcher.Invoke([action]{
            $txtSavedLog.AppendText("[$timestamp] $text`r`n")
            $txtSavedLog.ScrollToEnd()
        })
        $txtSavedLog.Dispatcher.Invoke([action]{}, [System.Windows.Threading.DispatcherPriority]::Background)
    }

    function UpdateLabel {
        $sel = $cmbSaved.SelectedItem
        $imgDir = Join-Path $savedImagesDir $sel
        $lblFile = Join-Path $imgDir "iso-label.txt"
        if (Test-Path $lblFile) {
            $lblImgLabel.Text = Get-Content $lblFile
        } else {
            $lblImgLabel.Text = "(no label found)"
        }
    }
    $cmbSaved.Add_SelectionChanged({ UpdateLabel })
    UpdateLabel

    function RefreshUSBList {
        $cmbUSB.Items.Clear()
        $usbs = Get-Disk | Where-Object BusType -eq 'USB'
        if ($usbs.Count -eq 0) {
            $cmbUSB.Items.Add("No USB drives found")
            $cmbUSB.SelectedIndex = 0
        } else {
            foreach ($disk in $usbs) {
                $desc = "$($disk.Number): $($disk.FriendlyName) - $([math]::Round($disk.Size/1GB,2)) GB"
                $cmbUSB.Items.Add($desc)
            }
            $cmbUSB.SelectedIndex = 0
        }
    }
    $btnRefreshUSB.Add_Click({ RefreshUSBList })
    RefreshUSBList

    $btnCancel.Add_Click({ $form.Close() })

    $btnCreateUSB.Add_Click({
        $sel = $cmbSaved.SelectedItem
        if (-not $sel) { [System.Windows.MessageBox]::Show("Select a saved image.", "Error", "OK", "Error"); return }
        $imgDir = Join-Path $savedImagesDir $sel
        $lblFile = Join-Path $imgDir "iso-label.txt"
        $label = "WIN11USB"
        if (Test-Path $lblFile) { $label = Get-Content $lblFile }
        if ($cmbUSB.SelectedItem -like "No USB*") {
            [System.Windows.MessageBox]::Show("Please insert a USB drive to continue.", "No USB Detected", "OK", "Warning")
            return
        }
        $driveNumber = $cmbUSB.SelectedItem.ToString().Split(":")[0]
        $usbs = Get-Disk | Where-Object BusType -eq 'USB'
        $usbDisk = $usbs | Where-Object Number -eq $driveNumber
        if (-not $usbDisk) {
            [System.Windows.MessageBox]::Show("Invalid USB disk.", "Error", "OK", "Error")
            return
        }
        if ($usbDisk.Size -lt 14GB) {
            [System.Windows.MessageBox]::Show("Selected USB drive is less than 14GB. Please use a 14GB or larger drive.", "Error", "OK", "Error")
            return
        }
        $res = [System.Windows.MessageBox]::Show("WARNING: This will ERASE all partitions and data on USB drive $driveNumber! Continue?", "Confirm", "YesNo", "Warning")
        if ($res -ne "Yes") { return }
        Write-SavedStatus "Removing partitions..."
        $existingPartitions = Get-Partition -DiskNumber $driveNumber -ErrorAction SilentlyContinue
        if ($existingPartitions) {
            foreach ($part in $existingPartitions) {
                Remove-Partition -DiskNumber $driveNumber -PartitionNumber $part.PartitionNumber -Confirm:$false -ErrorAction SilentlyContinue
            }
            Start-Sleep -Seconds 2
        }
        $disk = Get-Disk -Number $driveNumber
        if ($disk.PartitionStyle -eq 'RAW') {
            Write-SavedStatus "Initializing Disk"
            Initialize-Disk -Number $driveNumber -PartitionStyle MBR
            Start-Sleep -Seconds 2
        }
        $size14GB = 14GB
        Write-SavedStatus "Creating 14GB partition..."
        $partition = New-Partition -DiskNumber $driveNumber -Size $size14GB -AssignDriveLetter
        Start-Sleep -Seconds 1
        if ($label.Length -gt 11) { $label = $label.Substring(0, 11) }
        $label = $label -replace '[^a-zA-Z0-9_ ]', ''
        Write-SavedStatus "Formatting as FAT32 with label: $label"
        Format-Volume -Partition $partition -FileSystem FAT32 -NewFileSystemLabel $label -Confirm:$false
        $usbDriveLetter = ($partition | Get-Volume).DriveLetter
        if (-not $usbDriveLetter) {
            [System.Windows.MessageBox]::Show("Could not get drive letter for USB.", "Error", "OK", "Error")
            return
        }
        $usbRoot = "$usbDriveLetter`:\"
        Write-SavedStatus "Copying files to USB..."
        robocopy "$imgDir" "$usbRoot" /E /NJH /NJS /NP /NFL /NDL | Out-Null
        Write-SavedStatus "USB creation complete!"
        [System.Windows.MessageBox]::Show("Bootable Windows 11 USB created successfully from saved image!", "Done", "OK", "Info")
        $form.Close()
    })

    $form.ShowDialog() | Out-Null
}

# --- USB from ISO Dialog ---
function Show-UsbFromIsoForm {
    $isoTempDir = "C:\WinImagePrep\ISO_Temp"
    if (-not (Test-Path $isoTempDir)) { New-Item -Path $isoTempDir -ItemType Directory -Force | Out-Null }

    $xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Create Bootable USB from ISO" Height="360" Width="760" WindowStartupLocation="CenterScreen" ResizeMode="NoResize" Topmost="True" WindowStyle="SingleBorderWindow">
    <Grid Margin="10">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,10">
            <TextBlock Text="Select ISO:" VerticalAlignment="Center" Width="120"/>
            <TextBox x:Name="txtIsoOnly" Width="400" IsReadOnly="True"/>
            <Button Content="Browse..." x:Name="btnBrowseIsoOnly" Width="90" Height="24" Margin="10,0,0,0"/>
        </StackPanel>
        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,0,0,10">
            <TextBlock Text="Select USB Drive:" VerticalAlignment="Center" Width="120"/>
            <ComboBox x:Name="cmbUsbIsoOnly" Width="350"/>
            <Button Content="Refresh" x:Name="btnRefreshUsbIsoOnly" Width="110" Height="24" Margin="10,0,0,0"/>
        </StackPanel>
        <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="0,0,0,10" HorizontalAlignment="Center">
            <Button Content="Create USB from ISO" x:Name="btnCreateUsbIsoOnly" Width="260" Height="24" IsEnabled="False"/>
            <Button Content="Cancel" x:Name="btnCancelUsbIsoOnly" Width="120" Height="24" Margin="20,0,0,0"/>
        </StackPanel>
        <TextBox x:Name="txtLogIsoOnly"
                 Grid.Row="3"
                 Background="Black"
                 Foreground="Lime"
                 FontWeight="Bold"
                 Padding="5,2"
                 FontSize="15"
                 FontFamily="Consolas"
                 HorizontalAlignment="Stretch"
                 VerticalAlignment="Stretch"
                 IsReadOnly="True"
                 BorderThickness="1"
                 BorderBrush="Gray"
                 VerticalScrollBarVisibility="Auto"
                 TextWrapping="Wrap"/>
    </Grid>
</Window>
"@

    $reader = [System.Xml.XmlReader]::Create([System.IO.StringReader]$xaml)
    $form = [Windows.Markup.XamlReader]::Load($reader)
    $txtIsoOnly = $form.FindName("txtIsoOnly")
    $btnBrowseIsoOnly = $form.FindName("btnBrowseIsoOnly")
    $cmbUsbIsoOnly = $form.FindName("cmbUsbIsoOnly")
    $btnRefreshUsbIsoOnly = $form.FindName("btnRefreshUsbIsoOnly")
    $btnCreateUsbIsoOnly = $form.FindName("btnCreateUsbIsoOnly")
    $btnCancelUsbIsoOnly = $form.FindName("btnCancelUsbIsoOnly")
    $txtLogIsoOnly = $form.FindName("txtLogIsoOnly")

    function Write-Log([string]$text) {
        $timestamp = Get-Date -Format "HH:mm:ss"
        $txtLogIsoOnly.Dispatcher.Invoke([action]{
            $txtLogIsoOnly.AppendText("[$timestamp] $text`r`n")
            $txtLogIsoOnly.ScrollToEnd()
        })
        $txtLogIsoOnly.Dispatcher.Invoke([action]{}, [System.Windows.Threading.DispatcherPriority]::Background)
    }

    function RefreshUsbList {
        $cmbUsbIsoOnly.Items.Clear()
        $usbs = Get-Disk | Where-Object BusType -eq 'USB'
        if ($usbs.Count -eq 0) {
            $cmbUsbIsoOnly.Items.Add("No USB drives found")
            $cmbUsbIsoOnly.SelectedIndex = 0
        } else {
            foreach ($disk in $usbs) {
                $desc = "$($disk.Number): $($disk.FriendlyName) - $([math]::Round($disk.Size/1GB,2)) GB"
                $cmbUsbIsoOnly.Items.Add($desc)
            }
            $cmbUsbIsoOnly.SelectedIndex = 0
        }
    }

    $btnRefreshUsbIsoOnly.Add_Click({ RefreshUsbList })
    RefreshUsbList

    $btnCancelUsbIsoOnly.Add_Click({ $form.Close() })

    $btnBrowseIsoOnly.Add_Click({
        $ofd = New-Object System.Windows.Forms.OpenFileDialog
        $ofd.Filter = "ISO files (*.iso)|*.iso"
        if ($ofd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
            $txtIsoOnly.Text = $ofd.FileName
            Write-Log "ISO selected: $($ofd.FileName)"
            if ($cmbUsbIsoOnly.SelectedIndex -ge 0 -and $cmbUsbIsoOnly.SelectedItem -notlike "No USB*") {
                $btnCreateUsbIsoOnly.IsEnabled = $true
            }
        }
    })

    $cmbUsbIsoOnly.Add_SelectionChanged({
        if ($cmbUsbIsoOnly.SelectedIndex -ge 0 -and $cmbUsbIsoOnly.SelectedItem -notlike "No USB*" -and $txtIsoOnly.Text.Length -gt 0) {
            $btnCreateUsbIsoOnly.IsEnabled = $true
        } else {
            $btnCreateUsbIsoOnly.IsEnabled = $false
        }
    })

    $btnCreateUsbIsoOnly.Add_Click({
        $isoPath = $txtIsoOnly.Text.Trim()
        if (-not (Test-Path $isoPath)) {
            Write-Log "Select a valid ISO file."
            return
        }
        if ($cmbUsbIsoOnly.SelectedItem -like "No USB*") {
            Write-Log "No USB selected."
            return
        }
        $usbDriveNumber = $cmbUsbIsoOnly.SelectedItem.ToString().Split(":")[0]
        $usbs = Get-Disk | Where-Object BusType -eq 'USB'
        $usbDisk = $usbs | Where-Object Number -eq $usbDriveNumber
        if (-not $usbDisk) {
            Write-Log "Invalid USB selection."
            return
        }
        $usbSizeGB = [math]::Round($usbDisk.Size/1GB,2)
        $res = [System.Windows.MessageBox]::Show("WARNING: This will ERASE all partitions and data on USB drive $usbDriveNumber! Continue?", "Confirm", "YesNo", "Warning")
        if ($res -ne "Yes") { Write-Log "Operation cancelled by user."; return }

        Write-Log "Clearing temp folder..."
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$isoTempDir\*" | Out-Null

        Write-Log "Mounting ISO..."
        $mountResult = Mount-DiskImage -ImagePath $isoPath -PassThru -StorageType ISO
        Start-Sleep -Seconds 2
        $vol = ($mountResult | Get-Volume)
        $driveLetter = $vol.DriveLetter
        $isoLabel = $vol.FileSystemLabel
        if (-not $driveLetter) { Write-Log "ISO mount failed!"; return }

        Write-Log "Copying ISO files to temp..."
        robocopy "$driveLetter`:\" $isoTempDir /E /NJH /NJS /NP /NFL /NDL | Out-Null

        Write-Log "Clearing ReadOnly attributes..."
        Get-ChildItem -Path $isoTempDir -Recurse -File | ForEach-Object { $_.Attributes = $_.Attributes -band (-bnot [System.IO.FileAttributes]::ReadOnly) }
        Dismount-DiskImage -ImagePath $isoPath

        $sources = "$isoTempDir\Sources"
        $wimPath = "$sources\install.wim"
        $swmBasePath = "$sources\install.swm"
        if (Test-Path $wimPath) {
            $wimInfo = Get-Item $wimPath
            if ($wimInfo.Length -gt 4GB) {
                Write-Log "install.wim > 4GB, splitting..."
                Get-ChildItem -Path $sources -Filter "install*.swm" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
                $exitCode = Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Split-Image /ImageFile:`"$wimPath`" /SWMFile:`"$swmBasePath`" /FileSize:3800" -Wait
                if (Test-Path "$swmBasePath") {
                    Remove-Item $wimPath -Force
                    Write-Log "install.wim split successful"
                }
            }
        }

        if ($usbSizeGB -lt 32) {
            $partitionSizeGB = [math]::Floor($usbSizeGB)
        } else {
            $partitionSizeGB = 32
        }
        Write-Log "Preparing USB: $usbSizeGB GB detected, partition size: $partitionSizeGB GB"
        $existingPartitions = Get-Partition -DiskNumber $usbDriveNumber -ErrorAction SilentlyContinue
        if ($existingPartitions) {
            foreach ($part in $existingPartitions) {
                Remove-Partition -DiskNumber $usbDriveNumber -PartitionNumber $part.PartitionNumber -Confirm:$false -ErrorAction SilentlyContinue
            }
            Start-Sleep -Seconds 2
        }
        $disk = Get-Disk -Number $usbDriveNumber
        if ($disk.PartitionStyle -eq 'RAW') {
            Write-Log "Initializing Disk"
            Initialize-Disk -Number $usbDriveNumber -PartitionStyle MBR
            Start-Sleep -Seconds 2
        }
        Write-Log "Creating partition..."
        $partition = New-Partition -DiskNumber $usbDriveNumber -Size ($partitionSizeGB * 1GB) -AssignDriveLetter
        Start-Sleep -Seconds 1

        $label = $isoLabel
        if ($null -eq $label -or $label -eq "") { $label = "WIN11USB" }
        $label = $label -replace '[^a-zA-Z0-9_ ]', ''
        if ($label.Length -gt 11) { $label = $label.Substring(0,11) }

        Write-Log "Formatting as FAT32 with label: $label"
        Format-Volume -Partition $partition -FileSystem FAT32 -NewFileSystemLabel $label -Confirm:$false
        $usbDriveLetter = ($partition | Get-Volume).DriveLetter
        if (-not $usbDriveLetter) {
            Write-Log "Error: No drive letter"
            return
        }
        $usbRoot = "$usbDriveLetter`:\"
        Write-Log "Copying files to USB..."
        robocopy "$isoTempDir" "$usbRoot" /E /NJH /NJS /NP /NFL /NDL | Out-Null
        Write-Log "USB creation complete!"

        Write-Log "Cleaning up temp ISO files..."
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$isoTempDir\*" | Out-Null

        [System.Windows.MessageBox]::Show("Bootable Windows 11 USB created successfully from ISO!", "Done", "OK", "Info")
        $form.Close()
    })

    $form.ShowDialog() | Out-Null
}

# --- About Dialog ---
function Show-AboutDialog {
    $xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="About" Height="350" Width="450" WindowStartupLocation="CenterScreen" 
        ResizeMode="NoResize" Topmost="True" WindowStyle="SingleBorderWindow">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>
        <TextBlock Grid.Row="0" Text="Windows 11 Image Preparation Tool" FontSize="18" FontWeight="Bold" Margin="0,0,0,10" HorizontalAlignment="Center" Foreground="#0078D4"/>
        <ScrollViewer Grid.Row="1" VerticalScrollBarVisibility="Auto" Margin="0,0,0,10">
            <StackPanel>
                <TextBlock Text="Version 3.0" FontSize="14" FontWeight="Bold" Margin="0,0,0,10" HorizontalAlignment="Center"/>
                <TextBlock TextWrapping="Wrap" Margin="0,0,0,10">
                    <Run Text="A professional tool for creating custom Windows 11 installation media with integrated drivers, specifically designed for Microsoft Surface devices."/
                </TextBlock>
                <TextBlock Text="Features:" FontWeight="Bold" Margin="0,10,0,5"/>
                <TextBlock TextWrapping="Wrap" Margin="10,0,0,5">• Inject drivers into Windows 11 ISO images</TextBlock>
                <TextBlock TextWrapping="Wrap" Margin="10,0,0,5">• Create bootable USB drives (UEFI compatible)</TextBlock>
                <TextBlock TextWrapping="Wrap" Margin="10,0,0,5">• Edition selection (Pro, Enterprise, etc.)</TextBlock>
                <TextBlock TextWrapping="Wrap" Margin="10,0,0,5">• Save and reuse prepared images</TextBlock>
                <TextBlock TextWrapping="Wrap" Margin="10,0,0,5">• Silent background processing</TextBlock>
                <TextBlock TextWrapping="Wrap" Margin="10,0,0,5">• ISO integrity verification</TextBlock>
                <TextBlock TextWrapping="Wrap" Margin="10,0,0,5">• Automatic WIM splitting for FAT32</TextBlock>
                <TextBlock Text="System Requirements:" FontWeight="Bold" Margin="0,15,0,5"/>
                <TextBlock TextWrapping="Wrap" Margin="10,0,0,5">• Windows 10/11 with Administrator rights</TextBlock>
                <TextBlock TextWrapping="Wrap" Margin="10,0,0,5">• 25+ GB free disk space</TextBlock>
                <TextBlock TextWrapping="Wrap" Margin="10,0,0,5">• PowerShell 5.1 or higher</TextBlock>
            </StackPanel>
        </ScrollViewer>
        <Button Grid.Row="2" Content="Close" x:Name="btnClose" Width="100" Height="30" HorizontalAlignment="Center"/>
    </Grid>
</Window>
"@
    
    $reader = [System.Xml.XmlReader]::Create([System.IO.StringReader]$xaml)
    $window = [Windows.Markup.XamlReader]::Load($reader)
    $btnClose = $window.FindName("btnClose")
    $btnClose.Add_Click({ $window.Close() })
    $window.ShowDialog() | Out-Null
}

# --- Main Window ---
$xaml = @"
<Window xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="Windows 11 Image &amp; USB Creator V3" Height="750" Width="920" ResizeMode="NoResize" WindowStartupLocation="CenterScreen">
    <Window.Resources>
        <Style TargetType="Button">
            <Setter Property="Background" Value="#0078D4"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="FontWeight" Value="Bold"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Cursor" Value="Hand"/>
        </Style>
    </Window.Resources>
    <Grid Margin="15">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        
        <Border Grid.Row="0" Background="#0078D4" Padding="10" Margin="-15,-15,-15,15">
            <Grid>
                <TextBlock Text="Windows 11 Image Preparation Tool - Version 3.0" 
                           FontSize="18" FontWeight="Bold" Foreground="White" VerticalAlignment="Center"/>
                <Button Content="?" x:Name="btnAbout" Width="30" Height="30" HorizontalAlignment="Right" 
                        FontSize="16" FontWeight="Bold" Background="#005A9E"/>
            </Grid>
        </Border>
        
        <StackPanel Grid.Row="1" Orientation="Horizontal" Margin="0,0,0,10">
            <TextBlock Text="1. Select Windows 11 ISO:" VerticalAlignment="Center" Width="180" FontWeight="Bold"/>
            <TextBox x:Name="txtISO" Width="420" IsReadOnly="True" Margin="5,0,0,0" Height="26"/>
            <Button Content="Browse..." x:Name="btnBrowseISO" Margin="5,0,0,0" Width="90" Height="26"/>
            <Button Content="Verify" x:Name="btnVerifyISO" Margin="5,0,0,0" Width="90" Height="26"/>
        </StackPanel>
        
        <StackPanel Grid.Row="2" Orientation="Horizontal" Margin="0,0,0,10">
            <TextBlock Text="2. Select Driver MSI file:" VerticalAlignment="Center" Width="180" FontWeight="Bold"/>
            <TextBox x:Name="txtMSI" Width="420" IsReadOnly="True" Margin="5,0,0,0" Height="26"/>
            <Button Content="Browse..." x:Name="btnBrowseMSI" Margin="5,0,0,0" Width="90" Height="26"/>
        </StackPanel>
        
        <GroupBox Grid.Row="3" Header="Advanced Options" Padding="10" Margin="0,0,0,10">
            <StackPanel>
                <Button Content="Select Specific Windows Editions" x:Name="btnSelectEditions" Height="32" Margin="0,0,0,5"/>
            </StackPanel>
        </GroupBox>
        
        <StackPanel Grid.Row="4" Orientation="Horizontal" Margin="0,0,0,10" HorizontalAlignment="Center">
            <Button Content="Prepare Image with Drivers" x:Name="btnInject" Width="220" Height="36" Margin="0,0,10,0" FontSize="14"/>
            <Button Content="Create from Saved Image" x:Name="btnFromSaved" Width="220" Height="36" Margin="0,0,10,0" FontSize="14"/>
            <Button Content="Create USB from ISO" x:Name="btnUsbFromIso" Width="220" Height="36" FontSize="14"/>
        </StackPanel>
        
        <StackPanel Grid.Row="5" Orientation="Horizontal" Margin="0,0,0,10">
            <TextBlock Text="3. Select USB Drive:" VerticalAlignment="Center" Width="180" FontWeight="Bold"/>
            <ComboBox x:Name="cmbUSB" Width="420" Height="26"/>
            <Button Content="Refresh" x:Name="btnRefreshUSB" Margin="10,0,0,0" Width="90" Height="26"/>
            <Button Content="Repair/Cleanup" x:Name="btnRepair" Margin="5,0,0,0" Width="120" Height="26" Background="#E81123"/>
        </StackPanel>
        
        <Border Grid.Row="6" BorderBrush="#0078D4" BorderThickness="1" Background="#F0F0F0" Padding="10" Margin="0,0,0,10">
            <StackPanel>
                <TextBlock Text="USB Drive Information:" FontWeight="Bold" Margin="0,0,0,5"/>
                <TextBlock x:Name="lblUsbInfo" Text="No USB drive selected" TextWrapping="Wrap"/>
            </StackPanel>
        </Border>
        
        <Border Grid.Row="7" BorderBrush="#E81123" BorderThickness="2" Background="#FFF4CE" Padding="5" Margin="0,0,0,10" Visibility="Collapsed" x:Name="borderWarning">
            <TextBlock x:Name="lblWarn" Foreground="#E81123" VerticalAlignment="Center" FontWeight="Bold" TextWrapping="Wrap"/>
        </Border>
        
        <GroupBox Grid.Row="8" Header="Operation Log" Padding="5">
            <TextBox x:Name="txtLog"
                     Background="Black"
                     Foreground="Lime"
                     FontWeight="Bold"
                     Padding="5,2"
                     FontSize="13"
                     FontFamily="Consolas"
                     HorizontalAlignment="Stretch"
                     VerticalAlignment="Stretch"
                     IsReadOnly="True"
                     BorderThickness="0"
                     VerticalScrollBarVisibility="Auto"
                     TextWrapping="Wrap"/>
        </GroupBox>
    </Grid>
</Window>
"@

$topLevelDir   = "C:\WinImagePrep"
$windows11Dir  = Join-Path $topLevelDir "Windows11"
$driversDir    = Join-Path $topLevelDir "Drivers"
$mountDir      = Join-Path $topLevelDir "Mount"
$configDir     = Join-Path $topLevelDir "Config"
$script:selectedEditions = $null

$bytes = [System.Text.Encoding]::UTF8.GetBytes($xaml)
$stream = New-Object System.IO.MemoryStream(,$bytes)
$window = [Windows.Markup.XamlReader]::Load($stream)

$txtISO = $window.FindName("txtISO")
$btnBrowseISO = $window.FindName("btnBrowseISO")
$btnVerifyISO = $window.FindName("btnVerifyISO")
$txtMSI = $window.FindName("txtMSI")
$btnBrowseMSI = $window.FindName("btnBrowseMSI")
$btnSelectEditions = $window.FindName("btnSelectEditions")
$btnInject = $window.FindName("btnInject")
$btnRepair = $window.FindName("btnRepair")
$cmbUSB = $window.FindName("cmbUSB")
$btnRefreshUSB = $window.FindName("btnRefreshUSB")
$btnFromSaved = $window.FindName("btnFromSaved")
$btnUsbFromIso = $window.FindName("btnUsbFromIso")
$btnAbout = $window.FindName("btnAbout")
$lblUsbInfo = $window.FindName("lblUsbInfo")
$lblWarn = $window.FindName("lblWarn")
$borderWarning = $window.FindName("borderWarning")
$txtLog = $window.FindName("txtLog")
$script:usbDrives = @()

function Write-Status([string]$text) {
    $timestamp = Get-Date -Format "HH:mm:ss"
    $txtLog.Dispatcher.Invoke([action]{
        $txtLog.AppendText("[$timestamp] $text`r`n")
        $txtLog.ScrollToEnd()
    })
    $txtLog.Dispatcher.Invoke([action]{}, [System.Windows.Threading.DispatcherPriority]::Background)
}

function Clear-Log { 
    $txtLog.Dispatcher.Invoke([action]{ $txtLog.Clear() }) 
}

function Update-UsbInfo {
    if ($cmbUSB.SelectedIndex -ge 0 -and $cmbUSB.SelectedItem -notlike "No USB*") {
        $driveNumber = $cmbUSB.SelectedItem.ToString().Split(":")[0]
        $info = Get-USBDriveInfo -DiskNumber $driveNumber
        
        if ($info) {
            $infoText = "Drive: $($info.FriendlyName) | Size: $($info.SizeGB) GB | Type: $($info.BusType) - $($info.MediaType) | Status: $($info.OperationalStatus)"
            if ($info.FileSystem -ne "Unknown") {
                $infoText += " | Current Format: $($info.FileSystem)"
            }
            $lblUsbInfo.Text = $infoText
            
            if ($info.SizeGB -lt 14) {
                $lblWarn.Text = "⚠ WARNING: USB drive is less than 14GB. A larger drive is recommended for Windows 11 installation."
                $borderWarning.Visibility = [System.Windows.Visibility]::Visible
            } else {
                $lblWarn.Text = "⚠ WARNING: ALL DATA ON THIS USB DRIVE WILL BE ERASED!"
                $borderWarning.Visibility = [System.Windows.Visibility]::Visible
            }
        }
    } else {
        $lblUsbInfo.Text = "No USB drive selected"
        $borderWarning.Visibility = [System.Windows.Visibility]::Collapsed
    }
}

function Refresh-USBList {
    $cmbUSB.Items.Clear()
    $script:usbDrives = Get-Disk | Where-Object BusType -eq 'USB'
    if ($script:usbDrives.Count -eq 0) {
        $cmbUSB.Items.Add("No USB drives found")
        $cmbUSB.SelectedIndex = 0
    } else {
        foreach ($disk in $usbDrives) {
            $desc = "$($disk.Number): $($disk.FriendlyName) - $([math]::Round($disk.Size/1GB,2)) GB"
            $cmbUSB.Items.Add($desc)
        }
        $cmbUSB.SelectedIndex = 0
    }
    Update-UsbInfo
}

$btnRefreshUSB.Add_Click({ Refresh-USBList })
Refresh-USBList

$cmbUSB.Add_SelectionChanged({ Update-UsbInfo })

$btnBrowseISO.Add_Click({
    $ofd = New-Object System.Windows.Forms.OpenFileDialog
    $ofd.Filter = "ISO files (*.iso)|*.iso"
    if ($ofd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $txtISO.Text = $ofd.FileName
        Write-Status "ISO selected: $($ofd.FileName)"
    }
})

$btnBrowseMSI.Add_Click({
    $ofd = New-Object System.Windows.Forms.OpenFileDialog
    $ofd.Filter = "MSI files (*.msi)|*.msi"
    if ($ofd.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $txtMSI.Text = $ofd.FileName
        Write-Status "MSI selected: $($ofd.FileName)"
    }
})

$btnVerifyISO.Add_Click({
    $isoPath = $txtISO.Text.Trim()
    if (-not (Test-Path $isoPath)) {
        [System.Windows.MessageBox]::Show("Please select an ISO file first.", "Error", "OK", "Error")
        return
    }
    
    $window.Cursor = 'Wait'
    
    # Check disk space
    $spaceCheck = Test-DiskSpace -Path "C:\WinImagePrep" -RequiredGB 25
    if (-not $spaceCheck.HasSpace) {
        [System.Windows.MessageBox]::Show(
            "Insufficient disk space!`n`nRequired: $($spaceCheck.RequiredGB) GB`nAvailable: $($spaceCheck.FreeSpaceGB) GB",
            "Disk Space Error",
            "OK",
            "Error"
        )
        $window.Cursor = 'Arrow'
        return
    }
    
    $validation = Test-ISOIntegrity -ISOPath $isoPath
    
    if ($validation.Valid) {
        $msg = "ISO Validation Successful!`n`n"
        $msg += "[OK] boot.wim found`n"
        $msg += "[OK] install.wim found`n"
        $msg += "`nDisk Space: $($spaceCheck.FreeSpaceGB) GB available"
        [System.Windows.MessageBox]::Show($msg, "ISO Valid", "OK", "Info")
        Write-Status "ISO validation: PASSED"
    } else {
        [System.Windows.MessageBox]::Show("ISO Validation Failed!`n`n$($validation.Message)", "ISO Invalid", "OK", "Error")
        Write-Status "ISO validation: FAILED - $($validation.Message)"
    }
    
    $window.Cursor = 'Arrow'
})

$btnSelectEditions.Add_Click({
    $isoPath = $txtISO.Text.Trim()
    if (-not (Test-Path $isoPath)) {
        [System.Windows.MessageBox]::Show("Please select an ISO file first.", "Error", "OK", "Error")
        return
    }
    
    $window.Cursor = 'Wait'
    Write-Status "Reading ISO editions..."
    
    # Mount ISO temporarily to read editions
    $mount = Mount-DiskImage -ImagePath $isoPath -PassThru -StorageType ISO
    Start-Sleep -Seconds 2
    $vol = ($mount | Get-Volume)
    $driveLetter = $vol.DriveLetter
    
    if ($driveLetter) {
        $installWim = "${driveLetter}:\Sources\install.wim"
        $editions = Get-WimEditions -WimPath $installWim
        Dismount-DiskImage -ImagePath $isoPath
        
        if ($editions.Count -gt 0) {
            $script:selectedEditions = Show-EditionSelectionDialog -Editions $editions
            if ($script:selectedEditions) {
                Write-Status "Selected $($script:selectedEditions.Count) edition(s) for injection"
                [System.Windows.MessageBox]::Show("Selected $($script:selectedEditions.Count) edition(s) for driver injection.", "Editions Selected", "OK", "Info")
            }
        } else {
            [System.Windows.MessageBox]::Show("Could not read editions from ISO.", "Error", "OK", "Error")
        }
    }
    
    $window.Cursor = 'Arrow'
})

$btnRepair.Add_Click({ Show-RepairDialog })
$btnAbout.Add_Click({ Show-AboutDialog })
$btnFromSaved.Add_Click({ Show-CreateFromSavedImageForm })
$btnUsbFromIso.Add_Click({ Show-UsbFromIsoForm })

function New-BootableWin11USB {
    param(
        [string]$SourceFolder,
        [System.Windows.Controls.ComboBox]$cmbUSB,
        [System.Windows.Window]$window
    )
    if ($script:usbDrives.Count -eq 0) {
        Write-Status "No USB drives found. Please insert a USB drive."
        [System.Windows.MessageBox]::Show("Please insert a USB drive to continue.", "No USB Detected", "OK", "Warning")
        $window.Cursor = 'Arrow'
        return
    }
    $window.Cursor = 'Wait'
    Write-Status "===Preparing USB Creation==="
    Write-Status "Starting USB creation..."
    $src = $SourceFolder
    if (-not $src -or -not (Test-Path $src)) {
        Write-Status "Error: No image folder"
        $window.Cursor = 'Arrow'; return
    }
    if ($cmbUSB.SelectedItem -like "No USB*") {
        Write-Status "Error: No USB"
        [System.Windows.MessageBox]::Show("Please insert a USB drive to continue.", "No USB Detected", "OK", "Warning")
        $window.Cursor = 'Arrow'; return
    }
    $driveNumber = $cmbUSB.SelectedItem.ToString().Split(":")[0]
    $usbDrives = Get-Disk | Where-Object BusType -eq 'USB'
    $usbDisk = $usbDrives | Where-Object Number -eq $driveNumber
    if (-not $usbDisk) {
        Write-Status "Error: Invalid USB disk"
        $window.Cursor = 'Arrow'; return
    }
    if ($usbDisk.Size -lt 14GB) {
        Write-Status "Error: Selected USB drive is less than 14GB!"
        [System.Windows.MessageBox]::Show("Selected USB drive is less than 14GB. Please use a 14GB or larger drive.", "Error", "OK", "Error")
        $window.Cursor = 'Arrow'
        return
    }
    $res = [System.Windows.MessageBox]::Show("WARNING: This will ERASE all partitions and data on USB drive $driveNumber! Continue?", "Confirm", "YesNo", "Warning")
    if ($res -ne "Yes") {
        Write-Status "Aborted"
        $window.Cursor = 'Arrow'; return
    }
    
    # Show progress dialog
    $progressWindow = Show-ProgressDialog -Title "Creating USB" -Message "Preparing USB drive..."
    $progressWindow.Show()
    
    Update-Progress -Window $progressWindow -Percent 10 -Message "Removing existing partitions..."
    $existingPartitions = Get-Partition -DiskNumber $driveNumber -ErrorAction SilentlyContinue
    if ($existingPartitions) {
        foreach ($part in $existingPartitions) {
            Remove-Partition -DiskNumber $driveNumber -PartitionNumber $part.PartitionNumber -Confirm:$false -ErrorAction SilentlyContinue
        }
        Start-Sleep -Seconds 2
    }
    
    Update-Progress -Window $progressWindow -Percent 20 -Message "Initializing disk..."
    $disk = Get-Disk -Number $driveNumber
    if ($disk.PartitionStyle -eq 'RAW') {
        Initialize-Disk -Number $driveNumber -PartitionStyle MBR
        Start-Sleep -Seconds 2
    }
    
    Update-Progress -Window $progressWindow -Percent 30 -Message "Creating partition..."
    $size14GB = 14GB
    $partition = New-Partition -DiskNumber $driveNumber -Size $size14GB -AssignDriveLetter
    Start-Sleep -Seconds 1
    
    $usbLabel = "WIN11USB"
    $isoLabelFile = Join-Path $configDir "iso-label.txt"
    if (Test-Path $isoLabelFile) {
        $label = Get-Content $isoLabelFile
        if ($label -and $label.Length -le 11) {
            $usbLabel = $label
        } elseif ($label) {
            $label = $label -replace '[^a-zA-Z0-9_ ]', ''
            $usbLabel = $label.Substring(0, [Math]::Min($label.Length, 11))
        }
    }
    
    Update-Progress -Window $progressWindow -Percent 40 -Message "Formatting as FAT32..."
    Format-Volume -Partition $partition -FileSystem FAT32 -NewFileSystemLabel $usbLabel -Confirm:$false
    $usbDriveLetter = ($partition | Get-Volume).DriveLetter
    if (-not $usbDriveLetter) {
        Write-Status "Error: No drive letter"
        $progressWindow.Close()
        $window.Cursor = 'Arrow'; return
    }
    $usbRoot = "$usbDriveLetter`:\"
    
    Update-Progress -Window $progressWindow -Percent 50 -Message "Copying files to USB (this may take several minutes)..."
    robocopy "$src" "$usbRoot" /E /NJH /NJS /NP /NFL /NDL | Out-Null
    
    Update-Progress -Window $progressWindow -Percent 90 -Message "Finalizing..."
    Write-Status "USB creation complete!"
    Write-Status "Bootable Windows 11 USB (14GB FAT32, UEFI compatible) created successfully!"
    
    Update-Progress -Window $progressWindow -Percent 100 -Message "Complete!"
    Start-Sleep -Seconds 1
    $progressWindow.Close()
    $window.Cursor = 'Arrow'

    $savePrompt = [System.Windows.MessageBox]::Show(
        "Do you want to save the prepared Windows 11 image for later use?", 
        "Save Prepared Image", 
        [System.Windows.MessageBoxButton]::YesNo, 
        [System.Windows.MessageBoxImage]::Question
    )
    if ($savePrompt -eq [System.Windows.MessageBoxResult]::Yes) {
        $savedImagesDir = "C:\WinImagePrep\SavedImages"
        if (-not (Test-Path $savedImagesDir)) {
            New-Item -Path $savedImagesDir -ItemType Directory -Force | Out-Null
        }
        Add-Type -AssemblyName Microsoft.VisualBasic
        $customName = [Microsoft.VisualBasic.Interaction]::InputBox(
            "Enter a name for the saved image folder (or leave blank for timestamp):", 
            "Save Prepared Image", 
            ""
        )
        if (-not $customName -or $customName.Trim() -eq "") {
            $customName = "Image_" + (Get-Date -Format "yyyyMMdd_HHmmss")
        } else {
            $invalid = [System.IO.Path]::GetInvalidFileNameChars()
            foreach ($char in $invalid) {
                $customName = $customName -replace [Regex]::Escape([string]$char), ""
            }
        }
        $saveSubdir = Join-Path $savedImagesDir $customName
        Write-Status "Saving prepared image to $saveSubdir..."
        robocopy "$windows11Dir" "$saveSubdir" /E /NJH /NJS /NP /NFL /NDL | Out-Null
        Write-Status "Saved prepared files to $saveSubdir."
        $isoLabelFile = Join-Path $configDir "iso-label.txt"
        if (Test-Path $isoLabelFile) {
            Copy-Item $isoLabelFile -Destination $saveSubdir -Force
            Remove-Item $isoLabelFile -Force
            Write-Status "Copied image label to $saveSubdir and cleaned up config."
        }
    } else {
        Write-Status "Prepared files discarded by user choice."
        $isoLabelFile = Join-Path $configDir "iso-label.txt"
        if (Test-Path $isoLabelFile) {
            Remove-Item $isoLabelFile -Force
        }
    }
    Write-Status "Cleaning up working folders..."
    if (Test-Path $windows11Dir) {
        Get-ChildItem -Path $windows11Dir -Recurse -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path $driversDir) {
        Get-ChildItem -Path $driversDir -Recurse -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Status "Cleanup complete."

    $againPrompt = Show-CustomChoiceDialog "Would you like to quit or start again?" "All Done" "Quit" "Start Again"
    if ($againPrompt -eq 1) {
        exit
    } else {
        $txtISO.Clear()
        $txtMSI.Clear()
        $cmbUSB.SelectedIndex = -1
        $borderWarning.Visibility = [System.Windows.Visibility]::Collapsed
        Clear-Log
        Refresh-USBList
    }
}

$btnInject.Add_Click({
    $iso = $txtISO.Text.Trim()
    $msi = $txtMSI.Text.Trim()
    
    if (-not (Test-Path $iso)) { 
        [System.Windows.MessageBox]::Show("Please select a Windows 11 ISO file.", "Error", "OK", "Error")
        return 
    }
    
    if (-not (Test-Path $msi)) { 
        [System.Windows.MessageBox]::Show("Please select a driver MSI file.", "Error", "OK", "Error")
        return 
    }
    
    # Check disk space
    $spaceCheck = Test-DiskSpace -Path "C:\WinImagePrep" -RequiredGB 25
    if (-not $spaceCheck.HasSpace) {
        [System.Windows.MessageBox]::Show(
            "Insufficient disk space!`n`nRequired: $($spaceCheck.RequiredGB) GB`nAvailable: $($spaceCheck.FreeSpaceGB) GB`n`nPlease free up space and try again.",
            "Disk Space Error",
            "OK",
            "Error"
        )
        return
    }
    
    # Important time warning
    $timeWarning = [System.Windows.MessageBox]::Show(
        "IMPORTANT: This process can take 45 minutes to 1 hour or more depending on your system.`n`n" +
        "The application may appear frozen or unresponsive during driver injection - this is NORMAL.`n`n" +
        "The progress bar will update as each step completes, but individual DISM operations can take 10-15 minutes each.`n`n" +
        "Please be patient and do NOT close the application.`n`n" +
        "Do you want to continue?",
        "Time Warning - Please Read",
        [System.Windows.MessageBoxButton]::YesNo,
        [System.Windows.MessageBoxImage]::Warning
    )
    
    if ($timeWarning -ne [System.Windows.MessageBoxResult]::Yes) {
        Write-Status "Operation cancelled by user."
        return
    }
    
    $window.Cursor = 'Wait'
    $script:operationCancelled = $false
    
    # Create progress window
    $progressWindow = Show-ProgressDialog -Title "Preparing Windows Image" -Message "Initializing..."
    $progressWindow.Show()

    try {
        $dirs = @($topLevelDir, $windows11Dir, $driversDir, $mountDir, $configDir)
        foreach ($dir in $dirs) {
            if (-not (Test-Path $dir)) { New-Item -Path $dir -ItemType Directory -Force | Out-Null }
        }
        
        $mountPE = "$mountDir\WinPE"
        $mountSetup = "$mountDir\WinSetup"
        foreach ($d in @($mountPE, $mountSetup)) {
            if (-not (Test-Path $d)) { New-Item -Path $d -ItemType Directory -Force | Out-Null }
        }
        
        Update-Progress -Window $progressWindow -Percent 5 -Message "Mounting ISO..."
        Write-Status "===Mounting ISO==="
        $mountResult = Mount-DiskImage -ImagePath $iso -PassThru -StorageType ISO
        Start-Sleep -Seconds 2
        $vol = ($mountResult | Get-Volume)
        $driveLetter = $vol.DriveLetter
        if (-not $driveLetter) { 
            Write-Status "ISO mount failed!"
            throw "Failed to mount ISO"
        }

        $isoLabel = $vol.FileSystemLabel
        if (-not (Test-Path $configDir)) { New-Item -Path $configDir -ItemType Directory -Force | Out-Null }
        Set-Content -Path (Join-Path $configDir "iso-label.txt") -Value $isoLabel

        Update-Progress -Window $progressWindow -Percent 10 -Message "Copying ISO files..."
        Write-Status "Copying Files..."
        robocopy "$driveLetter`:\" $windows11Dir /E /NJH /NJS /NP /NFL /NDL | Out-Null
        
        Update-Progress -Window $progressWindow -Percent 20 -Message "Clearing read-only attributes..."
        Write-Status "Clearing ReadOnly attributes..."
        Get-ChildItem -Path $windows11Dir -Recurse -File | ForEach-Object { 
            $_.Attributes = $_.Attributes -band (-bnot [System.IO.FileAttributes]::ReadOnly) 
        }
        Dismount-DiskImage -ImagePath $iso

        # Process driver source
        Update-Progress -Window $progressWindow -Percent 25 -Message "Extracting drivers from MSI..."
        Write-Status "===Extracting Driver MSI==="
        if (Test-Path $driversDir) { 
            Remove-Item -Path $driversDir\* -Recurse -Force -ErrorAction SilentlyContinue 
        }
        
        Write-Status "Extracting MSI: $msi"
        $exitCode = Invoke-SilentCommand -FilePath "msiexec.exe" -ArgumentList "/a `"$msi`" /qn TARGETDIR=`"$driversDir`"" -Wait
        if ($exitCode -ne 0) {
            throw "MSI extraction failed with exit code $exitCode"
        }
        
        # Verify drivers
        $driverValidation = Test-DriverValidity -DriverPath $driversDir
        if (-not $driverValidation.Valid) {
            throw "No valid drivers found in the MSI file"
        }
        Write-Status "Found $($driverValidation.DriverCount) driver(s): $($driverValidation.SignedCount) signed, $($driverValidation.UnsignedCount) unsigned"

        $sources = "$windows11Dir\Sources"
        $bootWim = "$sources\boot.wim"
        $installWim = "$sources\install.wim"

        # WinPE
        if ($script:operationCancelled) { throw "Operation cancelled by user" }
        Update-Progress -Window $progressWindow -Percent 35 -Message "Adding drivers to WinPE..."
        Write-Status "===Adding Drivers to WinPE==="
        Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Mount-Wim /WimFile:`"$bootWim`" /index:1 /MountDir:`"$mountPE`"" -Wait | Out-Null
        Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Image:`"$mountPE`" /Add-Driver /Driver:`"$driversDir`" /Recurse" -Wait | Out-Null
        Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Unmount-Wim /MountDir:`"$mountPE`" /Commit" -Wait | Out-Null

        # WinSetup
        if ($script:operationCancelled) { throw "Operation cancelled by user" }
        Update-Progress -Window $progressWindow -Percent 45 -Message "Adding drivers to Windows Setup..."
        Write-Status "===Adding Drivers to Windows Setup==="
        Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Mount-Wim /WimFile:`"$bootWim`" /index:2 /MountDir:`"$mountSetup`"" -Wait | Out-Null
        Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Image:`"$mountSetup`" /Add-Driver /Driver:`"$driversDir`" /Recurse" -Wait | Out-Null
        Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Unmount-Wim /MountDir:`"$mountSetup`" /Commit" -Wait | Out-Null

        # Process install.wim editions
        $editions = Get-WimEditions -WimPath $installWim
        $editionsToProcess = if ($script:selectedEditions) { $script:selectedEditions } else { $editions.ImageIndex }
        
        $editionCount = 0
        $totalEditions = $editionsToProcess.Count
        
        foreach ($editionIndex in $editionsToProcess) {
            if ($script:operationCancelled) { throw "Operation cancelled by user" }
            
            $editionCount++
            $editionInfo = $editions | Where-Object { $_.ImageIndex -eq $editionIndex }
            $editionName = $editionInfo.ImageName
            $baseProgress = 50 + (($editionCount - 1) / $totalEditions * 40)
            
            Update-Progress -Window $progressWindow -Percent $baseProgress -Message "Processing: $editionName (Edition $editionCount of $totalEditions)..."
            Write-Status "===Processing Edition $editionIndex - $editionName==="
            
            $mountEdition = "$mountDir\Edition_$editionIndex"
            if (-not (Test-Path $mountEdition)) { New-Item -Path $mountEdition -ItemType Directory -Force | Out-Null }
            
            Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Mount-Wim /WimFile:`"$installWim`" /index:$editionIndex /MountDir:`"$mountEdition`"" -Wait | Out-Null
            Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Image:`"$mountEdition`" /Add-Driver /Driver:`"$driversDir`" /Recurse" -Wait | Out-Null
            
            # Check for WinRE
            $winreWim = Join-Path $mountEdition "Windows\System32\Recovery\Winre.wim"
            if (Test-Path $winreWim) {
                Write-Status "Processing WinRE for $editionName..."
                $mountWinRE = "$mountDir\WinRE_$editionIndex"
                if (-not (Test-Path $mountWinRE)) { New-Item -Path $mountWinRE -ItemType Directory -Force | Out-Null }
                
                Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Mount-Wim /WimFile:`"$winreWim`" /index:1 /MountDir:`"$mountWinRE`"" -Wait | Out-Null
                Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Image:`"$mountWinRE`" /Add-Driver /Driver:`"$driversDir`" /Recurse" -Wait | Out-Null
                Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Unmount-Wim /MountDir:`"$mountWinRE`" /Commit" -Wait | Out-Null
                Remove-Item -Path $mountWinRE -Recurse -Force -ErrorAction SilentlyContinue
            }
            
            Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Unmount-Wim /MountDir:`"$mountEdition`" /Commit" -Wait | Out-Null
            Remove-Item -Path $mountEdition -Recurse -Force -ErrorAction SilentlyContinue
        }

        # Split WIM if needed
        Update-Progress -Window $progressWindow -Percent 92 -Message "Checking install.wim size..."
        $wimPath = "$sources\install.wim"
        $swmBasePath = "$sources\install.swm"
        if (Test-Path $wimPath) {
            $wimInfo = Get-Item $wimPath
            if ($wimInfo.Length -gt 4GB) {
                Write-Status "Splitting install.wim (>4GB)..."
                Get-ChildItem -Path $sources -Filter "install*.swm" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
                Invoke-SilentCommand -FilePath "dism.exe" -ArgumentList "/Split-Image /ImageFile:`"$wimPath`" /SWMFile:`"$swmBasePath`" /FileSize:3800" -Wait | Out-Null
                if (Test-Path "$swmBasePath") {
                    Remove-Item $wimPath -Force
                    Write-Status "install.wim split successful"
                }
            }
        }
        
        Update-Progress -Window $progressWindow -Percent 100 -Message "Complete!"
        Write-Status "===Image Preparation Complete==="
        Start-Sleep -Seconds 1
        $progressWindow.Close()

    } catch {
        Write-Status "ERROR: $($_.Exception.Message)"
        [System.Windows.MessageBox]::Show("Error during image preparation: $($_.Exception.Message)", "Error", "OK", "Error")
        $progressWindow.Close()
        Invoke-Cleanup
        $window.Cursor = 'Arrow'
        return
    }
    
    $window.Cursor = 'Arrow'
    
    # Prompt for USB creation
    do {
        $script:usbDrives = Get-Disk | Where-Object BusType -eq 'USB'
        Refresh-USBList
        $usbPresent = ($script:usbDrives | Measure-Object).Count -gt 0
        $driveList = $script:usbDrives | ForEach-Object { "$($_.Number): $($_.FriendlyName) - $([math]::Round($_.Size/1GB,1)) GB" }
        $driveMsg = if ($driveList) { "`n`nDetected USBs:`n" + ($driveList -join "`n") } else { "" }
        if (-not $usbPresent) {
            [System.Windows.MessageBox]::Show("Please insert a USB drive to continue.$driveMsg", "Insert USB", "OK", "Warning")
            Start-Sleep -Seconds 3
        }
    } while (-not $usbPresent)
    Start-Sleep -Seconds 2
    Refresh-USBList

    $go = [System.Windows.MessageBox]::Show("Image preparation complete. Ready to create Bootable USB. Click OK to continue.", "Ready to Create USB", "OK", "Information")
    if ($go -eq "OK") {
        if ($cmbUSB.SelectedIndex -lt 0 -or $cmbUSB.SelectedItem -like "No USB*") {
            $cmbUSB.SelectedIndex = 0
        }
        New-BootableWin11USB -SourceFolder $windows11Dir -cmbUSB $cmbUSB -window $window
        return
    }
})

# Startup message
Write-Status "Windows 11 Image Preparation Tool V3 - Ready"
Write-Status "Features: Silent processing, Enhanced UI, Detailed drive info"
Write-Status "======================="

$window.ShowDialog() | Out-Null

# Show console on exit
[Console.Window]::ShowWindow($consolePtr, 5) # 5 = show
