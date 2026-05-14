using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using RockETDock.App.Models;
using RockETDock.App.Services;

namespace RockETDock.App;

public partial class App : System.Windows.Application
{
    private const string SettingsPipeName = "RockETDock.Settings";

    private DockConfigurationStore? _store;
    private readonly List<MainWindow> _windows = [];
    private readonly NativeTaskbarController _nativeTaskbar = new();
    private readonly WindowAnimationController _windowAnimations = new();
    private WindowMinimizeMonitor? _windowMonitor;
    private CancellationTokenSource? _settingsPipeCancellation;
    private bool _settingsOnlyMode;
    private bool _shutdownRequested;
    private bool _nativeTaskbarWasHiddenThisSession;
    private bool _nativeTaskbarRestoredForShutdown;
    private bool _windowAnimationsRestoredForShutdown;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        _store = new DockConfigurationStore();
        var configuration = _store.Load();

        _settingsOnlyMode = ShouldOpenSettingsOnly(e.Args);
        if (_settingsOnlyMode && TryRequestSettingsFromRunningApp())
        {
            Shutdown();
            return;
        }

        if (_settingsOnlyMode)
        {
            ShowSettingsOnly(configuration);
            return;
        }

        foreach (var bar in configuration.Bars)
        {
            ShowBar(bar);
        }

