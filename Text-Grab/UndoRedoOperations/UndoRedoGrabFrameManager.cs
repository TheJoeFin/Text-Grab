using System;
using System.Collections.Generic;
using System.Linq;

namespace Text_Grab.UndoRedoOperations;

internal class UndoRedoStrokeManager
{
    private Dictionary<uint, uint> RefCountMap { get; } = new Dictionary<uint, uint>();

    private uint TotalStrokeCount { get; set; }


    public IReadOnlyList<uint> AddStrokes(IReadOnlyList<InkStroke> inkStrokes)
    {
        var strokeIds = new List<uint>();
        var strokeIdArray = new uint[inkStrokes.Count];

        if (inkStrokes.Count == 0)
        {
            return strokeIds.AsReadOnly();
        }

        // duplicate list
        var strokesToAdd = inkStrokes.ToList();

        // Keep a second copy that we can search through
        var inkStrokesSearchList = inkStrokes.ToList();

        // First search through existing strokes.
        foreach (var pair in StrokeMap)
        {
            var index = strokesToAdd.FindIndex(stroke => stroke == pair.Value);
            if (index >= 0)
            {
                strokesToAdd.RemoveAt(index);

                var index2 = inkStrokesSearchList.FindIndex(stroke => stroke == pair.Value);
                if (index2 >= 0)
                {
                    strokeIdArray[index2] = pair.Key;
                }
            }
        }

        // Add remaining strokes
        foreach (var stroke in strokesToAdd)
        {
            uint newId = TotalStrokeCount;
            ++TotalStrokeCount;
            StrokeMap.Add(newId, stroke);
            RefCountMap.Add(newId, 0);

            var index = inkStrokesSearchList.FindIndex(listStroke => listStroke == stroke);
            if (index >= 0)
            {
                strokeIdArray[index] = newId;
            }
        }

        // ensure stroke order is identical
        for (uint i = 0; i < inkStrokes.Count; i++)
        {
            strokeIds.Add(strokeIdArray[i]);
        }

        return strokeIds.AsReadOnly();
    }

    public void UpdateStroke(uint id, InkStroke newInkStroke)
    {
        if (StrokeMap.ContainsKey(id))
        {
            StrokeMap[id] = newInkStroke;
        }
    }

    public InkStroke CloneAndUpdateStroke(uint id)
    {
        var gotStroke = GetStroke(id);

        if (gotStroke == null)
            return gotStroke;

        var strokeClone = CloneInkStroke(gotStroke);
        UpdateStroke(id, strokeClone);
        return strokeClone;
    }

    public void Clear()
    {
        StrokeMap.Clear();
        RefCountMap.Clear();
        TotalStrokeCount = 0;
    }

    public void AddRef(uint strokeId)
    {
        if (RefCountMap.ContainsKey(strokeId))
        {
            RefCountMap[strokeId]++;
        }
    }

    public void RemoveRef(uint strokeId)
    {
        if (RefCountMap.ContainsKey(strokeId))
        {
            var currentRef = RefCountMap[strokeId];

            if (currentRef <= 1)
            {
                RefCountMap.Remove(strokeId);
                StrokeMap.Remove(strokeId);
            }
            else
            {
                RefCountMap[strokeId]--;
            }
        }
    }

    public static InkStroke CloneInkStroke(InkStroke stroke)
    {
        var inkbuilder = new InkStrokeBuilder();
        var newStroke = inkbuilder.CreateStrokeFromInkPoints(stroke.GetInkPoints(), stroke.PointTransform);
        newStroke.DrawingAttributes = stroke.DrawingAttributes;

        return newStroke;
    }
}