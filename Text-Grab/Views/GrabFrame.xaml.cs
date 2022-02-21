using Fasetto.Word;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Windows.Media.Ocr;

namespace Text_Grab.Views
{
    /// <summary>
    /// Interaction logic for PersistentWindow.xaml
    /// </summary>
    public partial class GrabFrame : Window
    {
        private bool isDrawing = false;
        private OcrResult? ocrResultOfWindow;
        private ObservableCollection<WordBorder> wordBorders = new();
        private DispatcherTimer reDrawTimer = new();
        private bool isSelecting;
        private Point clickedPoint;
        private Border selectBorder = new();

        private ResultTable? AnalyedResultTable;

        public bool IsFromEditWindow { get; set; } = false;

        public GrabFrame()
        {
            InitializeComponent();

            WindowResizer resizer = new(this);
            reDrawTimer.Interval = new(0, 0, 0, 0, 1200);
            reDrawTimer.Tick += ReDrawTimer_Tick;

            RoutedCommand newCmd = new();
            _ = newCmd.InputGestures.Add(new KeyGesture(Key.Escape));
            _ = CommandBindings.Add(new CommandBinding(newCmd, Escape_Keyed));
        }

        private void GrabFrameWindow_Initialized(object sender, EventArgs e)
        {
            WindowUtilities.SetWindowPosition(this);
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            string windowSizeAndPosition = $"{this.Left},{this.Top},{this.Width},{this.Height}";
            Properties.Settings.Default.GrabFrameWindowSizeAndPosition = windowSizeAndPosition;
            Properties.Settings.Default.Save();

            WindowUtilities.ShouldShutDown();
        }

