using System;
using System.Collections.Generic;
using System.Linq;

namespace Text_Grab.Models;

public enum GrabFrameTablePlacementMode
{
    None,
    AddRow,
    AddColumn,
}

public sealed class GrabFrameTableEditState
{
    public const double MinimumSeparatorGap = 6;

    public List<double> ManualColumnSeparators { get; private set; } = [];

    public List<double> ManualRowSeparators { get; private set; } = [];

    public GrabFrameTablePlacementMode PlacementMode { get; private set; }

    public double? PreviewPosition { get; private set; }

    public bool IsPreviewValid { get; private set; }

    public bool IsPlacementActive => PlacementMode != GrabFrameTablePlacementMode.None;

    public void BeginPlacement(GrabFrameTablePlacementMode placementMode)
    {
        PlacementMode = placementMode;
        PreviewPosition = null;
        IsPreviewValid = false;
    }

    public void CancelPlacement()
    {
        PlacementMode = GrabFrameTablePlacementMode.None;
        PreviewPosition = null;
        IsPreviewValid = false;
    }

    public void ClearAll()
    {
        CancelPlacement();
        ManualRowSeparators = [];
        ManualColumnSeparators = [];
    }

    public IReadOnlyList<double> GetExistingSeparatorsForPlacement()
    {
        return PlacementMode switch
        {
            GrabFrameTablePlacementMode.AddRow => ManualRowSeparators,
            GrabFrameTablePlacementMode.AddColumn => ManualColumnSeparators,
            _ => []
        };
    }

    public void SetManualSeparators(IEnumerable<double>? manualRowSeparators, IEnumerable<double>? manualColumnSeparators)
    {
        ManualRowSeparators = NormalizeSeparators(manualRowSeparators);
        ManualColumnSeparators = NormalizeSeparators(manualColumnSeparators);
    }

    public void ScaleSeparators(double rowScale, double columnScale)
    {
        if (double.IsFinite(rowScale) && rowScale > 0)
            ManualRowSeparators = NormalizeSeparators(ManualRowSeparators.Select(position => position * rowScale));

        if (double.IsFinite(columnScale) && columnScale > 0)
            ManualColumnSeparators = NormalizeSeparators(ManualColumnSeparators.Select(position => position * columnScale));

        if (PreviewPosition is not double previewPosition)
            return;

        if (PlacementMode == GrabFrameTablePlacementMode.AddRow && double.IsFinite(rowScale) && rowScale > 0)
            PreviewPosition = Math.Round(previewPosition * rowScale);
        else if (PlacementMode == GrabFrameTablePlacementMode.AddColumn && double.IsFinite(columnScale) && columnScale > 0)
            PreviewPosition = Math.Round(previewPosition * columnScale);
    }

    public bool TryCommitPreview()
    {
        if (!IsPlacementActive || !IsPreviewValid || PreviewPosition is not double previewPosition)
            return false;

        List<double> separatorList = PlacementMode == GrabFrameTablePlacementMode.AddRow
            ? ManualRowSeparators
            : ManualColumnSeparators;

        separatorList.Add(previewPosition);
        separatorList.Sort();
        separatorList = NormalizeSeparators(separatorList);

        if (PlacementMode == GrabFrameTablePlacementMode.AddRow)
            ManualRowSeparators = separatorList;
        else
            ManualColumnSeparators = separatorList;

        return true;
    }

    public bool TryUpdatePreview(
        double requestedPosition,
        double minimumPosition,
        double maximumPosition,
        IEnumerable<double> existingSeparators,
        double minimumGap = MinimumSeparatorGap)
    {
        if (!IsPlacementActive)
        {
            PreviewPosition = null;
            IsPreviewValid = false;
            return false;
        }

        IsPreviewValid = TryNormalizeSeparatorPosition(
            requestedPosition,
            minimumPosition,
            maximumPosition,
            existingSeparators,
            minimumGap,
            out double normalizedPosition);

        PreviewPosition = normalizedPosition;
        return IsPreviewValid;
    }

    public static List<double> NormalizeSeparators(IEnumerable<double>? separators)
    {
        if (separators is null)
            return [];

        return [.. separators
            .Where(double.IsFinite)
            .Select(position => Math.Round(position))
            .Distinct()
            .OrderBy(position => position)];
    }

    public static bool TryNormalizeSeparatorPosition(
        double requestedPosition,
        double minimumPosition,
        double maximumPosition,
        IEnumerable<double>? existingSeparators,
        double minimumGap,
        out double normalizedPosition)
    {
        normalizedPosition = 0;

        if (!double.IsFinite(requestedPosition)
            || !double.IsFinite(minimumPosition)
            || !double.IsFinite(maximumPosition)
            || !double.IsFinite(minimumGap)
            || maximumPosition <= minimumPosition)
        {
            return false;
        }

        double clampedPosition = Math.Round(Math.Clamp(requestedPosition, minimumPosition, maximumPosition));
        normalizedPosition = clampedPosition;

        if (clampedPosition <= minimumPosition || clampedPosition >= maximumPosition)
            return false;

        foreach (double existingPosition in NormalizeSeparators(existingSeparators))
        {
            if (Math.Abs(existingPosition - clampedPosition) < minimumGap)
                return false;
        }

        return true;
    }
}
