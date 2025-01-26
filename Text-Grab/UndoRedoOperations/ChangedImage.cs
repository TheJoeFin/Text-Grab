using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;
using Text_Grab.Controls;

namespace Text_Grab.UndoRedoOperations;

internal class ChangedImage : Operation, IUndoRedoOperation
{
    public ChangedImage(uint transactionId, Image destination, List<WordBorder> previousWordBorders,
        Canvas canvas, ICollection<WordBorder> wordBorders, ImageSource? oldImage, ImageSource? newImage) : base(transactionId)
    {
        DestinationImage = destination;
        RectanglesCanvas = canvas;
        PreviousWordBorders = previousWordBorders;
        WordBorders = wordBorders;
        OldImage = oldImage;
        NewImage = newImage;
    }

    private readonly ImageSource? OldImage;

    private readonly ImageSource? NewImage;

    private readonly Image DestinationImage;

    private readonly Canvas RectanglesCanvas;

    private readonly List<WordBorder> PreviousWordBorders;

    private readonly ICollection<WordBorder> WordBorders;

    public UndoRedoOperation GetUndoRedoOperation() => UndoRedoOperation.ChangedImage;

    public void Undo()
    {
        DestinationImage.Source = OldImage;

        foreach (WordBorder wordBorder in PreviousWordBorders)
        {
            RectanglesCanvas.Children.Add(wordBorder);
            WordBorders.Add(wordBorder);
        }
    }

    public void Redo()
    {
        DestinationImage.Source = NewImage;
        RectanglesCanvas.Children.Clear();
        WordBorders.Clear();
    }
}
