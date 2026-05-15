using System;
using RockETDock.App.Models;

namespace RockETDock.App.Services;

public static class DockGeometry
{
    public static DockPlacement Calculate(DockGeometryInput input)
    {
        var itemCount = Math.Max(1, input.ItemCount);
        var itemStep = input.ItemButtonSize + Math.Max(0, input.IconSpacing);
        var thickness = input.ItemButtonSize + 28;
        var edgeDistance = Math.Max(0, 8 + input.EdgeDistance);
        var maxShellWidth = Math.Max(40, input.WorkingWidth - 32 - (input.HorizontalOverhang * 2));
        var maxShellHeight = Math.Max(40, input.WorkingHeight - 32 - (input.VerticalOverhang * 2));
        var requiredPrimarySize = itemCount * itemStep + 24;

        double shellWidth;
        double shellHeight;

        if (input.Edge is DockEdge.Left or DockEdge.Right)
        {
            shellWidth = input.BarWidth > 0
                ? ClampToAvailable(input.BarWidth, thickness, maxShellWidth)
                : Math.Min(maxShellWidth, thickness);
            shellHeight = input.BarHeight > 0
                ? ClampToAvailable(input.BarHeight, requiredPrimarySize, maxShellHeight)
                : Math.Min(maxShellHeight, Math.Max(thickness + 24, requiredPrimarySize));
        }
        else
        {
            shellWidth = input.BarWidth > 0
                ? ClampToAvailable(input.BarWidth, requiredPrimarySize, maxShellWidth)
                : Math.Min(maxShellWidth, Math.Max(thickness + 80, requiredPrimarySize));
            shellHeight = input.BarHeight > 0
                ? ClampToAvailable(input.BarHeight, thickness, maxShellHeight)
                : Math.Min(maxShellHeight, thickness);
        }

        var windowWidth = shellWidth + (input.HorizontalOverhang * 2);
        var windowHeight = shellHeight + (input.VerticalOverhang * 2);
        var centeredShellLeft = Clamp(
            input.WorkingLeft + (input.WorkingWidth - shellWidth) / 2 + input.CenterOffset,
            input.WorkingLeft + 8,
            input.WorkingLeft + input.WorkingWidth - shellWidth - 8);
        var centeredShellTop = Clamp(
            input.WorkingTop + (input.WorkingHeight - shellHeight) / 2 + input.CenterOffset,
            input.WorkingTop + 8,
            input.WorkingTop + input.WorkingHeight - shellHeight - 8);

        var windowLeft = centeredShellLeft - input.HorizontalOverhang;
        var windowTop = centeredShellTop - input.VerticalOverhang;

        switch (input.Edge)
        {
            case DockEdge.Bottom:
                windowTop = input.WorkingTop + input.WorkingHeight - shellHeight - edgeDistance - input.VerticalOverhang;
                break;
            case DockEdge.Top:
                windowTop = input.WorkingTop + edgeDistance - input.VerticalOverhang;
                break;
            case DockEdge.Left:
                windowLeft = input.WorkingLeft + edgeDistance - input.HorizontalOverhang;
                break;
            case DockEdge.Right:
                windowLeft = input.WorkingLeft + input.WorkingWidth - shellWidth - edgeDistance - input.HorizontalOverhang;
                break;
        }

        var shellLeft = Clamp(
            windowLeft + input.HorizontalOverhang,
            input.WorkingLeft,
            input.WorkingLeft + input.WorkingWidth - shellWidth);
        var shellTop = Clamp(
            windowTop + input.VerticalOverhang,
            input.WorkingTop,
            input.WorkingTop + input.WorkingHeight - shellHeight);

        windowLeft = shellLeft - input.HorizontalOverhang;
        windowTop = shellTop - input.VerticalOverhang;

        return new DockPlacement(windowLeft, windowTop, windowWidth, windowHeight, shellWidth, shellHeight);
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        if (minimum > maximum)
        {
            return value;
        }

        return value < minimum ? minimum : value > maximum ? maximum : value;
    }

    private static double ClampToAvailable(double value, double desiredMinimum, double maximum)
    {
        return Clamp(value, Math.Min(desiredMinimum, maximum), maximum);
    }
}

public readonly record struct DockGeometryInput(
    DockEdge Edge,
    double WorkingLeft,
    double WorkingTop,
    double WorkingWidth,
    double WorkingHeight,
    int ItemCount,
    double ItemButtonSize,
    double IconSpacing,
    double HorizontalOverhang,
    double VerticalOverhang,
    double EdgeDistance,
    double CenterOffset,
    double BarWidth,
    double BarHeight);

public readonly record struct DockPlacement(
    double WindowLeft,
    double WindowTop,
    double WindowWidth,
    double WindowHeight,
    double ShellWidth,
    double ShellHeight);
