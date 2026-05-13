using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Dock.App.Models;
using Dock.App.Services;
using Dock.App.ViewModels;
using Forms = System.Windows.Forms;

namespace Dock.App;

public partial class MainWindow : Window
{
    private const string ItemDragFormat = "RockEtDockItemId";
    private const int SwpNoSize = 0x0001;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoActivate = 0x0010;
    private const int SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndBottom = new(1);
    private static readonly IntPtr HwndNoTopMost = new(-2);

    private readonly DockConfigurationStore _store;
    private readonly DockBarSettings _bar;
    private readonly DockBarViewModel _viewModel;
    private readonly DockItemImporter _importer = new();
    private readonly DockItemExporter _exporter = new();
    private readonly DispatcherTimer _autoHideTimer = new();
    private readonly DispatcherTimer _popupTimer = new();
    private readonly DispatcherTimer _reorderDragTimer = new();
    private double _visibleLeft;
    private double _visibleTop;
    private bool _isDockHidden;
    private bool _isPointerInside;
    private Point _dragStartPoint;
    private DockItemViewModel? _pendingDragItem;
    private DockItemViewModel? _draggedItemViewModel;
    private Button? _draggedButton;
    private Popup? _dragPreviewPopup;
    private Border? _dragPreviewShell;
    private bool _isReorderDragActive;
    private bool _isCompletingReorderDrag;
    private string? _draggedItemId;
    private int _dragStartIndex = -1;
    private DateTime _suppressClickUntilUtc;
    private bool _isOpeningNativeWindowsMenu;
    private bool _windowsButtonStartWasOpenOnMouseDown;
    private bool _startMenuOpenedByDock;
    private DateTime _startMenuOpenedByDockAtUtc;
    private SettingsWindow? _settingsWindow;
    private bool _isHoverZoomActive;
    private bool _isHoverZoomSettling;
    private Point _hoverZoomPointer;
    private TimeSpan _lastHoverRenderingTime;

    public MainWindow(DockConfigurationStore store, DockBarSettings bar)
    {
        _store = store;
        _bar = bar;
        _viewModel = new DockBarViewModel(bar);

        InitializeComponent();

        Title = $"{UserPaths.AppName} - {bar.Name}";
        DataContext = _viewModel;
        ContextMenu = BuildContextMenu();

        _autoHideTimer.Tick += (_, _) =>
        {
            _autoHideTimer.Stop();
            if (!_isPointerInside && _bar.AutoHide)
            {
                HideDock();
            }
        };
        _popupTimer.Tick += (_, _) =>
        {
            _popupTimer.Stop();
            ShowDock();
        };
        _reorderDragTimer.Interval = TimeSpan.FromMilliseconds(16);
        _reorderDragTimer.Tick += (_, _) => PollReorderDrag();

        Loaded += (_, _) =>
        {
            ApplyBarSettings();
            PositionDock();
        };
        SizeChanged += (_, _) => PositionDock();
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        PreviewMouseMove += MainWindow_ReorderMouseMove;
        PreviewMouseLeftButtonUp += MainWindow_ReorderMouseLeftButtonUp;
        LostMouseCapture += MainWindow_ReorderLostMouseCapture;
        DockShell.MouseEnter += (_, _) => HandleDockMouseEnter();
        DockShell.MouseLeave += (_, _) => HandleDockMouseLeave();
        DockShell.MouseMove += DockShell_MouseMove;
        DockShell.DragEnter += DockShell_DragEnter;
        DockShell.DragOver += DockShell_DragOver;
        DockShell.DragLeave += DockShell_DragLeave;
        DockShell.Drop += DockShell_Drop;
        CompositionTarget.Rendering += CompositionTarget_Rendering;
        Closed += (_, _) => CompositionTarget.Rendering -= CompositionTarget_Rendering;
    }

    private void DockItem_Click(object sender, RoutedEventArgs e)
    {
        if (DateTime.UtcNow <= _suppressClickUntilUtc)
        {
            e.Handled = true;
            return;
        }

        if (sender is FrameworkElement { DataContext: DockItemViewModel itemViewModel })
        {
            try
            {
                if (itemViewModel.IsWindowsButton)
                {
                    ToggleWindowsStartMenu();
                    e.Handled = true;
                    return;
                }

                if (itemViewModel.IsAnimatedGif || itemViewModel.IsDropPlaceholder)
                {
                    e.Handled = true;
                    return;
                }

                var opened = DockLauncher.Open(itemViewModel.Item);
                if (opened && itemViewModel.Item.Kind == DockItemKind.Window)
                {
                    _viewModel.RemoveRuntimeWindow(itemViewModel.Item.NativeWindowHandle);
                    PositionDock();
                }
            }
            catch (Exception ex)
            {
                RuntimeLog.Write(ex, "DockItem_Click");
            }
        }
    }

    private void ToggleWindowsStartMenu()
    {
        if (_windowsButtonStartWasOpenOnMouseDown ||
            WindowsButtonService.IsStartMenuOpen() ||
            IsStartMenuLikelyOpenedByDock())
        {
            WindowsButtonService.CloseStartMenu();
            _startMenuOpenedByDock = false;
            return;
        }

        WindowsButtonService.OpenStartMenu();
        _startMenuOpenedByDock = true;
        _startMenuOpenedByDockAtUtc = DateTime.UtcNow;
    }

    private bool IsStartMenuLikelyOpenedByDock()
    {
        return _startMenuOpenedByDock &&
               DateTime.UtcNow - _startMenuOpenedByDockAtUtc < TimeSpan.FromSeconds(30);
    }

