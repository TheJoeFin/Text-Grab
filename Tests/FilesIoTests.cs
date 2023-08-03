using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Text_Grab.Utilities;

namespace Tests;

public class FilesIoTests
{
    private const string fontSamplePath = @".\Images\font_sample.png";

    [WpfFact]
    public async Task ShouldBeAbleToSaveImage()
    {
        Bitmap fontSampleBitmap = new Bitmap(FileUtilities.GetPathToLocalFile(fontSamplePath));

        bool couldSave = await FileUtilities.SaveImageFile(fontSampleBitmap, "newTest.png", Text_Grab.FileStorageKind.WithHistory);

        Assert.True(couldSave);
    }
}
