using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;

namespace Text_Grab.Extensions;

internal static class ImageExtensions
{
    private const int exifOrientationID = 0x112; //274

    internal static void ExifRotate(this Image img)
    {
        RotateFlipType rot = img.GetRotateFlipType();
        if (rot != RotateFlipType.RotateNoneFlipNone)
        {
            img.RotateFlip(rot);
            img.RemovePropertyItem(exifOrientationID);
        }
    }

    internal static RotateFlipType GetRotateFlipType(this Image img)
    {
        if (!img.PropertyIdList.Contains(exifOrientationID)
            || img.GetPropertyItem(exifOrientationID) is not PropertyItem prop
            || prop.Value is not byte[] propValue)
            return RotateFlipType.RotateNoneFlipNone;

        int val = BitConverter.ToUInt16(propValue, 0);
        var rot = RotateFlipType.RotateNoneFlipNone;

        if (val == 3 || val == 4)
            rot = RotateFlipType.Rotate180FlipNone;
        else if (val == 5 || val == 6)
            rot = RotateFlipType.Rotate90FlipNone;
        else if (val == 7 || val == 8)
            rot = RotateFlipType.Rotate270FlipNone;

        if (val == 2 || val == 4 || val == 5 || val == 7)
            rot |= RotateFlipType.RotateNoneFlipX;

        return rot;
    }
}
