# Text-Grab

This is a minimal Windows utility which makes all visible text availble for copy and paste. 

Too often there is text on the screen but it is unable to be selected. This happens when text is saved in an image, in a video, or the text within an application. 

The OCR is done locally by [Windows 10 API](https://docs.microsoft.com/en-us/uwp/api/Windows.Media.Ocr). This enables Text Grab to have essentially no UI and not require a constantly running background process.

## Two Use Cases

The first usecase is the most obvious, selecting a region of of the screen and the text within the selected region will be added to the clipboard.

The second usecase takes a single click and attempts to copy the word which was clicked on. This is enabled because the Windows 10 OCR API draws a bounding box around each recognized word. 

If the click point or selected region has no text in it the Text Grab window stays active. To exit the application press the escape key, or Alt+F4. 
