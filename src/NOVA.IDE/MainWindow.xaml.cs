using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;

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
        StatusText.Text = "Compiling firmware...";
        var build = await RunProcess(cmake, $"--build \"{buildDirectory}\" --parallel", _projectDirectory);
        StatusText.Text = build == 0 ? $"Build succeeded: {buildDirectory}" : "Build failed. See output.";
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
