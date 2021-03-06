# Text-Grab

This is a minimal Windows utility which makes all visible text available for copy and paste. 

Too often there is text on the screen but it is unable to be selected. This happens when text is saved in an image, in a video, or the text within an application. 

The OCR is done locally by [Windows 10 API](https://docs.microsoft.com/en-us/uwp/api/Windows.Media.Ocr). This enables Text Grab to have essentially no UI and not require a constantly running background process.

## Two Modes

### Full Screen Mode

The first full screen use case is the most obvious, selecting a region of the screen and the text within the selected region will be added to the clipboard.
![Select text from a region](images/Region-Toast-Edit-2.gif)

The second use case takes a single click and attempts to copy the word which was clicked on. This is enabled because the Windows 10 OCR API draws a bounding box around each recognized word. 
![Select clicked word](images/Single-Click.gif)

If the click point or selected region has no text in it the Text Grab window stays active. To exit the application, press the escape key, or Alt+F4.

### Grab Frame Mode

Grab frame is a mostly transparent frame with a search bar and Grab button. The Grab Frame can be positioned wherever you want to copy the text. This can be done by searching for text, clicking on a word border, and/or clicking on the Grab button.

The underlying OCR technology is the same as the full screen mode and has all of the same benefits and drawbacks. Since Text Grab is using OCR the recognition is not perfect. However, adjusting the size and position of the window does affect the OCR's accuracy.

![Grab Frame](images/Grab-Frame.gif)

## Principles
Text Grab is designed to be as minimal and quick as possible. By using Windows 10’s OCR capabilities Text Grab can launch quickly without needing to run in the background. Pinning Text Grab to the Taskbar enables launching via keyboard shortcut. 
There is no history, or dialog box, or feedback. This tool is designed to be used hundreds of times a day. Reducing clicks and menus means saving time, which is the primary focus of Text Grab. 
### Thanks for using Text Grab
Hopefully this simple app makes you more productive and saves you time from transcribing text.
If you have any questions or feedback reach out on Twitter [@TheJoeFin](http://www.twitter.com/thejoefin) or by email joe@textgrab.net