        StartSettingsPipeServer();
        RefreshGlobalServices();
    }

    internal void CreateBar(DockEdge edge)
    {
        CreateBar(edge, showWindow: !_settingsOnlyMode);
    }

    internal void CreateBar(DockEdge edge, bool showWindow)
    {
        if (_store is null)
        {
            return;
        }

        var configuration = _store.Current;
        var text = TextCatalog.Get(configuration.App.Language);
        var baseName = edge switch
        {
            DockEdge.Left => text["BarLeft"],
            DockEdge.Right => text["BarRight"],
            DockEdge.Top => text["BarTop"],
            DockEdge.Bottom => text["BarBottom"],
            _ => text["BarGeneric"]
        };

        var bar = DockBarSettings.Create(GetUniqueBarName(configuration, baseName), edge);
        configuration.Bars.Add(bar);
        _store.Save();
        if (showWindow)
        {
            ShowBar(bar);
        }
    }

    internal void SaveConfiguration()
    {
        _store?.Save();
    }

    internal void RemoveBar(MainWindow window, DockBarSettings bar)
    {
        if (_store is null)
        {
            return;
        }

        if (_store.Current.Bars.Count > 1)
        {
            _store.Current.Bars.Remove(bar);
        }

        _store.Save();
        _windows.Remove(window);
        window.Close();
    }

    internal void ExitAll()
    {
        SaveConfiguration();
        ShutdownApplication();
    }

    internal void RefreshGlobalServices()
    {
        if (_store is null)
        {
            return;
        }

        var hideNativeTaskbar = _store.Current.App.HideNativeTaskbar;
        _nativeTaskbar.Apply(hideNativeTaskbar);
        _nativeTaskbarWasHiddenThisSession |= hideNativeTaskbar;
        _windowAnimations.Apply(_store.Current.App.DisableMinimizeAnimations);

        if (_store.Current.App.MinimizeWindowsToDock)
        {
            _windowMonitor ??= new WindowMinimizeMonitor(AddMinimizedWindow, RemoveRuntimeWindow);
            _windowMonitor.Start();
        }
        else
        {
            _windowMonitor?.Stop();
            foreach (var window in _windows)
            {
                window.ClearRuntimeWindows();
            }
        }
    }

    internal void EnsureGlobalHotKeyRegistration()
    {
        foreach (var window in _windows.ToArray())
        {
            if (window.TryRegisterGlobalHotKey())
            {
                return;
            }
        }
    }

    internal void ToggleDockVisibilityByHotKey()
    {
        foreach (var window in _windows.ToArray())
        {
            window.ToggleVisibilityByHotKey();
        }
    }

    internal void RemoveRuntimeWindow(long nativeWindowHandle)
    {
        Dispatcher.BeginInvoke(() =>
        {
            foreach (var window in _windows.ToArray())
            {
                window.RemoveRuntimeWindow(nativeWindowHandle);
            }
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        RestoreNativeTaskbarForShutdown();
        RestoreWindowAnimationsForShutdown();
        AppDomain.CurrentDomain.ProcessExit -= CurrentDomain_ProcessExit;
        _settingsPipeCancellation?.Cancel();
        _settingsPipeCancellation?.Dispose();
        _windowMonitor?.Dispose();
        base.OnExit(e);
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        RestoreNativeTaskbarForShutdown();
        RestoreWindowAnimationsForShutdown();
        base.OnSessionEnding(e);
    }

    private static void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        RuntimeLog.Write(e.Exception, "DispatcherUnhandledException");
        e.Handled = true;
    }

    private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            RuntimeLog.Write(exception, "UnhandledException");
        }
    }

    private static void TaskScheduler_UnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
    {
        RuntimeLog.Write(e.Exception, "UnobservedTaskException");
        e.SetObserved();
    }

    private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
    {
        RestoreNativeTaskbarForShutdown();
        RestoreWindowAnimationsForShutdown();
    }

    private void ShowBar(DockBarSettings bar)
    {
        if (_store is null)
        {
            return;
        }

        UserPaths.EnsureBarFolder(bar.Name);

        var window = new MainWindow(_store, bar);
        window.Closed += HandleBarClosed;
        _windows.Add(window);
        window.Show();
    }

    private void HandleBarClosed(object? sender, EventArgs e)
    {
        if (sender is MainWindow window)
        {
            _windows.Remove(window);
        }

        if (!_settingsOnlyMode && _windows.Count == 0)
        {
            ShutdownApplication();
        }
    }

    private void ShutdownApplication()
    {
        if (_shutdownRequested)
        {
            return;
        }

        _shutdownRequested = true;
        RestoreNativeTaskbarForShutdown();
        RestoreWindowAnimationsForShutdown();
        Shutdown();
    }

    private void RestoreNativeTaskbarForShutdown()
    {
        if (_nativeTaskbarRestoredForShutdown)
        {
            return;
        }

        _nativeTaskbarRestoredForShutdown = true;

        try
        {
            if (_nativeTaskbarWasHiddenThisSession)
            {
                _nativeTaskbar.ForceRestore();
                return;
            }

            _nativeTaskbar.Restore();
        }
        catch (Exception ex)
        {
            RuntimeLog.Write(ex, "RestoreNativeTaskbarForShutdown");
        }
    }

    private void RestoreWindowAnimationsForShutdown()
    {
        if (_windowAnimationsRestoredForShutdown)
        {
            return;
        }

        _windowAnimationsRestoredForShutdown = true;

        try
        {
            _windowAnimations.Restore();
        }
        catch (Exception ex)
        {
            RuntimeLog.Write(ex, "RestoreWindowAnimationsForShutdown");
        }
    }

    private void ShowSettingsOnly(DockConfiguration configuration)
    {
        if (_store is null)
        {
            Shutdown();
            return;
        }

        var bar = configuration.Bars.FirstOrDefault();
        if (bar is null)
        {
            Shutdown();
            return;
        }

        var settingsWindow = new SettingsWindow(_store, bar)
        {
            ShowInTaskbar = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        settingsWindow.CreateBarRequested += (_, edge) => CreateBar(edge, showWindow: false);
        settingsWindow.Closed += (_, _) => Shutdown();
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private void StartSettingsPipeServer()
    {
        _settingsPipeCancellation = new CancellationTokenSource();
        _ = Task.Run(() => RunSettingsPipeServerAsync(_settingsPipeCancellation.Token));
    }

    private async Task RunSettingsPipeServerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    SettingsPipeName,
                    PipeDirection.InOut,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(server);
                using var writer = new StreamWriter(server) { AutoFlush = true };
                var command = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (string.Equals(command, "open-settings", StringComparison.OrdinalIgnoreCase))
                {
                    await Dispatcher.InvokeAsync(OpenSettingsFromExternalRequest);
                    await writer.WriteLineAsync("ok").ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                RuntimeLog.Write(ex, "RunSettingsPipeServerAsync");
            }
        }
    }

    private static bool TryRequestSettingsFromRunningApp()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", SettingsPipeName, PipeDirection.InOut, PipeOptions.None);
            client.Connect(300);
            using var reader = new StreamReader(client);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine("open-settings");
            return string.Equals(reader.ReadLine(), "ok", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private void OpenSettingsFromExternalRequest()
    {
        var window = _windows.FirstOrDefault();
        if (window is not null)
        {
            window.OpenSettings();
            return;
        }

        if (_store is not null)
        {
            ShowSettingsOnly(_store.Current);
        }
    }

    private static bool ShouldOpenSettingsOnly(string[] args)
    {
        if (args.Any(static arg =>
                arg.Equals("--settings", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/settings", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var processPath = Environment.ProcessPath;
        var executableName = string.IsNullOrWhiteSpace(processPath)
            ? ""
            : Path.GetFileNameWithoutExtension(processPath);
        return executableName.Contains("Settings", StringComparison.OrdinalIgnoreCase);
    }

    private void AddMinimizedWindow(DockItem item)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var targetWindow = _windows.FirstOrDefault();
            targetWindow?.AddRuntimeWindow(item);
        });
    }

    private static string GetUniqueBarName(DockConfiguration configuration, string baseName)
    {
        var existing = configuration.Bars
            .Select(static bar => bar.Name)
            .ToHashSet(StringComparer.CurrentCultureIgnoreCase);

        if (!existing.Contains(baseName))
        {
            return baseName;
        }

        for (var index = 2; ; index++)
        {
            var candidate = $"{baseName} {index}";
            if (!existing.Contains(candidate))
            {
                return candidate;
            }
        }
    }
}
