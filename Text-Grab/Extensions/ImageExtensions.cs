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
        RotateFlipType rot = RotateFlipType.RotateNoneFlipNone;

        if (val is 3 or 4)
            rot = RotateFlipType.Rotate180FlipNone;
        else if (val is 5 or 6)
            rot = RotateFlipType.Rotate90FlipNone;
        else if (val is 7 or 8)
            rot = RotateFlipType.Rotate270FlipNone;

        if (val is 2 or 4 or 5 or 7)
            rot |= RotateFlipType.RotateNoneFlipX;

        return rot;
    }
}
