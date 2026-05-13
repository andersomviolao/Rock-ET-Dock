using System;
using Dock.App.Models;

namespace Dock.App.Services;

public static class DockGeometry
{
    public static DockPlacement Calculate(DockGeometryInput input)
    {
        var itemCount = Math.Max(1, input.ItemCount);
        var itemStep = input.ItemButtonSize + Math.Max(0, input.IconSpacing);
        var thickness = input.ItemButtonSize + 28;
        var edgeDistance = 8 + input.EdgeDistance;
        var maxShellWidth = Math.Max(40, input.WorkingWidth - 32 - (input.Overhang * 2));
        var maxShellHeight = Math.Max(40, input.WorkingHeight - 32 - (input.Overhang * 2));

        double shellWidth;
        double shellHeight;

        if (input.Edge is DockEdge.Left or DockEdge.Right)
        {
            shellWidth = input.BarWidth > 0
                ? Clamp(input.BarWidth, 40, maxShellWidth)
                : thickness;
            shellHeight = input.BarHeight > 0
                ? Clamp(input.BarHeight, Math.Min(thickness + 24, maxShellHeight), maxShellHeight)
                : Math.Min(maxShellHeight, Math.Max(thickness + 24, itemCount * itemStep + 24));
        }
        else
        {
            shellWidth = input.BarWidth > 0
                ? Clamp(input.BarWidth, Math.Min(thickness + 80, maxShellWidth), maxShellWidth)
                : Math.Min(maxShellWidth, Math.Max(thickness + 80, itemCount * itemStep + 24));
            shellHeight = input.BarHeight > 0
                ? Clamp(input.BarHeight, 40, maxShellHeight)
                : thickness;
        }

        var windowWidth = shellWidth + (input.Overhang * 2);
        var windowHeight = shellHeight + (input.Overhang * 2);
        var centeredShellLeft = Clamp(
            input.WorkingLeft + (input.WorkingWidth - shellWidth) / 2 + input.CenterOffset,
            input.WorkingLeft + 8,
            input.WorkingLeft + input.WorkingWidth - shellWidth - 8);
        var centeredShellTop = Clamp(
            input.WorkingTop + (input.WorkingHeight - shellHeight) / 2 + input.CenterOffset,
            input.WorkingTop + 8,
            input.WorkingTop + input.WorkingHeight - shellHeight - 8);

        var windowLeft = centeredShellLeft - input.Overhang;
        var windowTop = centeredShellTop - input.Overhang;

        switch (input.Edge)
        {
            case DockEdge.Bottom:
                windowTop = input.WorkingTop + input.WorkingHeight - shellHeight - edgeDistance - input.Overhang;
                break;
            case DockEdge.Top:
                windowTop = input.WorkingTop + edgeDistance - input.Overhang;
                break;
            case DockEdge.Left:
                windowLeft = input.WorkingLeft + edgeDistance - input.Overhang;
                break;
            case DockEdge.Right:
                windowLeft = input.WorkingLeft + input.WorkingWidth - shellWidth - edgeDistance - input.Overhang;
                break;
        }

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
    double Overhang,
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
