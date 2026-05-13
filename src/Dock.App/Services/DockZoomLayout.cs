using System;
using System.Collections.Generic;

namespace Dock.App.Services;

public static class DockZoomLayout
{
    public static IReadOnlyList<double> CalculateOffsets(
        IReadOnlyList<double> centerAxes,
        IReadOnlyList<double> scales,
        double itemSize)
    {
        if (centerAxes.Count != scales.Count)
        {
            throw new ArgumentException("Center and scale lists must have the same length.");
        }

        var offsets = new double[centerAxes.Count];
        var baseItemSize = Math.Max(1, itemSize);

        for (var itemIndex = 0; itemIndex < centerAxes.Count; itemIndex++)
        {
            var offset = 0.0;
            for (var influenceIndex = 0; influenceIndex < centerAxes.Count; influenceIndex++)
            {
                if (itemIndex == influenceIndex || scales[influenceIndex] <= 1.001)
                {
                    continue;
                }

                var expansion = baseItemSize * (scales[influenceIndex] - 1.0);
                if (centerAxes[itemIndex] < centerAxes[influenceIndex])
                {
                    offset -= expansion / 2.0;
                }
                else if (centerAxes[itemIndex] > centerAxes[influenceIndex])
                {
                    offset += expansion / 2.0;
                }
            }

            offsets[itemIndex] = offset;
        }

        return offsets;
    }
}
