using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Dock.App.Models;
using Dock.App.Services;

namespace Dock.App;

public partial class App : System.Windows.Application
{
    private DockConfigurationStore? _store;
    private readonly List<MainWindow> _windows = [];
    private readonly NativeTaskbarController _nativeTaskbar = new();
    private WindowMinimizeMonitor? _windowMonitor;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        _store = new DockConfigurationStore();
        var configuration = _store.Load();

        foreach (var bar in configuration.Bars)
        {
            ShowBar(bar);
        }

        RefreshGlobalServices();
    }

    internal void CreateBar(DockEdge edge)
    {
        if (_store is null)
        {
            return;
        }

        var configuration = _store.Current;
        var baseName = edge switch
        {
            DockEdge.Left => "Barra Esquerda",
            DockEdge.Right => "Barra Direita",
            DockEdge.Top => "Barra Superior",
            DockEdge.Bottom => "Barra Inferior",
            _ => "Barra"
        };

        var bar = DockBarSettings.Create(GetUniqueBarName(configuration, baseName), edge);
        configuration.Bars.Add(bar);
        _store.Save();
        ShowBar(bar);
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

        if (_windows.Count == 0)
        {
            Shutdown();
        }
    }

    internal void ExitAll()
    {
        SaveConfiguration();
        Shutdown();
    }

    internal void RefreshGlobalServices()
    {
        if (_store is null)
        {
            return;
        }

        _nativeTaskbar.Apply(_store.Current.App.HideNativeTaskbar);

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
        _windowMonitor?.Dispose();
        _nativeTaskbar.Restore();
        base.OnExit(e);
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

    private void ShowBar(DockBarSettings bar)
    {
        if (_store is null)
        {
            return;
        }

        UserPaths.EnsureBarFolder(bar.Name);

        var window = new MainWindow(_store, bar);
        window.Closed += (_, _) => _windows.Remove(window);
        _windows.Add(window);
        window.Show();
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