        private void Escape_Keyed(object sender, ExecutedRoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text) == false && SearchBox.Text != "Search For Text...")
                SearchBox.Text = "";
            else if (RectanglesCanvas.Children.Count > 0)
                ResetGrabFrame();
            else
                Close();
        }

        private async void ReDrawTimer_Tick(object? sender, EventArgs? e)
        {
            reDrawTimer.Stop();
            ResetGrabFrame();

            TextBox searchBox = SearchBox;

            if (searchBox != null)
                await DrawRectanglesAroundWords(searchBox.Text);
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void GrabBTN_Click(object sender, RoutedEventArgs e)
        {
            string frameText = "";

            if (wordBorders.Where(w => w.IsSelected == true).ToList().Count == 0)
            {
                Point windowPosition = this.GetAbsolutePosition();
                DpiScale dpi = VisualTreeHelper.GetDpi(this);
                System.Drawing.Rectangle rectCanvasSize = new System.Drawing.Rectangle
                {
                    Width = (int)((ActualWidth + 2) * dpi.DpiScaleX),
                    Height = (int)((Height - 64) * dpi.DpiScaleY),
                    X = (int)((windowPosition.X - 2) * dpi.DpiScaleX),
                    Y = (int)((windowPosition.Y + 24) * dpi.DpiScaleY)
                };
                frameText = await ImageMethods.GetRegionsText(null, rectCanvasSize);
            }

            if (wordBorders.Count > 0)
            {
                List<WordBorder>? selectedBorders = wordBorders.Where(w => w.IsSelected == true).ToList();

                if (selectedBorders.Count == 0)
                    selectedBorders.AddRange(wordBorders);

                List<string> lineList = new();
                StringBuilder outputString = new();
                int? lastLineNum = 0;
                int lastColumnNum = 0;

                if (selectedBorders.FirstOrDefault() != null)
                    lastLineNum = selectedBorders.FirstOrDefault()!.LineNumber;

                selectedBorders = selectedBorders.OrderBy(x => x.ResultColumnID).ToList();
                selectedBorders = selectedBorders.OrderBy(x => x.ResultRowID).ToList();

                int numberOfDistinctRows = selectedBorders.Select(x => x.ResultRowID).Distinct().Count();

                foreach (WordBorder border in selectedBorders)
                {
                    if (border.ResultColumnID != lastColumnNum && numberOfDistinctRows > 1)
                        lineList.Add("\t");
                    lastColumnNum = border.ResultColumnID;

                    if (border.ResultRowID != lastLineNum)
                    {
                        outputString.Append(string.Join(' ', lineList).Trim());
                        outputString.Replace(" \t ", "\t");
                        outputString.Append(Environment.NewLine);
                        lineList.Clear();
                        lastLineNum = border.ResultRowID;
                    }
                    lineList.Add(border.Word);
                }
                outputString.Append(string.Join(' ', lineList));

                frameText = outputString.ToString();
            }

            if (IsFromEditWindow == false
                && string.IsNullOrWhiteSpace(frameText) == false
                && Settings.Default.NeverAutoUseClipboard == false)
                Clipboard.SetText(frameText);

            if (Settings.Default.ShowToast == true
                && IsFromEditWindow == false)
                NotificationUtilities.ShowToast(frameText);

            if (IsFromEditWindow == true && string.IsNullOrWhiteSpace(frameText) == false)
                WindowUtilities.AddTextToOpenWindow(frameText);
        }

        private void ResetGrabFrame()
        {
            ocrResultOfWindow = null;
            RectanglesCanvas.Children.Clear();
            wordBorders.Clear();
            MatchesTXTBLK.Text = "Matches: 0";
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
            if (IsLoaded == false)
                return;

            ResetGrabFrame();

            reDrawTimer.Start();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsLoaded == false)
                return;

            ResetGrabFrame();

            reDrawTimer.Start();
        }

        private void GrabFrameWindow_Deactivated(object sender, EventArgs e)
        {
            ResetGrabFrame();
        }

        private async Task DrawRectanglesAroundWords(string searchWord)
        {
            if (isDrawing == true)
                return;

            isDrawing = true;

            RectanglesCanvas.Children.Clear();
            wordBorders.Clear();

            Point windowPosition = this.GetAbsolutePosition();
            DpiScale dpi = VisualTreeHelper.GetDpi(this);
            System.Drawing.Rectangle rectCanvasSize = new System.Drawing.Rectangle
            {
                Width = (int)((ActualWidth + 2) * dpi.DpiScaleX),
                Height = (int)((ActualHeight - 64) * dpi.DpiScaleY),
                X = (int)((windowPosition.X - 2) * dpi.DpiScaleX),
                Y = (int)((windowPosition.Y + 24) * dpi.DpiScaleY)
            };

            if (ocrResultOfWindow == null || ocrResultOfWindow.Lines.Count == 0)
                ocrResultOfWindow = await ImageMethods.GetOcrResultFromRegion(rectCanvasSize);

            if (ocrResultOfWindow == null)
                return;

            int numberOfMatches = 0;
            int lineNumber = 0;

            foreach (OcrLine ocrLine in ocrResultOfWindow.Lines)
            {
                foreach (OcrWord ocrWord in ocrLine.Words)
                {
                    string wordString = ocrWord.Text;

                    if (Settings.Default.CorrectErrors)
                        wordString = wordString.TryFixEveryWordLetterNumberErrors();

                    WordBorder wordBorderBox = new WordBorder
                    {
                        Width = (ocrWord.BoundingRect.Width / dpi.DpiScaleX),
                        Height = (ocrWord.BoundingRect.Height / dpi.DpiScaleY),
                        Word = wordString,
                        ToolTip = wordString,
                        LineNumber = lineNumber,
                        IsFromEditWindow = IsFromEditWindow
                    };

                    if ((bool)ExactMatchChkBx.IsChecked!)
                    {
                        if (wordString.Equals(searchWord, StringComparison.CurrentCulture))
                        {
                            wordBorderBox.Select();
                            numberOfMatches++;
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(searchWord)
                            && wordString.Contains(searchWord, StringComparison.CurrentCultureIgnoreCase))
                        {
                            wordBorderBox.Select();
                            numberOfMatches++;
                        }
                    }

                    wordBorders.Add(wordBorderBox);
                    _ = RectanglesCanvas.Children.Add(wordBorderBox);
                    Canvas.SetLeft(wordBorderBox, (ocrWord.BoundingRect.Left / dpi.DpiScaleX));
                    Canvas.SetTop(wordBorderBox, (ocrWord.BoundingRect.Top / dpi.DpiScaleY));
                }

                lineNumber++;
            }

            if (ocrResultOfWindow != null && ocrResultOfWindow.TextAngle != null)
            {
                RotateTransform transform = new RotateTransform((double)ocrResultOfWindow.TextAngle)
                {
                    CenterX = (Width - 4) / 2,
                    CenterY = (Height - 60) / 2
                };
                RectanglesCanvas.RenderTransform = transform;
            }

            if (TableToggleButton.IsChecked == true)
            {
                AnalyzeAsTable(rectCanvasSize);
            }

            List<UIElement> wordBordersRePlace = new();
            foreach (UIElement child in RectanglesCanvas.Children)
            {
                if (child is WordBorder wordBorder)
                    wordBordersRePlace.Add(wordBorder);
            }
            RectanglesCanvas.Children.Clear();
            foreach (UIElement uie in wordBordersRePlace)
            {
                if (uie is WordBorder wordBorder)
                {
                    wordBorder.Width += 4;
                    wordBorder.Height += 4;
                    RectanglesCanvas.Children.Add(wordBorder);
                    Canvas.SetLeft(wordBorder, Canvas.GetLeft(wordBorder) - 2);
                    Canvas.SetTop(wordBorder, Canvas.GetTop(wordBorder) - 2);
                }
            }

            if (TableToggleButton.IsChecked == true && AnalyedResultTable is not null)
            {
                DrawTable(AnalyedResultTable);
            }

            MatchesTXTBLK.Text = $"Matches: {numberOfMatches}";
            isDrawing = false;
        }

        private void DrawTable(ResultTable resultTable)
        {
            throw new NotImplementedException();
        }

        private void AnalyzeAsTable(System.Drawing.Rectangle rectCanvasSize)
        {
            int hitGridSpacing = 3;

            int numberOfVerticalLines = rectCanvasSize.Width / hitGridSpacing;
            int numberOfHorizontalLines = rectCanvasSize.Height / hitGridSpacing;

            List<ResultRow> resultRows = new();

            List<int> rowAreas = new();
            for (int i = 0; i < numberOfHorizontalLines; i++)
            {
                Border horzLine = new()
                {
                    Height = 1,
                    Width = rectCanvasSize.Width,
                    Opacity = 0,
                    Background = new SolidColorBrush(Colors.Gray)
                };
                Rect horzLineRect = new(0, i * hitGridSpacing, horzLine.Width, horzLine.Height);
                _ = RectanglesCanvas.Children.Add(horzLine);
                Canvas.SetTop(horzLine, i * 3);

                foreach (var child in RectanglesCanvas.Children)
                {
                    if (child is WordBorder wb)
                    {
                        Rect wbRect = new Rect(Canvas.GetLeft(wb), Canvas.GetTop(wb), wb.Width, wb.Height);
                        if (horzLineRect.IntersectsWith(wbRect) == true)
                        {
                            rowAreas.Add(i * hitGridSpacing);
                            break;
                        }
                    }
                }
            }

            int rowTop = 0;
            int rowCount = 0;
            for (int i = 0; i < rowAreas.Count; i++)
            {
                int thisLine = rowAreas[i];

                // check if should set this as top
                if (i == 0)
                    rowTop = thisLine;
                else
                {
                    int prevRow = rowAreas[i - 1];
                    if (thisLine - prevRow != hitGridSpacing)
                    {
                        rowTop = thisLine;
                    }
                }

                // check to see if at bottom of row
                if (i == rowAreas.Count - 1)
                {
                    resultRows.Add(new ResultRow { Top = rowTop, Bottom = thisLine, ID = rowCount });
                    rowCount++;
                }
                else
                {
                    int nextRow = rowAreas[i + 1];
                    if (nextRow - thisLine != hitGridSpacing)
                    {
                        resultRows.Add(new ResultRow { Top = rowTop, Bottom = thisLine, ID = rowCount });
                        rowCount++;
                    }
                }
            }

            List<int> columnAreas = new();
            for (int i = 0; i < numberOfVerticalLines; i++)
            {
                Border vertLine = new()
                {
                    Height = rectCanvasSize.Height,
                    Width = 1,
                    Opacity = 0,
                    Background = new SolidColorBrush(Colors.Gray)
                };
                _ = RectanglesCanvas.Children.Add(vertLine);
                Canvas.SetLeft(vertLine, i * hitGridSpacing);

                Rect vertLineRect = new(i * hitGridSpacing, 0, vertLine.Width, vertLine.Height);
                foreach (var child in RectanglesCanvas.Children)
                {
                    if (child is WordBorder wb)
                    {
                        Rect wbRect = new Rect(Canvas.GetLeft(wb), Canvas.GetTop(wb), wb.Width, wb.Height);
                        if (vertLineRect.IntersectsWith(wbRect) == true)
                        {
                            columnAreas.Add(i * hitGridSpacing);
                            break;
                        }
                    }
                }
            }

            List<ResultColumn> resultColumns = new();
            int columnLeft = 0;
            int columnCount = 0;
            for (int i = 0; i < columnAreas.Count; i++)
            {
                int thisLine = columnAreas[i];

                // check if should set this as top
                if (i == 0)
                    columnLeft = thisLine;
                else
                {
                    int prevColumn = columnAreas[i - 1];
                    if (thisLine - prevColumn != hitGridSpacing)
                    {
                        columnLeft = thisLine;
                    }
                }

                // check to see if at bottom of row
                if (i == columnAreas.Count - 1)
                {
                    resultColumns.Add(new ResultColumn { Left = columnLeft, Right = thisLine, ID = columnCount });
                    columnCount++;
                }
                else
                {
                    int nextColumn = columnAreas[i + 1];
                    if (nextColumn - thisLine != hitGridSpacing)
                    {
                        resultColumns.Add(new ResultColumn { Left = columnLeft, Right = thisLine, ID = columnCount });
                        columnCount++;
                    }
                }
            }

            Rect tableBoundingRect = new()
            {
                X = columnAreas.FirstOrDefault(),
                Y = rowAreas.FirstOrDefault(),
                Width = columnAreas.LastOrDefault() - columnAreas.FirstOrDefault(),
                Height = rowAreas.LastOrDefault() - rowAreas.FirstOrDefault()
            };

            // try 4 times to refine the rows and columns for outliers
            // on the fifth time set the word boundery properties
            for (int r = 0; r < 5; r++)
            {
                int outlierThreshould = 2;

                List<int> outlierRowIDs = new();

                foreach (ResultRow row in resultRows)
                {
                    int numberOfIntersectingWords = 0;
                    Border rowBorder = new()
                    {
                        Height = row.Bottom - row.Top,
                        Width = tableBoundingRect.Width,
                        Background = new SolidColorBrush(Colors.Red),
                        Tag = row.ID
                    };
                    RectanglesCanvas.Children.Add(rowBorder);
                    Canvas.SetLeft(rowBorder, tableBoundingRect.X);
                    Canvas.SetTop(rowBorder, row.Top);

                    Rect rowRect = new Rect(tableBoundingRect.X, row.Top, rowBorder.Width, rowBorder.Height);
                    foreach (var child in RectanglesCanvas.Children)
                    {
                        if (child is WordBorder wb)
                        {
                            Rect wbRect = new Rect(Canvas.GetLeft(wb), Canvas.GetTop(wb), wb.Width, wb.Height);
                            if (rowRect.IntersectsWith(wbRect) == true)
                            {
                                numberOfIntersectingWords++;
                                wb.ResultRowID = row.ID;
                            }
                        }
                    }

                    if (numberOfIntersectingWords <= outlierThreshould && r != 4)
                        outlierRowIDs.Add(row.ID);
                }

                if (outlierRowIDs.Count > 0)
                    mergeTheseRowIDs(resultRows, outlierRowIDs);


                List<int> outlierColumnIDs = new();

                foreach (ResultColumn column in resultColumns)
                {
                    int numberOfIntersectingWords = 0;
                    Border columnBorder = new()
                    {
                        Height = tableBoundingRect.Height,
                        Width = column.Right - column.Left,
                        Background = new SolidColorBrush(Colors.Blue),
                        Opacity = 0.2,
                        Tag = column.ID
                    };
                    RectanglesCanvas.Children.Add(columnBorder);
                    Canvas.SetLeft(columnBorder, column.Left);
                    Canvas.SetTop(columnBorder, tableBoundingRect.Y);

                    Rect columnRect = new Rect(column.Left, tableBoundingRect.Y, columnBorder.Width, columnBorder.Height);
                    foreach (var child in RectanglesCanvas.Children)
                    {
                        if (child is WordBorder wb)
                        {
                            Rect wbRect = new Rect(Canvas.GetLeft(wb), Canvas.GetTop(wb), wb.Width, wb.Height);
                            if (columnRect.IntersectsWith(wbRect) == true)
                            {
                                numberOfIntersectingWords++;
                                wb.ResultColumnID = column.ID;
                            }
                        }
                    }

                    if (numberOfIntersectingWords <= outlierThreshould)
                        outlierColumnIDs.Add(column.ID);
                }

                if (outlierColumnIDs.Count > 0 && r != 4)
                    mergetheseColumnIDs(resultColumns, outlierColumnIDs);
            }

            AnalyedResultTable = new ResultTable(resultColumns, resultRows);

            // Draw the lines and bounds of the table
            Border tableOutline = new()
            {
                Width = tableBoundingRect.Width,
                Height = tableBoundingRect.Height,
                BorderThickness = new Thickness(2),
                BorderBrush = new SolidColorBrush(Colors.Yellow)
            };
            RectanglesCanvas.Children.Add(tableOutline);
            Canvas.SetTop(tableOutline, tableBoundingRect.Top);
            Canvas.SetLeft(tableOutline, tableBoundingRect.Left);

            // foreach (ResultRow row in resultRows)
            // {
            //     Border rowBorder = new()
            //     {
            //         Height = row.Bottom - row.Top,
            //         Width = tableBoundingRect.Width,
            //         Background = new SolidColorBrush(Colors.Red),
            //         Opacity = 0.2,
            //         Tag = row.ID
            //     };
            //     RectanglesCanvas.Children.Add(rowBorder);
            //     Canvas.SetLeft(rowBorder, tableBoundingRect.X);
            //     Canvas.SetTop(rowBorder, row.Top);
            // }

            // foreach (ResultColumn column in resultColumns)
            // {
            //     Border columnBorder = new()
            //     {
            //         Height = tableBoundingRect.Height,
            //         Width = column.Right - column.Left,
            //         Background = new SolidColorBrush(Colors.Blue),
            //         Opacity = 0.2,
            //         Tag = column.ID
            //     };
            //     RectanglesCanvas.Children.Add(columnBorder);
            //     Canvas.SetLeft(columnBorder, column.Left);
            //     Canvas.SetTop(columnBorder, tableBoundingRect.Y);
            // }
        }

        private static void mergetheseColumnIDs(List<ResultColumn> resultColumns, List<int> outlierColumnIDs)
        {
            for (int i = 0; i < outlierColumnIDs.Count; i++)
            {
                for (int j = 0; j < resultColumns.Count; j++)
                {
                    ResultColumn jthColumn = resultColumns[j];
                    if (jthColumn.ID == outlierColumnIDs[i])
                    {
                        if (j == 0)
                        {
                            // merge with next column if possible
                            if (j + 1 < resultColumns.Count)
                            {
                                ResultColumn nextColumn = resultColumns[j + 1];
                                nextColumn.Left = jthColumn.Left;
                            }
                        }
                        else if (j == resultColumns.Count - 1)
                        {
                            // merge with previous column
                            if (j - 1 >= 0)
                            {
                                ResultColumn prevColumn = resultColumns[j - 1];
                                prevColumn.Right = jthColumn.Right;
                            }
                        }
                        else
                        {
                            // merge with closet column
                            ResultColumn prevColumn = resultColumns[j - 1];
                            ResultColumn nextColumn = resultColumns[j + 1];
                            int distToPrev = (int)(jthColumn.Left - prevColumn.Right);
                            int distToNext = (int)(nextColumn.Left - jthColumn.Right);

                            if (distToNext < distToPrev)
                            {
                                // merge with next column
                                nextColumn.Left = jthColumn.Left;
                            }
                            else
                            {
                                // merge with prev column
                                prevColumn.Right = jthColumn.Right;
                            }
                        }
                        resultColumns.RemoveAt(j);
                    }
                }
            }
        }

        private static void mergeTheseRowIDs(List<ResultRow> resultRows, List<int> outlierRowIDs)
        {

        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded == false)
                return;

            if (sender is TextBox searchBox)
                await DrawRectanglesAroundWords(searchBox.Text);
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox? searchBox = sender as TextBox;

            if (searchBox != null)
                searchBox.Text = "";
        }

        private async void ExactMatchChkBx_Click(object sender, RoutedEventArgs e)
        {
            TextBox searchBox = SearchBox;

            if (searchBox != null)
                await DrawRectanglesAroundWords(searchBox.Text);
        }

        private void ClearBTN_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
        }

        private async void RefreshBTN_Click(object sender, RoutedEventArgs e)
        {
            TextBox searchBox = SearchBox;
            ResetGrabFrame();

            await Task.Delay(200);

            if (searchBox != null)
                await DrawRectanglesAroundWords(searchBox.Text);
        }

        private void SettingsBTN_Click(object sender, RoutedEventArgs e)
        {
            WindowUtilities.OpenOrActivateWindow<SettingsWindow>();
        }

        private void GrabFrameWindow_Activated(object sender, EventArgs e)
        {
            reDrawTimer.Start();
        }

        private void RectanglesCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            isSelecting = true;
            clickedPoint = e.GetPosition(RectanglesCanvas);
            RectanglesCanvas.CaptureMouse();
            CursorClipper.ClipCursor(RectanglesCanvas);
            selectBorder.Height = 1;
            selectBorder.Width = 1;

            try { RectanglesCanvas.Children.Remove(selectBorder); } catch (Exception) { }

            selectBorder.BorderThickness = new Thickness(2);
            Color borderColor = Color.FromArgb(255, 40, 118, 126);
            selectBorder.BorderBrush = new SolidColorBrush(borderColor);
            Color backgroundColor = Color.FromArgb(15, 40, 118, 126);
            selectBorder.Background = new SolidColorBrush(backgroundColor);
            _ = RectanglesCanvas.Children.Add(selectBorder);
            Canvas.SetLeft(selectBorder, clickedPoint.X);
            Canvas.SetTop(selectBorder, clickedPoint.Y);
        }

        private async void RectanglesCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            isSelecting = false;
            CursorClipper.UnClipCursor();
            RectanglesCanvas.ReleaseMouseCapture();

            try { RectanglesCanvas.Children.Remove(selectBorder); } catch { }

            await Task.Delay(50);
            CheckSelectBorderIntersections(true);
        }

        private void RectanglesCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (isSelecting == false)
                return;

            Point movingPoint = e.GetPosition(RectanglesCanvas);

            var left = Math.Min(clickedPoint.X, movingPoint.X);
            var top = Math.Min(clickedPoint.Y, movingPoint.Y);

            selectBorder.Height = Math.Max(clickedPoint.Y, movingPoint.Y) - top;
            selectBorder.Width = Math.Max(clickedPoint.X, movingPoint.X) - left;

            Canvas.SetLeft(selectBorder, left);
            Canvas.SetTop(selectBorder, top);

            CheckSelectBorderIntersections();
        }

        private void CheckSelectBorderIntersections(bool finalCheck = false)
        {
            Rect rectSelect = new Rect(Canvas.GetLeft(selectBorder), Canvas.GetTop(selectBorder), selectBorder.Width, selectBorder.Height);

            bool clickedEmptySpace = true;
            bool smallSelction = false;
            if (rectSelect.Width < 10 && rectSelect.Height < 10)
                smallSelction = true;

            foreach (WordBorder wordBorder in wordBorders)
            {
                Rect wbRect = new Rect(Canvas.GetLeft(wordBorder), Canvas.GetTop(wordBorder), wordBorder.Width, wordBorder.Height);

                if (rectSelect.IntersectsWith(wbRect))
                {
                    clickedEmptySpace = false;

                    if (smallSelction == false)
                    {
                        wordBorder.Select();
                        wordBorder.WasRegionSelected = true;
                    }
                    else
                    {
                        if (wordBorder.IsSelected)
                            wordBorder.Deselect();
                        else
                            wordBorder.Select();
                        wordBorder.WasRegionSelected = false;
                    }

                }
                else
                {
                    if (wordBorder.WasRegionSelected == true
                        && smallSelction == false)
                        wordBorder.Deselect();
                }

                if (finalCheck == true)
                    wordBorder.WasRegionSelected = false;
            }

            if (clickedEmptySpace == true
                && smallSelction == true
                && finalCheck == true)
            {
                foreach (WordBorder wb in wordBorders)
                    wb.Deselect();
            }
        }

        private async void TableToggleButton_Click(object sender, RoutedEventArgs e)
        {
            TextBox searchBox = SearchBox;
            ResetGrabFrame();

            await Task.Delay(200);

            if (searchBox != null)
                await DrawRectanglesAroundWords(searchBox.Text);
        }
    }
}