    private void DockItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
        _pendingDragItem = sender is FrameworkElement { DataContext: DockItemViewModel itemViewModel }
            ? itemViewModel
            : null;
        _windowsButtonStartWasOpenOnMouseDown = _pendingDragItem?.IsWindowsButton == true &&
                                                (WindowsButtonService.IsStartMenuOpen() || IsStartMenuLikelyOpenedByDock());
        TraceDrag($"down item={_pendingDragItem?.Item.Id ?? "<none>"} point={_dragStartPoint.X:0},{_dragStartPoint.Y:0}");
    }

    private void DockItem_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isReorderDragActive)
        {
            return;
        }

        _pendingDragItem = null;
    }

    private void DockItem_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_isReorderDragActive ||
            _bar.LockItems ||
            _pendingDragItem is null ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (sender is not Button button)
        {
            return;
        }

        TraceDrag($"threshold item={_pendingDragItem.Item.Id} point={current.X:0},{current.Y:0}");
        StartInternalReorderDrag(button, _pendingDragItem, current);
        e.Handled = true;
    }

    private void StartInternalReorderDrag(Button draggedButton, DockItemViewModel draggedItem, Point windowPosition)
    {
        ResetHoverZoom(immediate: true);
        _draggedButton = draggedButton;
        _draggedItemViewModel = draggedItem;
        BeginReorderDrag(draggedItem.Item.Id);

        if (!TryCaptureReorderMouse())
        {
            TraceDrag($"capture-failed item={draggedItem.Item.Id}");
            EndReorderDrag();
            _draggedButton = null;
            _draggedItemViewModel = null;
            _pendingDragItem = null;
            RunOleReorderDragFallback(draggedButton, draggedItem, windowPosition);
            return;
        }

        TraceDrag($"capture-ok item={draggedItem.Item.Id} captured={Mouse.Captured?.GetType().Name ?? "<none>"}");
        BeginHeldItemAnimation(draggedItem, draggedButton);
        DockItemsControl.UpdateLayout();
        ShowDragPreview(draggedItem, windowPosition);
        UpdateReorderPlaceholderFromPointer(Mouse.GetPosition(DockItemsControl));
        _reorderDragTimer.Start();
        _suppressClickUntilUtc = DateTime.UtcNow.AddMilliseconds(250);
    }

    private bool TryCaptureReorderMouse()
    {
        return Mouse.Capture(this, CaptureMode.SubTree) ||
               Mouse.Capture(DockRoot, CaptureMode.SubTree);
    }

    private void RunOleReorderDragFallback(Button draggedButton, DockItemViewModel draggedItem, Point windowPosition)
    {
        var draggedItemId = draggedItem.Item.Id;
        var data = new DataObject(ItemDragFormat, draggedItemId);
        _draggedButton = draggedButton;
        _draggedItemViewModel = draggedItem;
        BeginReorderDrag(draggedItemId);
        BeginHeldItemAnimation(draggedItem, draggedButton);
        DockItemsControl.UpdateLayout();
        ShowDragPreview(draggedItem, windowPosition);

        try
        {
            var result = DragDrop.DoDragDrop(draggedButton, data, System.Windows.DragDropEffects.Move);
            if (result == System.Windows.DragDropEffects.Move)
            {
                _viewModel.PersistVisualOrder();
                _store.Save();
                _suppressClickUntilUtc = DateTime.UtcNow.AddMilliseconds(250);
                AnimateDropSettle(draggedItemId);
            }
            else
            {
                RestoreDraggedItemToStart(draggedItemId);
            }
        }
        finally
        {
            HideDragPreview();
            EndHeldItemAnimation(draggedItem, draggedButton);
            EndReorderDrag();
            _draggedButton = null;
            _draggedItemViewModel = null;
            _pendingDragItem = null;
        }
    }

    private void MainWindow_ReorderMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isReorderDragActive)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            TraceDrag("move-saw-button-up");
            CompleteInternalReorderDrag(commit: true);
            e.Handled = true;
            return;
        }

        UpdateDragPreview(e.GetPosition(this));
        UpdateReorderPlaceholderFromPointer(e.GetPosition(DockItemsControl));
        e.Handled = true;
    }

    private void PollReorderDrag()
    {
        if (!_isReorderDragActive)
        {
            _reorderDragTimer.Stop();
            return;
        }

        if (GetCursorPos(out var cursorPosition))
        {
            var screenPoint = new Point(cursorPosition.X, cursorPosition.Y);
            if (ShouldStartExternalShellDrag(screenPoint))
            {
                StartExternalShellDrag();
                return;
            }

            var windowPoint = PointFromScreen(screenPoint);
            UpdateDragPreview(windowPoint);
            UpdateReorderPlaceholderFromPointer(DockItemsControl.PointFromScreen(screenPoint));
        }

        if (!IsLeftMouseButtonDown())
        {
            TraceDrag("poll-saw-button-up");
            CompleteInternalReorderDrag(commit: true);
        }
    }

    private bool ShouldStartExternalShellDrag(Point screenPoint)
    {
        return _draggedItemViewModel is not null &&
               CanStartExternalShellDrag(_draggedItemViewModel.Item) &&
               IsScreenPointOutsideDockShell(screenPoint, tolerance: 22);
    }

    private static bool CanStartExternalShellDrag(DockItem item)
    {
        if (item.Kind is DockItemKind.WindowsButton or DockItemKind.RecycleBin ||
            item.IsRuntime ||
            string.IsNullOrWhiteSpace(item.TargetPath))
        {
            return false;
        }

        return File.Exists(item.TargetPath) || Directory.Exists(item.TargetPath);
    }

    private void StartExternalShellDrag()
    {
        if (_draggedItemViewModel is null || string.IsNullOrWhiteSpace(_draggedItemId))
        {
            return;
        }

        var draggedItem = _draggedItemViewModel;
        var draggedItemId = _draggedItemId;
        var draggedButton = _draggedButton;
        var sourcePath = draggedItem.Item.TargetPath;
        var startIndex = _dragStartIndex;

        _isCompletingReorderDrag = true;
        TraceDrag($"external-shell-start item={draggedItemId}");

        try
        {
            _reorderDragTimer.Stop();
            HideDragPreview();
            EndHeldItemAnimation(draggedItem, draggedButton);

            if (Mouse.Captured == this || Mouse.Captured == DockRoot)
            {
                Mouse.Capture(null);
            }

            EndReorderDrag();
            _draggedButton = null;
            _draggedItemViewModel = null;
            _pendingDragItem = null;

            var result = DragDrop.DoDragDrop(this, CreateFileMoveDataObject(sourcePath), System.Windows.DragDropEffects.Move);
            TraceDrag($"external-shell-result result={result}");

            if (result == System.Windows.DragDropEffects.Move)
            {
                DeleteSourceAfterExternalMoveIfStillPresent(sourcePath);
                if (_viewModel.RemoveItem(draggedItemId))
                {
                    _store.Save();
                    PositionDock();
                }

                _suppressClickUntilUtc = DateTime.UtcNow.AddMilliseconds(250);
                return;
            }

            if (startIndex >= 0)
            {
                _viewModel.MoveItemToAbsoluteIndex(draggedItemId, startIndex);
            }
        }
        catch (Exception ex)
        {
            RuntimeLog.Write(ex, "StartExternalShellDrag");
            if (startIndex >= 0)
            {
                _viewModel.MoveItemToAbsoluteIndex(draggedItemId, startIndex);
            }

            System.Windows.MessageBox.Show(this, ex.Message, UserPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _isCompletingReorderDrag = false;
        }
    }

    private static DataObject CreateFileMoveDataObject(string path)
    {
        var data = new DataObject();
        data.SetData(System.Windows.DataFormats.FileDrop, new[] { path });
        data.SetData("Preferred DropEffect", new MemoryStream(BitConverter.GetBytes((int)System.Windows.DragDropEffects.Move)));
        return data;
    }

    private static void DeleteSourceAfterExternalMoveIfStillPresent(string sourcePath)
    {
        if (File.Exists(sourcePath))
        {
            File.Delete(sourcePath);
            return;
        }

        if (Directory.Exists(sourcePath))
        {
            Directory.Delete(sourcePath, recursive: true);
        }
    }

    private void MainWindow_ReorderMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isReorderDragActive)
        {
            _pendingDragItem = null;
            return;
        }

        CompleteInternalReorderDrag(commit: true);
        e.Handled = true;
    }

    private void MainWindow_ReorderLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_isCompletingReorderDrag || !_isReorderDragActive)
        {
            return;
        }

        if (Mouse.Captured == this || Mouse.Captured == DockRoot)
        {
            TraceDrag($"lost-from-previous-capture ignored captured={Mouse.Captured.GetType().Name}");
            return;
        }

        TraceDrag($"lost-capture cancel captured={Mouse.Captured?.GetType().Name ?? "<none>"}");
        CompleteInternalReorderDrag(commit: false);
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || !_isReorderDragActive)
        {
            return;
        }

        CompleteInternalReorderDrag(commit: false);
        e.Handled = true;
    }

    private void CompleteInternalReorderDrag(bool commit)
    {
        if (!_isReorderDragActive)
        {
            return;
        }

        var draggedItemId = _draggedItemId;
        var draggedItem = _draggedItemViewModel;
        var draggedButton = _draggedButton;
        var exportedToDesktop = false;
        _isCompletingReorderDrag = true;
        TraceDrag($"complete commit={commit} item={draggedItemId ?? "<none>"}");

        try
        {
            if (!string.IsNullOrWhiteSpace(draggedItemId))
            {
                if (commit && draggedItem is not null && IsCursorOutsideDockShell())
                {
                    exportedToDesktop = TryExportDraggedItemToDesktop(draggedItem);
                    if (!exportedToDesktop)
                    {
                        RestoreDraggedItemToStart(draggedItemId);
                    }
                }
                else if (commit)
                {
                    FinalizeReorderDrag(draggedItemId);
                    _viewModel.PersistVisualOrder();
                    _store.Save();
                    _suppressClickUntilUtc = DateTime.UtcNow.AddMilliseconds(250);
                }
                else
                {
                    RestoreDraggedItemToStart(draggedItemId);
                }
            }

            HideDragPreview();

            if (draggedItem is not null)
            {
                EndHeldItemAnimation(draggedItem, exportedToDesktop ? null : draggedButton);
            }

            if (commit && !exportedToDesktop && !string.IsNullOrWhiteSpace(draggedItemId))
            {
                AnimateDropSettle(draggedItemId);
            }
        }
        finally
        {
            EndReorderDrag();
            _reorderDragTimer.Stop();
            _draggedButton = null;
            _draggedItemViewModel = null;
            _pendingDragItem = null;

            if (Mouse.Captured == this || Mouse.Captured == DockRoot)
            {
                Mouse.Capture(null);
            }

            if (_bar.AutoHide && !DockShell.IsMouseOver)
            {
                _isPointerInside = false;
                _autoHideTimer.Stop();
                _autoHideTimer.Start();
            }

            _isCompletingReorderDrag = false;
        }
    }

    private bool IsCursorOutsideDockShell()
    {
        if (!GetCursorPos(out var cursorPosition))
        {
            return false;
        }

        return IsScreenPointOutsideDockShell(new Point(cursorPosition.X, cursorPosition.Y), tolerance: 8);
    }

    private bool IsScreenPointOutsideDockShell(Point screenPoint, double tolerance)
    {
        var point = DockShell.PointFromScreen(screenPoint);
        return point.X < -tolerance ||
               point.Y < -tolerance ||
               point.X > DockShell.ActualWidth + tolerance ||
               point.Y > DockShell.ActualHeight + tolerance;
    }

    private bool TryExportDraggedItemToDesktop(DockItemViewModel itemViewModel)
    {
        if (itemViewModel.IsWindowsButton || itemViewModel.Item.IsRuntime)
        {
            return false;
        }

        try
        {
            if (!itemViewModel.IsRecycleBin)
            {
                _exporter.MoveToDesktop(itemViewModel.Item);
            }

            if (!_viewModel.RemoveItem(itemViewModel.Item.Id))
            {
                return false;
            }

            _store.Save();
            _suppressClickUntilUtc = DateTime.UtcNow.AddMilliseconds(250);
            PositionDock();
            return true;
        }
        catch (Exception ex)
        {
            RuntimeLog.Write(ex, "TryExportDraggedItemToDesktop");
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                UserPaths.AppName,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }
    }

    private void DockItem_GiveFeedback(object sender, GiveFeedbackEventArgs e)
    {
        if (_dragPreviewPopup?.IsOpen != true)
        {
            return;
        }

        if (GetCursorPos(out var cursorPosition))
        {
            UpdateDragPreview(PointFromScreen(new Point(cursorPosition.X, cursorPosition.Y)));
        }

        e.UseDefaultCursors = false;
        Mouse.SetCursor(Cursors.Hand);
        e.Handled = true;
    }

    private void DockItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DockItemViewModel itemViewModel } &&
            (itemViewModel.IsWindowsButton || itemViewModel.IsRecycleBin || itemViewModel.IsAnimatedGif || itemViewModel.IsDropPlaceholder))
        {
            e.Handled = true;
        }
    }

    private void DockItem_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DockItemViewModel { IsWindowsButton: true } })
        {
            OpenNativeWindowsContextMenu();
            e.Handled = true;
            return;
        }

        if (sender is FrameworkElement { DataContext: DockItemViewModel { IsRecycleBin: true } })
        {
            OpenRecycleBinContextMenu(e.GetPosition(this));
            e.Handled = true;
            return;
        }

        if (sender is FrameworkElement { DataContext: DockItemViewModel itemViewModel } &&
            (itemViewModel.IsAnimatedGif || itemViewModel.IsDropPlaceholder))
        {
            e.Handled = true;
        }
    }

    private void DockItem_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DockItemViewModel { IsWindowsButton: true } })
        {
            e.Handled = true;
            OpenNativeWindowsContextMenu();
            return;
        }

        if (sender is FrameworkElement { DataContext: DockItemViewModel { IsRecycleBin: true } })
        {
            e.Handled = true;
            OpenRecycleBinContextMenu(Mouse.GetPosition(this));
            return;
        }

        if (sender is FrameworkElement { DataContext: DockItemViewModel itemViewModel } &&
            (itemViewModel.IsAnimatedGif || itemViewModel.IsDropPlaceholder))
        {
            e.Handled = true;
        }
    }

    private void OpenNativeWindowsContextMenu()
    {
        if (_isOpeningNativeWindowsMenu)
        {
            return;
        }

        _isOpeningNativeWindowsMenu = true;
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                WindowsButtonService.OpenPowerUserMenu();
            }
            catch (Exception ex)
            {
                RuntimeLog.Write(ex, "OpenNativeWindowsContextMenu");
            }
            finally
            {
                _isOpeningNativeWindowsMenu = false;
            }
        }, DispatcherPriority.ContextIdle);
    }

    private void OpenRecycleBinContextMenu(Point windowPoint)
    {
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                RecycleBinService.ShowContextMenu(this, PointToScreen(windowPoint));
            }
            catch (Exception ex)
            {
                RuntimeLog.Write(ex, "OpenRecycleBinContextMenu");
            }
        }, DispatcherPriority.ContextIdle);
    }

    private void DockItem_MouseEnter(object sender, MouseEventArgs e)
    {
        if (!_bar.ZoomEnabled || sender is not Button button)
        {
            return;
        }

        try
        {
            StartHoverZoom(e.GetPosition(DockItemsControl));
        }
        catch (Exception ex)
        {
            RuntimeLog.Write(ex, "DockItem_MouseEnter");
        }
    }

    private void DockItem_MouseLeave(object sender, MouseEventArgs e)
    {
        if (DockShell.IsMouseOver)
        {
            return;
        }

        try
        {
            ResetHoverZoom();
        }
        catch (Exception ex)
        {
            RuntimeLog.Write(ex, "DockItem_MouseLeave");
        }
    }

    private void DockShell_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_bar.ZoomEnabled || _isReorderDragActive || e.LeftButton == MouseButtonState.Pressed)
        {
            return;
        }

        try
        {
            StartHoverZoom(e.GetPosition(DockItemsControl));
        }
        catch (Exception ex)
        {
            RuntimeLog.Write(ex, "DockShell_MouseMove");
        }
    }

    private void DockShell_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (!_bar.LockItems && CanImport(e.Data))
        {
            UpdateExternalDropPlaceholderFromPointer(e.GetPosition(DockItemsControl));
        }

        e.Effects = GetDockDropEffect(e.Data);
        e.Handled = true;
    }

    private void DockShell_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        UpdateDragPreview(e.GetPosition(this));
        if (e.Data.GetDataPresent(ItemDragFormat))
        {
            UpdateReorderPlaceholderFromPointer(e.GetPosition(DockItemsControl));
        }
        else if (CanImport(e.Data))
        {
            UpdateExternalDropPlaceholderFromPointer(e.GetPosition(DockItemsControl));
        }

        e.Effects = GetDockDropEffect(e.Data);
        e.Handled = true;
    }

    private void DockShell_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        if (!DockShell.IsMouseOver)
        {
            RemoveExternalDropPlaceholder();
        }
    }

    private void DockShell_Drop(object sender, System.Windows.DragEventArgs e)
    {
        try
        {
            if (_bar.LockItems)
            {
                return;
            }

            if (TryFinalizeReorderDrag(e.Data, e.GetPosition(DockItemsControl)))
            {
                e.Effects = System.Windows.DragDropEffects.Move;
                e.Handled = true;
                return;
            }

            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) &&
                e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] paths)
            {
                var insertionIndex = GetExternalDropInsertionIndex();
                RemoveExternalDropPlaceholder();
                foreach (var path in paths)
                {
                    var item = _importer.ImportFileSystemPath(_bar, path);
                    _viewModel.InsertItem(insertionIndex++, item);
                }

                _store.Save();
                PositionDock();
                e.Effects = GetDockDropEffect(e.Data);
                e.Handled = true;
                return;
            }

            if (TryGetUri(e.Data, out var uri))
            {
                var insertionIndex = GetExternalDropInsertionIndex();
                RemoveExternalDropPlaceholder();
                var item = _importer.ImportUrl(_bar, uri);
                _viewModel.InsertItem(insertionIndex, item);
                _store.Save();
                PositionDock();
                e.Effects = System.Windows.DragDropEffects.Link;
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            RemoveExternalDropPlaceholder();
            System.Windows.MessageBox.Show(this, ex.Message, UserPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DockItem_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DockItemViewModel { IsRecycleBin: true } } &&
            e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Move;
            e.Handled = true;
            return;
        }

        if (!_bar.LockItems && CanImport(e.Data))
        {
            UpdateExternalDropPlaceholderFromPointer(e.GetPosition(DockItemsControl));
            e.Effects = GetDockDropEffect(e.Data);
            e.Handled = true;
            return;
        }

        if (_bar.LockItems ||
            sender is not FrameworkElement ||
            !TryGetDraggedItemId(e.Data, out var draggedItemId))
        {
            e.Effects = System.Windows.DragDropEffects.None;
            return;
        }

        UpdateDragPreview(e.GetPosition(this));
        UpdateReorderPlaceholderFromPointer(e.GetPosition(DockItemsControl));

        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;
    }

    private void DockItem_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: DockItemViewModel { IsRecycleBin: true } } &&
            e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) &&
            e.Data.GetData(System.Windows.DataFormats.FileDrop) is string[] recyclePaths)
        {
            try
            {
                RecycleBinService.MovePathsToRecycleBin(recyclePaths);
                e.Effects = System.Windows.DragDropEffects.Move;
            }
            catch (Exception ex)
            {
                RuntimeLog.Write(ex, "DockItem_Drop_RecycleBin");
                System.Windows.MessageBox.Show(this, ex.Message, UserPaths.AppName, MessageBoxButton.OK, MessageBoxImage.Warning);
                e.Effects = System.Windows.DragDropEffects.None;
            }

            e.Handled = true;
            return;
        }

        if (_bar.LockItems ||
            sender is not FrameworkElement ||
            !TryGetDraggedItemId(e.Data, out var draggedItemId))
        {
            return;
        }

        UpdateReorderPlaceholderFromPointer(e.GetPosition(DockItemsControl));
        FinalizeReorderDrag(draggedItemId);
        e.Effects = System.Windows.DragDropEffects.Move;
        e.Handled = true;
    }

    private bool TryFinalizeReorderDrag(System.Windows.IDataObject data, Point pointer)
    {
        if (!TryGetDraggedItemId(data, out var draggedItemId))
        {
            return false;
        }

        if (_isReorderDragActive)
        {
            UpdateReorderPlaceholderFromPointer(pointer);
            FinalizeReorderDrag(draggedItemId);
        }
        else
        {
            _viewModel.MoveItemToEnd(draggedItemId);
        }

        return true;
    }

    private bool TryGetDraggedItemId(System.Windows.IDataObject data, out string draggedItemId)
    {
        draggedItemId = "";
        if (!data.GetDataPresent(ItemDragFormat) ||
            data.GetData(ItemDragFormat) is not string value ||
            string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        draggedItemId = value;
        return true;
    }

    private void BeginReorderDrag(string draggedItemId)
    {
        _isReorderDragActive = true;
        _draggedItemId = draggedItemId;
        _dragStartIndex = _viewModel.Items.ToList().FindIndex(item => item.Item.Id == draggedItemId);
    }

    private void EndReorderDrag()
    {
        _isReorderDragActive = false;
        _draggedItemId = null;
        _dragStartIndex = -1;
    }

    private void UpdateReorderPlaceholderFromPointer(Point pointer)
    {
        if (!_isReorderDragActive || string.IsNullOrWhiteSpace(_draggedItemId))
        {
            return;
        }

        var insertionIndex = GetVisualInsertionIndex(pointer);
        TraceDrag($"move-placeholder visualIndex={insertionIndex} pointer={pointer.X:0},{pointer.Y:0}");
        MoveDraggedPlaceholderToVisualIndex(insertionIndex);
    }

    private void UpdateExternalDropPlaceholderFromPointer(Point pointer)
    {
        var insertionIndex = GetExternalDropVisualInsertionIndex(pointer);
        AnimateReorderChange(
            () => _viewModel.SetDropPlaceholderVisualIndex(insertionIndex),
            DockBarViewModel.DropPlaceholderId);
    }

    private int GetVisualInsertionIndex(Point pointer)
    {
        var axisPosition = IsVerticalDock ? pointer.Y : pointer.X;
        var insertionIndex = 0;

        foreach (var item in GetVisibleReorderItems())
        {
            var presenter = FindDockItemPresenterById(DockItemsControl, item.Item.Id);
            if (presenter is null || presenter.ActualWidth <= 0 || presenter.ActualHeight <= 0)
            {
                continue;
            }

            var layoutPosition = GetPresenterLayoutPosition(presenter, DockItemsControl);
            var center = new Point(
                layoutPosition.X + presenter.ActualWidth / 2,
                layoutPosition.Y + presenter.ActualHeight / 2);
            var itemAxisCenter = IsVerticalDock ? center.Y : center.X;
            if (axisPosition > itemAxisCenter)
            {
                insertionIndex++;
            }
        }

        return insertionIndex;
    }

    private int GetExternalDropVisualInsertionIndex(Point pointer)
    {
        var axisPosition = IsVerticalDock ? pointer.Y : pointer.X;
        var insertionIndex = 0;

        foreach (var item in _viewModel.Items.Where(static item => !item.IsDropPlaceholder))
        {
            var presenter = FindDockItemPresenterById(DockItemsControl, item.Item.Id);
            if (presenter is null || presenter.ActualWidth <= 0 || presenter.ActualHeight <= 0)
            {
                continue;
            }

            var layoutPosition = GetPresenterLayoutPosition(presenter, DockItemsControl);
            var center = new Point(
                layoutPosition.X + presenter.ActualWidth / 2,
                layoutPosition.Y + presenter.ActualHeight / 2);
            var itemAxisCenter = IsVerticalDock ? center.Y : center.X;
            if (axisPosition > itemAxisCenter)
            {
                insertionIndex++;
            }
        }

        return insertionIndex;
    }

    private int GetExternalDropInsertionIndex()
    {
        var index = _viewModel.DropPlaceholderIndex;
        return index < 0 ? _viewModel.Items.Count : index;
    }

    private void RemoveExternalDropPlaceholder()
    {
        AnimateReorderChange(
            () => _viewModel.RemoveDropPlaceholder() >= 0,
            DockBarViewModel.DropPlaceholderId);
    }

    private bool MoveDraggedPlaceholderToVisualIndex(int visualInsertionIndex)
    {
        if (!_isReorderDragActive || string.IsNullOrWhiteSpace(_draggedItemId))
        {
            return false;
        }

        var insertionIndex = ConvertVisualInsertionIndexToFullIndex(visualInsertionIndex);
        TraceDrag($"move-request visualIndex={visualInsertionIndex} fullIndex={insertionIndex}");
        return AnimateReorderChange(
            () => _viewModel.MoveItemToIndex(_draggedItemId, insertionIndex),
            _draggedItemId);
    }

    private bool FinalizeReorderDrag(string draggedItemId)
    {
        _viewModel.PersistVisualOrder();
        return true;
    }

    private int ConvertVisualInsertionIndexToFullIndex(int visualInsertionIndex)
    {
        var sourceIndex = _viewModel.Items.ToList().FindIndex(item => item.Item.Id == _draggedItemId);
        if (sourceIndex < 0)
        {
            return visualInsertionIndex;
        }

        return visualInsertionIndex <= sourceIndex
            ? visualInsertionIndex
            : visualInsertionIndex + 1;
    }

    private bool AnimateReorderChange(Func<bool> moveOperation, string draggedItemId)
    {
        var previousPositions = CaptureVisualPositions(draggedItemId);
        var moved = moveOperation();
        TraceDrag($"move-result moved={moved}");
        if (!moved)
        {
            return false;
        }

        DockItemsControl.UpdateLayout();
        AnimateDockItemsFrom(previousPositions, draggedItemId);
        return true;
    }

    private Dictionary<string, Point> CaptureVisualPositions(string excludedItemId)
    {
        DockItemsControl.UpdateLayout();
        var positions = new Dictionary<string, Point>(StringComparer.Ordinal);

        foreach (var presenter in FindDockItemPresenters(DockItemsControl))
        {
            if (presenter.Content is not DockItemViewModel itemViewModel ||
                itemViewModel.Item.Id == excludedItemId)
            {
                continue;
            }

            positions[itemViewModel.Item.Id] = GetVisualPosition(presenter, DockRoot);
        }

        return positions;
    }

    private void AnimateDockItemsFrom(Dictionary<string, Point> previousPositions, string excludedItemId)
    {
        var duration = TimeSpan.FromMilliseconds(190);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        foreach (var presenter in FindDockItemPresenters(DockItemsControl))
        {
            if (presenter.Content is not DockItemViewModel itemViewModel ||
                itemViewModel.Item.Id == excludedItemId ||
                !previousPositions.TryGetValue(itemViewModel.Item.Id, out var previous))
            {
                continue;
            }

            var layout = GetPresenterLayoutPosition(presenter, DockRoot);
            var deltaX = previous.X - layout.X;
            var deltaY = previous.Y - layout.Y;
            if (Math.Abs(deltaX) < 0.5 && Math.Abs(deltaY) < 0.5)
            {
                continue;
            }

            var translate = EnsurePresenterTranslate(presenter);
            translate.BeginAnimation(TranslateTransform.XProperty, null);
            translate.BeginAnimation(TranslateTransform.YProperty, null);
            translate.X = deltaX;
            translate.Y = deltaY;
            translate.BeginAnimation(
                TranslateTransform.XProperty,
                new DoubleAnimation(0, duration) { EasingFunction = easing });
            translate.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(0, duration) { EasingFunction = easing });
        }
    }

    private void RestoreDraggedItemToStart(string draggedItemId)
    {
        if (_dragStartIndex < 0)
        {
            return;
        }

        AnimateReorderChange(
            () => _viewModel.MoveItemToAbsoluteIndex(draggedItemId, _dragStartIndex),
            draggedItemId);
    }

    private IEnumerable<DockItemViewModel> GetVisibleReorderItems()
    {
        return _viewModel.Items.Where(item => item.Item.Id != _draggedItemId);
    }

    private bool IsVerticalDock => _bar.Edge is DockEdge.Left or DockEdge.Right;

    private void ShowDragPreview(DockItemViewModel itemViewModel, Point position)
    {
        _dragPreviewShell = CreateDragPreview(itemViewModel);
        _dragPreviewPopup ??= new Popup
        {
            AllowsTransparency = true,
            IsHitTestVisible = false,
            Placement = PlacementMode.Relative,
            PlacementTarget = DockRoot,
            PopupAnimation = PopupAnimation.None,
            StaysOpen = true
        };

        _dragPreviewPopup.Child = _dragPreviewShell;
        _dragPreviewPopup.IsOpen = true;
        UpdateDragPreview(position);
        AnimateDragPreviewLift(_dragPreviewShell);
    }

    private Border CreateDragPreview(DockItemViewModel itemViewModel)
    {
        var previewSize = _viewModel.ItemButtonSize;
        var shell = new Border
        {
            Width = previewSize,
            Height = previewSize,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderBrush = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            Opacity = 0.98,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(0.82, 0.82),
            CacheMode = new BitmapCache
            {
                EnableClearType = true,
                RenderAtScale = 1.2,
                SnapsToDevicePixels = true
            }
        };

        var grid = new Grid
        {
            ClipToBounds = false,
            IsHitTestVisible = false
        };

        grid.Children.Add(new Border
        {
            Width = _viewModel.IconTileSize,
            Height = _viewModel.IconTileSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Background = _viewModel.TileBackground,
            BorderBrush = _viewModel.TileBorderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10)
        });

        grid.Children.Add(new Image
        {
            Width = _viewModel.IconImageSize,
            Height = _viewModel.IconImageSize,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Source = itemViewModel.Icon,
            Opacity = 1.0,
            IsHitTestVisible = false
        });

        grid.Children.Add(new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(4, 0, 4, 3),
            FontFamily = new FontFamily(_bar.FontFamily),
            FontSize = _viewModel.LabelFontSize,
            Foreground = _viewModel.LabelBrush,
            MaxWidth = _viewModel.LabelMaxWidth,
            Text = itemViewModel.ShortLabel,
            TextAlignment = TextAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Visibility = _viewModel.LabelVisibility,
            IsHitTestVisible = false
        });

        shell.Child = grid;
        return shell;
    }

    private void UpdateDragPreview(Point position)
    {
        if (_dragPreviewPopup?.IsOpen != true || _dragPreviewShell is null)
        {
            return;
        }

        _dragPreviewPopup.HorizontalOffset = position.X - _dragPreviewShell.Width / 2;
        _dragPreviewPopup.VerticalOffset = position.Y - _dragPreviewShell.Height / 2;
    }

    private static void AnimateDragPreviewLift(Border preview)
    {
        if (preview.RenderTransform is not ScaleTransform scale)
        {
            return;
        }

        var duration = TimeSpan.FromMilliseconds(120);
        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.22 };
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1.08, duration) { EasingFunction = ease });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1.08, duration) { EasingFunction = ease });
        preview.BeginAnimation(OpacityProperty, new DoubleAnimation(0.94, duration));
    }

    private void HideDragPreview()
    {
        if (_dragPreviewPopup is null)
        {
            return;
        }

        _dragPreviewPopup.IsOpen = false;
        _dragPreviewPopup.Child = null;
        _dragPreviewShell = null;
    }

    private void BeginHeldItemAnimation(DockItemViewModel itemViewModel, Button? button)
    {
        itemViewModel.IsBeingDragged = true;

        if (button is null)
        {
            return;
        }

        button.BeginAnimation(OpacityProperty, null);
        AnimateScale(button, 1.0);
    }

    private void EndHeldItemAnimation(DockItemViewModel itemViewModel, Button? button)
    {
        itemViewModel.IsBeingDragged = false;

        if (button is null)
        {
            return;
        }

        button.Opacity = 1.0;
        AnimateScale(button, 1.0);
    }

    private void AnimateDropSettle(string draggedItemId)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var button = FindDockItemButtonById(DockItemsControl, draggedItemId);
            if (button is null)
            {
                return;
            }

            var (scale, _) = EnsureItemTransforms(button);
            var xAnimation = CreateDropSettleAnimation();
            var yAnimation = CreateDropSettleAnimation();
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, xAnimation);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, yAnimation);
        }, DispatcherPriority.Loaded);
    }

    private static DoubleAnimationUsingKeyFrames CreateDropSettleAnimation()
    {
        return new DoubleAnimationUsingKeyFrames
        {
            KeyFrames =
            {
                new EasingDoubleKeyFrame(1.16, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(80)), new CubicEase { EasingMode = EasingMode.EaseOut }),
                new EasingDoubleKeyFrame(0.98, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(145)), new CubicEase { EasingMode = EasingMode.EaseInOut }),
                new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(215)), new CubicEase { EasingMode = EasingMode.EaseOut })
            }
        };
    }

    private static bool CanImport(System.Windows.IDataObject data)
    {
        return data.GetDataPresent(System.Windows.DataFormats.FileDrop) || TryGetUri(data, out _);
    }

    private System.Windows.DragDropEffects GetDockDropEffect(System.Windows.IDataObject data)
    {
        if (_bar.LockItems)
        {
            return System.Windows.DragDropEffects.None;
        }

        if (data.GetDataPresent(ItemDragFormat))
        {
            return System.Windows.DragDropEffects.Move;
        }

        if (data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return _bar.ImportMode == DockImportMode.CreateShortcutInBarFolder
                ? System.Windows.DragDropEffects.Link
                : System.Windows.DragDropEffects.Move;
        }

        return TryGetUri(data, out _)
            ? System.Windows.DragDropEffects.Link
            : System.Windows.DragDropEffects.None;
    }

    private static bool TryGetUri(System.Windows.IDataObject data, out Uri uri)
    {
        uri = null!;

        if (!data.GetDataPresent(System.Windows.DataFormats.UnicodeText))
        {
            return false;
        }

        var text = data.GetData(System.Windows.DataFormats.UnicodeText) as string;
        if (!Uri.TryCreate(text?.Trim(), UriKind.Absolute, out var parsedUri))
        {
            return false;
        }

        uri = parsedUri;
        return
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private System.Windows.Controls.ContextMenu BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        menu.Items.Add(CreateMenuItem("Configuracoes da barra", (_, _) => OpenSettings()));
        menu.Items.Add(new System.Windows.Controls.Separator());

        menu.Items.Add(CreateMenuItem("Abrir pasta da barra", (_, _) =>
        {
            DockLauncher.Open(new DockItem { TargetPath = UserPaths.EnsureBarFolder(_bar.Name) });
        }));

        menu.Items.Add(new System.Windows.Controls.Separator());

        var createMenu = new System.Windows.Controls.MenuItem { Header = "Criar nova barra" };
        createMenu.Items.Add(CreateMenuItem("Esquerda", (_, _) => CurrentApp.CreateBar(DockEdge.Left)));
        createMenu.Items.Add(CreateMenuItem("Direita", (_, _) => CurrentApp.CreateBar(DockEdge.Right)));
        createMenu.Items.Add(CreateMenuItem("Topo", (_, _) => CurrentApp.CreateBar(DockEdge.Top)));
        createMenu.Items.Add(CreateMenuItem("Rodape", (_, _) => CurrentApp.CreateBar(DockEdge.Bottom)));
        menu.Items.Add(createMenu);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var positionMenu = new System.Windows.Controls.MenuItem { Header = "Mover esta barra" };
        positionMenu.Items.Add(CreateMenuItem("Esquerda", (_, _) => ChangeEdge(DockEdge.Left)));
        positionMenu.Items.Add(CreateMenuItem("Direita", (_, _) => ChangeEdge(DockEdge.Right)));
        positionMenu.Items.Add(CreateMenuItem("Topo", (_, _) => ChangeEdge(DockEdge.Top)));
        positionMenu.Items.Add(CreateMenuItem("Rodape", (_, _) => ChangeEdge(DockEdge.Bottom)));
        menu.Items.Add(positionMenu);

        menu.Items.Add(new System.Windows.Controls.Separator());

        menu.Items.Add(CreateMenuItem("Remover esta barra", (_, _) => CurrentApp.RemoveBar(this, _bar)));
        menu.Items.Add(CreateMenuItem("Sair", (_, _) => CurrentApp.ExitAll()));

        return menu;
    }

    private void ChangeEdge(DockEdge edge)
    {
        _bar.Edge = edge;
        _store.Save();
        ApplyBarSettings();
    }

    private static System.Windows.Controls.MenuItem CreateMenuItem(string header, RoutedEventHandler click)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header };
        item.Click += click;
        return item;
    }

    private void PositionDock()
    {
        var screens = Forms.Screen.AllScreens;
        var screen = screens[Math.Clamp(_bar.MonitorIndex, 0, screens.Length - 1)];
        var area = screen.WorkingArea;

        var placement = DockGeometry.Calculate(new DockGeometryInput(
            _bar.Edge,
            area.Left,
            area.Top,
            area.Width,
            area.Height,
            _viewModel.Items.Count,
            _viewModel.ItemButtonSize,
            _bar.IconSpacing,
            _viewModel.ZoomOverhang,
            _bar.Offset,
            _bar.CenterOffset,
            _bar.BarWidth,
            _bar.BarHeight));

        Width = placement.WindowWidth;
        Height = placement.WindowHeight;
        _visibleLeft = placement.WindowLeft;
        _visibleTop = placement.WindowTop;

        if (_bar.AutoHide && _isDockHidden)
        {
            MoveToHiddenPosition(animated: false);
        }
        else
        {
            MoveToVisiblePosition(animated: false);
        }

        ApplyLayering();
    }

    private void OpenSettings()
    {
        if (_settingsWindow is { IsVisible: true })
        {
            _settingsWindow.Activate();
            return;
        }

        var settingsWindow = new SettingsWindow(_store, _bar)
        {
            Owner = this,
            ShowInTaskbar = false
        };
        _settingsWindow = settingsWindow;
        settingsWindow.SettingsApplied += SettingsWindow_SettingsApplied;
        settingsWindow.Closed += (_, _) =>
        {
            settingsWindow.SettingsApplied -= SettingsWindow_SettingsApplied;
            if (ReferenceEquals(_settingsWindow, settingsWindow))
            {
                _settingsWindow = null;
            }
        };

        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private void SettingsWindow_SettingsApplied(object? sender, EventArgs e)
    {
        ApplyBarSettings();
        CurrentApp.RefreshGlobalServices();
    }

    internal void AddRuntimeWindow(DockItem item)
    {
        _viewModel.AddRuntimeItem(item);
        PositionDock();
    }

    internal void RemoveRuntimeWindow(long nativeWindowHandle)
    {
        if (_viewModel.RemoveRuntimeWindow(nativeWindowHandle))
        {
            PositionDock();
        }
    }

    internal void ClearRuntimeWindows()
    {
        foreach (var runtimeWindow in _viewModel.Items
                     .Where(static item => item.Item.IsRuntime)
                     .Select(static item => item.Item.NativeWindowHandle)
                     .ToArray())
        {
            _viewModel.RemoveRuntimeWindow(runtimeWindow);
        }

        PositionDock();
    }

    private void ApplyBarSettings()
    {
        ResetHoverZoom(immediate: true);
        Title = $"{UserPaths.AppName} - {_bar.Name}";
        Topmost = _bar.Layering == DockLayering.TopMost;
        DockShell.AllowDrop = !_bar.LockItems;
        ConfigureHideTimers();
        _viewModel.SyncPersistentItemsFromSettings();
        RenderOptions.SetBitmapScalingMode(DockItemsControl, _bar.IconQuality switch
        {
            IconQuality.Low => BitmapScalingMode.LowQuality,
            IconQuality.Medium => BitmapScalingMode.Linear,
            _ => BitmapScalingMode.HighQuality
        });
        _viewModel.RefreshSettings();
        DataContext = null;
        DataContext = _viewModel;
        DockItemsControl.Items.Refresh();
        if (!_bar.AutoHide)
        {
            ShowDock();
        }
        PositionDock();
    }

    private void ConfigureHideTimers()
    {
        _autoHideTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, _bar.AutoHideDelayMs));
        _popupTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, _store.Current.App.PopupOnMouseover ? _store.Current.App.PopupDelayMs : 0));
        if (!_bar.AutoHide)
        {
            _autoHideTimer.Stop();
            _popupTimer.Stop();
        }
    }

    private void HandleDockMouseEnter()
    {
        _isPointerInside = true;
        _autoHideTimer.Stop();

        if (!_bar.AutoHide || !_isDockHidden)
        {
            return;
        }

        if (_store.Current.App.PopupOnMouseover)
        {
            _popupTimer.Start();
        }
        else
        {
            ShowDock();
        }
    }

    private void HandleDockMouseLeave()
    {
        if (_isReorderDragActive)
        {
            return;
        }

        ResetHoverZoom();
        _isPointerInside = false;
        _popupTimer.Stop();

        if (_bar.AutoHide)
        {
            _autoHideTimer.Start();
        }
    }

    private void ShowDock()
    {
        _isDockHidden = false;
        MoveToVisiblePosition(animated: _bar.AutoHide);
    }

    private void HideDock()
    {
        _isDockHidden = true;
        MoveToHiddenPosition(animated: true);
    }

    private void MoveToVisiblePosition(bool animated)
    {
        MoveWindow(_visibleLeft, _visibleTop, animated);
    }

    private void MoveToHiddenPosition(bool animated)
    {
        var area = GetCurrentWorkingArea();
        const double visibleStrip = 6;
        var overhang = _viewModel.ZoomOverhang;

        var left = _visibleLeft;
        var top = _visibleTop;

        switch (_bar.Edge)
        {
            case DockEdge.Bottom:
                top = area.Bottom - visibleStrip - overhang;
                break;
            case DockEdge.Top:
                top = area.Top - Height + visibleStrip + overhang;
                break;
            case DockEdge.Left:
                left = area.Left - Width + visibleStrip + overhang;
                break;
            case DockEdge.Right:
                left = area.Right - visibleStrip - overhang;
                break;
        }

        MoveWindow(left, top, animated);
    }

    private void MoveWindow(double left, double top, bool animated)
    {
        if (!animated || _bar.AutoHideDurationMs <= 0)
        {
            BeginAnimation(LeftProperty, null);
            BeginAnimation(TopProperty, null);
            Left = left;
            Top = top;
            return;
        }

        var duration = TimeSpan.FromMilliseconds(_bar.AutoHideDurationMs);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

        BeginAnimation(LeftProperty, new DoubleAnimation(left, duration) { EasingFunction = ease });
        BeginAnimation(TopProperty, new DoubleAnimation(top, duration) { EasingFunction = ease });
    }

    private Forms.Screen GetCurrentScreen()
    {
        var screens = Forms.Screen.AllScreens;
        return screens[Math.Clamp(_bar.MonitorIndex, 0, screens.Length - 1)];
    }

    private System.Drawing.Rectangle GetCurrentWorkingArea()
    {
        return GetCurrentScreen().WorkingArea;
    }

    private void ApplyLayering()
    {
        if (!IsLoaded)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        if (_bar.Layering == DockLayering.Bottom)
        {
            SetWindowPos(handle, HwndBottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
        }
        else if (_bar.Layering == DockLayering.Normal)
        {
            SetWindowPos(handle, HwndNoTopMost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
        }
    }

    private void AnimateScale(Button button, double scale)
    {
        var (transform, _) = EnsureItemTransforms(button);
        if (Math.Abs(transform.ScaleX - scale) < 0.006 &&
            Math.Abs(transform.ScaleY - scale) < 0.006)
        {
            return;
        }

        var duration = TimeSpan.FromMilliseconds(Math.Clamp(_bar.ZoomDurationMs, 0, 260));
        if (duration <= TimeSpan.Zero)
        {
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            transform.ScaleX = scale;
            transform.ScaleY = scale;
            return;
        }

        IEasingFunction? easing = _bar.HoverEffect == HoverEffect.None
            ? null
            : new CubicEase { EasingMode = EasingMode.EaseOut };

        var xAnimation = new DoubleAnimation(scale, duration) { EasingFunction = easing };
        var yAnimation = new DoubleAnimation(scale, duration) { EasingFunction = easing };

        transform.BeginAnimation(ScaleTransform.ScaleXProperty, xAnimation, HandoffBehavior.SnapshotAndReplace);
        transform.BeginAnimation(ScaleTransform.ScaleYProperty, yAnimation, HandoffBehavior.SnapshotAndReplace);
    }

    private void StartHoverZoom(Point pointer)
    {
        if (!_isHoverZoomActive)
        {
            ClearHoverAnimationClocks();
        }

        _hoverZoomPointer = pointer;
        _isHoverZoomActive = true;
        _isHoverZoomSettling = true;
    }

    private void CompositionTarget_Rendering(object? sender, EventArgs e)
    {
        if (!_isHoverZoomActive && !_isHoverZoomSettling)
        {
            return;
        }

        var renderingTime = e is RenderingEventArgs renderingEventArgs
            ? renderingEventArgs.RenderingTime
            : TimeSpan.Zero;
        var delta = _lastHoverRenderingTime == TimeSpan.Zero || renderingTime <= _lastHoverRenderingTime
            ? TimeSpan.FromMilliseconds(16)
            : renderingTime - _lastHoverRenderingTime;
        _lastHoverRenderingTime = renderingTime;

        var smoothing = 1.0 - Math.Exp(-Math.Clamp(delta.TotalSeconds, 0.001, 0.05) * 32);
        var allSettled = UpdateHoverZoomFrame(_hoverZoomPointer, smoothing);
        if (!_isHoverZoomActive && allSettled)
        {
            _isHoverZoomSettling = false;
            _lastHoverRenderingTime = TimeSpan.Zero;
        }
    }

    private bool UpdateHoverZoomFrame(Point pointer, double smoothing)
    {
        var buttons = FindDockItemButtons(DockItemsControl)
            .Where(static button => button.DataContext is DockItemViewModel { IsDropPlaceholder: false })
            .ToList();
        var pointerAxis = IsVerticalDock ? pointer.Y : pointer.X;
        var slotStep = Math.Max(1, _viewModel.ItemButtonSize + Math.Max(0, _bar.IconSpacing));
        var allSettled = true;

        foreach (var button in buttons)
        {
            if (button.DataContext is not DockItemViewModel item)
            {
                continue;
            }

            var presenter = FindDockItemPresenterById(DockItemsControl, item.Item.Id);
            if (presenter is null || presenter.ActualWidth <= 0 || presenter.ActualHeight <= 0)
            {
                continue;
            }

            var layout = GetPresenterLayoutPosition(presenter, DockItemsControl);
            var centerAxis = IsVerticalDock
                ? layout.Y + (presenter.ActualHeight / 2)
                : layout.X + (presenter.ActualWidth / 2);
            var distance = _isHoverZoomActive
                ? Math.Abs(pointerAxis - centerAxis) / slotStep
                : double.PositiveInfinity;
            var targetScale = _isHoverZoomActive
                ? _viewModel.GetZoomScaleForDistance(distance)
                : 1.0;

            allSettled &= SetScaleTowards(button, targetScale, smoothing);

            if (_bar.ZoomOpaque && FindVisualChild<Image>(button) is { } image)
            {
                var targetOpacity = _isHoverZoomActive && targetScale > 1.001
                    ? 1.0
                    : _viewModel.ItemOpacity;
                allSettled &= SetOpacityTowards(image, targetOpacity, smoothing);
            }
        }

        return allSettled;
    }

    private void ResetHoverZoom(bool immediate = false)
    {
        _isHoverZoomActive = false;
        _isHoverZoomSettling = true;

        if (!immediate)
        {
            return;
        }

        ClearHoverAnimationClocks();
        foreach (var button in FindDockItemButtons(DockItemsControl))
        {
            var (scale, _) = EnsureItemTransforms(button);
            scale.ScaleX = 1.0;
            scale.ScaleY = 1.0;
            if (FindVisualChild<Image>(button) is { } image)
            {
                image.Opacity = _viewModel.ItemOpacity;
            }
        }

        _isHoverZoomSettling = false;
        _lastHoverRenderingTime = TimeSpan.Zero;
    }

    private void ClearHoverAnimationClocks()
    {
        foreach (var button in FindDockItemButtons(DockItemsControl))
        {
            var (scale, _) = EnsureItemTransforms(button);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            if (FindVisualChild<Image>(button) is { } image)
            {
                image.BeginAnimation(OpacityProperty, null);
            }
        }
    }

    private static bool SetScaleTowards(Button button, double targetScale, double smoothing)
    {
        var (scale, _) = EnsureItemTransforms(button);
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);

        var nextX = SmoothValue(scale.ScaleX, targetScale, smoothing);
        var nextY = SmoothValue(scale.ScaleY, targetScale, smoothing);
        scale.ScaleX = nextX;
        scale.ScaleY = nextY;
        return Math.Abs(nextX - targetScale) < 0.003 &&
               Math.Abs(nextY - targetScale) < 0.003;
    }

    private static bool SetOpacityTowards(UIElement element, double targetOpacity, double smoothing)
    {
        element.BeginAnimation(OpacityProperty, null);
        var next = SmoothValue(element.Opacity, targetOpacity, smoothing);
        element.Opacity = next;
        return Math.Abs(next - targetOpacity) < 0.004;
    }

    private static double SmoothValue(double current, double target, double smoothing)
    {
        return Math.Abs(current - target) < 0.003
            ? target
            : current + ((target - current) * smoothing);
    }

    private static (ScaleTransform Scale, TranslateTransform Translate) EnsureItemTransforms(Button button)
    {
        if (button.RenderTransform is TransformGroup existingGroup &&
            !existingGroup.IsFrozen)
        {
            var existingScale = existingGroup.Children.OfType<ScaleTransform>().FirstOrDefault();
            var existingTranslate = existingGroup.Children.OfType<TranslateTransform>().FirstOrDefault();
            if (existingScale is not null && existingTranslate is not null)
            {
                return (existingScale, existingTranslate);
            }
        }

        var scale = button.RenderTransform as ScaleTransform;
        var translate = button.RenderTransform as TranslateTransform;
        var group = new TransformGroup();
        group.Children.Add(scale?.CloneCurrentValue() ?? new ScaleTransform(1, 1));
        group.Children.Add(translate?.CloneCurrentValue() ?? new TranslateTransform(0, 0));
        button.RenderTransform = group;
        return ((ScaleTransform)group.Children[0], (TranslateTransform)group.Children[1]);
    }

    private static TranslateTransform EnsurePresenterTranslate(ContentPresenter presenter)
    {
        if (presenter.RenderTransform is TranslateTransform existingTranslate &&
            !existingTranslate.IsFrozen)
        {
            return existingTranslate;
        }

        var translate = new TranslateTransform();
        presenter.RenderTransform = translate;
        return translate;
    }

    private static Point GetVisualPosition(FrameworkElement element, Visual ancestor)
    {
        return element.TransformToAncestor(ancestor).Transform(new Point(0, 0));
    }

    private static Point GetLayoutPosition(Button button, Visual ancestor)
    {
        var visualPosition = GetVisualPosition(button, ancestor);
        var (_, translate) = EnsureItemTransforms(button);
        return new Point(visualPosition.X - translate.X, visualPosition.Y - translate.Y);
    }

    private static Point GetPresenterLayoutPosition(ContentPresenter presenter, Visual ancestor)
    {
        var visualPosition = GetVisualPosition(presenter, ancestor);
        var translate = EnsurePresenterTranslate(presenter);
        return new Point(visualPosition.X - translate.X, visualPosition.Y - translate.Y);
    }

    private void AnimateOpacity(UIElement element, double opacity)
    {
        if (Math.Abs(element.Opacity - opacity) < 0.006)
        {
            return;
        }

        var duration = TimeSpan.FromMilliseconds(Math.Clamp(_bar.ZoomDurationMs, 0, 260));
        if (duration <= TimeSpan.Zero)
        {
            element.BeginAnimation(OpacityProperty, null);
            element.Opacity = opacity;
            return;
        }

        element.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(opacity, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } },
            HandoffBehavior.SnapshotAndReplace);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static Button? FindDockItemButtonById(DependencyObject parent, string itemId)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is Button { DataContext: DockItemViewModel itemViewModel } &&
                itemViewModel.Item.Id == itemId)
            {
                return (Button)child;
            }

            var descendant = FindDockItemButtonById(child, itemId);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static ContentPresenter? FindDockItemPresenterById(DependencyObject parent, string itemId)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is ContentPresenter { Content: DockItemViewModel itemViewModel } &&
                itemViewModel.Item.Id == itemId)
            {
                return (ContentPresenter)child;
            }

            var descendant = FindDockItemPresenterById(child, itemId);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static IEnumerable<Button> FindDockItemButtons(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is Button { DataContext: DockItemViewModel })
            {
                yield return (Button)child;
            }

            foreach (var descendant in FindDockItemButtons(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<ContentPresenter> FindDockItemPresenters(DependencyObject parent)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is ContentPresenter { Content: DockItemViewModel })
            {
                yield return (ContentPresenter)child;
            }

            foreach (var descendant in FindDockItemPresenters(child))
            {
                yield return descendant;
            }
        }
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        if (minimum > maximum)
        {
            return value;
        }

        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    private static void TraceDrag(string message)
    {
        RuntimeLog.WriteDiagnostic("drag", message);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        int flags);

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetCursorPos(out NativePoint lpPoint);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    private static bool IsLeftMouseButtonDown()
    {
        const int vkLeftButton = 0x01;
        return (GetAsyncKeyState(vkLeftButton) & unchecked((short)0x8000)) != 0;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private App CurrentApp => (App)System.Windows.Application.Current;
}
