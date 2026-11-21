using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using NOVA.IDE.Packaging;

namespace NOVA.IDE;

public partial class MainWindow : Window
{
    private string? _projectDirectory;

    public MainWindow()
    {
        InitializeComponent();
        CheckToolchain();
    }

    private static string? FindOnPath(string executable)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder.Trim('"'), executable);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private void CheckToolchain()
    {
        CmakeStatus.Text = $"CMake: {(FindOnPath("cmake.exe") is null ? "missing" : "available")}";
        NinjaStatus.Text = $"Ninja: {(FindOnPath("ninja.exe") is null ? "missing" : "available")}";
        ArmStatus.Text = $"ARM GCC: {(FindOnPath("arm-none-eabi-gcc.exe") is null ? "missing" : "available")}";
        var sdk = Environment.GetEnvironmentVariable("PICO_SDK_PATH");
        SdkStatus.Text = $"Pico SDK: {(Directory.Exists(sdk) ? sdk : "PICO_SDK_PATH not configured")}";
    }

    private void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select NOVA project folder" };
        if (dialog.ShowDialog() != true) return;
        if (!File.Exists(Path.Combine(dialog.FolderName, "CMakeLists.txt")))
        {
            MessageBox.Show("This folder does not contain CMakeLists.txt.", "Not a NOVA/CMake project", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _projectDirectory = dialog.FolderName;
        ProjectPathText.Text = _projectDirectory;
        StatusText.Text = "Project loaded. Select a target and build.";
    }

    private async void Build_Click(object sender, RoutedEventArgs e)
    {
        if (_projectDirectory is null)
        {
            MessageBox.Show("Open a project folder first.", "NOVA IDE", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var cmake = FindOnPath("cmake.exe");
        if (cmake is null || FindOnPath("ninja.exe") is null)
        {
            MessageBox.Show("CMake and Ninja must be installed and available on PATH.", "Toolchain missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var sdk = Environment.GetEnvironmentVariable("PICO_SDK_PATH");
        if (string.IsNullOrWhiteSpace(sdk) || !Directory.Exists(sdk))
        {
            MessageBox.Show("Set PICO_SDK_PATH to a valid local Raspberry Pi Pico SDK directory.", "Pico SDK missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var isPico2 = TargetBox.SelectedIndex == 1;
        var board = isPico2 ? "pico2" : "pico";
        var platform = isPico2 ? "rp2350-arm-s" : "rp2040";
        var buildDirectory = Path.Combine(_projectDirectory, "ide-build-" + board);
        OutputBox.Clear();
        StatusText.Text = $"Configuring {board}...";
        var configure = await RunProcess(cmake, $"-S . -B \"{buildDirectory}\" -G Ninja -DPICO_BOARD={board} -DPICO_PLATFORM={platform} -DPICO_SDK_PATH=\"{sdk}\" -DCMAKE_BUILD_TYPE=Release", _projectDirectory);
        if (configure != 0) { StatusText.Text = "CMake configuration failed."; return; }
        StatusText.Text = "Compiling...";
        var build = await RunProcess(cmake, $"--build \"{buildDirectory}\" --parallel", _projectDirectory);
        StatusText.Text = build == 0 ? $"Build succeeded: {buildDirectory}" : "Build failed. See output.";
    }

    private void Package_Click(object sender, RoutedEventArgs e)
    {
        var open = new OpenFileDialog
        {
            Title = "Select NOVA application ELF or BIN",
            Filter = "NOVA application input (*.elf;*.bin)|*.elf;*.bin|ARM ELF32 (*.elf)|*.elf|Raw binary (*.bin)|*.bin"
        };
        if (open.ShowDialog() != true) return;
        var target = TargetBox.SelectedIndex == 1 ? NovaTarget.Rp2350ArmM33 : NovaTarget.Rp2040;
        uint loadAddress = 0;
        if (Path.GetExtension(open.FileName).Equals(".bin", StringComparison.OrdinalIgnoreCase))
        {
            var text = LoadAddressBox.Text.Trim();
            if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
            if (!uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out loadAddress) || loadAddress == 0)
            {
                MessageBox.Show("Enter a valid non-zero hexadecimal BIN load address.", "Invalid load address", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
        var save = new SaveFileDialog { Title = "Save NOVA application package", Filter = "NOVA application (*.nova)|*.nova", FileName = Path.GetFileNameWithoutExtension(open.FileName) + ".nova" };
        if (save.ShowDialog() != true) return;
        try
        {
            var name = string.IsNullOrWhiteSpace(AppNameBox.Text) ? Path.GetFileNameWithoutExtension(open.FileName) : AppNameBox.Text.Trim();
            var version = string.IsNullOrWhiteSpace(AppVersionBox.Text) ? "1.0.0" : AppVersionBox.Text.Trim();
            var info = NovaPackage.Create(open.FileName, save.FileName, name, version, target, 1u, loadAddress);
            OutputBox.AppendText($"Created {save.FileName}\nTarget: {info.Target}\nKind: {info.ImageKind}\nPayload: {info.PayloadLength} bytes\nSHA-256: {info.Sha256}\n");
            StatusText.Text = "NOVA package created. Runtime compatibility is not yet certified.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Package creation failed.";
            MessageBox.Show(ex.Message, "NOVA package error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void InspectPackage_Click(object sender, RoutedEventArgs e)
    {
        var open = new OpenFileDialog { Title = "Inspect NOVA package", Filter = "NOVA application (*.nova)|*.nova" };
        if (open.ShowDialog() != true) return;
        try
        {
            var info = NovaPackage.Inspect(open.FileName);
            OutputBox.AppendText($"Verified {open.FileName}\nName: {info.Name}\nVersion: {info.Version}\nTarget: {info.Target}\nABI: {info.AbiVersion}\nLoad: 0x{info.LoadAddress:X8}\nPayload: {info.PayloadLength} bytes\nSHA-256: {info.Sha256}\n");
            StatusText.Text = "Package structure and payload hash verified.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Package verification failed.";
            MessageBox.Show(ex.Message, "NOVA package error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    private void InstallPackage_Click(object sender, RoutedEventArgs e)
    {
        var open = new OpenFileDialog { Title = "Select NOVA package to install", Filter = "NOVA application (*.nova)|*.nova" };
        if (open.ShowDialog() != true) return;
        var folder = new OpenFolderDialog { Title = "Select the root of the NOVA SD card" };
        if (folder.ShowDialog() != true) return;
        try
        {
            var root = Path.GetPathRoot(folder.FolderName);
            if (string.IsNullOrWhiteSpace(root)) throw new InvalidOperationException("Cannot identify the selected drive.");
            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.DriveType != DriveType.Removable)
                throw new InvalidOperationException("Select a mounted removable SD-card drive.");
            if (!drive.DriveFormat.Equals("FAT32", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"NOVA currently requires FAT32; selected drive is {drive.DriveFormat}.");
            var info = NovaPackage.Inspect(open.FileName);
            if (TargetBox.SelectedIndex == 0 && info.Target != NovaTarget.Rp2040 ||
                TargetBox.SelectedIndex == 1 && info.Target != NovaTarget.Rp2350ArmM33)
                throw new InvalidOperationException("Package target does not match the selected IDE target.");
            var apps = Path.Combine(folder.FolderName, "NOVA", "APPS");
            Directory.CreateDirectory(apps);
            var destination = Path.Combine(apps, Path.GetFileName(open.FileName));
            File.Copy(open.FileName, destination, overwrite: true);
            OutputBox.AppendText($"Installed package file: {destination}\nPackage: {info.Name} {info.Version}\n");
            StatusText.Text = "Package copied to SD. Runtime execution is not yet enabled in this firmware.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "SD installation failed.";
            MessageBox.Show(ex.Message, "NOVA SD installer", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task<int> RunProcess(string executable, string arguments, string workingDirectory)
    {
        var start = new ProcessStartInfo(executable, arguments) { WorkingDirectory = workingDirectory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        using var process = new Process { StartInfo = start };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Dispatcher.Invoke(() => OutputBox.AppendText(e.Data + Environment.NewLine)); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Dispatcher.Invoke(() => OutputBox.AppendText(e.Data + Environment.NewLine)); };
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
