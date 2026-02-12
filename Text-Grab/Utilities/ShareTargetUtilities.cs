using Microsoft.Windows.AppLifecycle;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Text_Grab.Views;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.DataTransfer.ShareTarget;
using Windows.Storage;

namespace Text_Grab.Utilities;

public static class ShareTargetUtilities
{
    public static bool IsShareTargetActivation()
    {
        try
        {
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();
            return args.Kind == ExtendedActivationKind.ShareTarget;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error checking share target activation: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> HandleShareTargetActivationAsync()
    {
        try
        {
            AppActivationArguments args = AppInstance.GetCurrent().GetActivatedEventArgs();

            if (args.Kind != ExtendedActivationKind.ShareTarget)
                return false;

            if (args.Data is not ShareTargetActivatedEventArgs shareArgs)
                return false;

            ShareOperation shareOperation = shareArgs.ShareOperation;
            DataPackageView data = shareOperation.Data;

            bool handled = false;

            if (data.Contains(StandardDataFormats.StorageItems))
            {
                handled = await HandleSharedStorageItemsAsync(data);
            }
            else if (data.Contains(StandardDataFormats.Bitmap))
            {
                handled = await HandleSharedBitmapAsync(data);
            }
            else if (data.Contains(StandardDataFormats.Text))
            {
                handled = await HandleSharedTextAsync(data);
            }
            else if (data.Contains(StandardDataFormats.Uri))
            {
                handled = await HandleSharedUriAsync(data);
            }

            shareOperation.ReportCompleted();
            return handled;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error handling share target activation: {ex.Message}");
            return false;
        }
    }

    private static async Task<bool> HandleSharedStorageItemsAsync(DataPackageView data)
    {
        var items = await data.GetStorageItemsAsync();

        foreach (IStorageItem item in items)
        {
            if (item is StorageFile file && IoUtilities.IsImageFileExtension(Path.GetExtension(file.Path)))
            {
                GrabFrame gf = new(file.Path);
                gf.Show();
                gf.Activate();
                return true;
            }
        }

        // If non-image files were shared, try to read as text
        foreach (IStorageItem item in items)
        {
            if (item is StorageFile file)
            {
                try
                {
                    string text = await FileIO.ReadTextAsync(file);
                    EditTextWindow etw = new();
                    etw.AddThisText(text);
                    etw.Show();
                    etw.Activate();
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to read shared file as text: {ex.Message}");
                }
            }
        }

        return false;
    }

    private static async Task<bool> HandleSharedBitmapAsync(DataPackageView data)
    {
        var bitmapRef = await data.GetBitmapAsync();
        using var stream = await bitmapRef.OpenReadAsync();

        string tempPath = Path.Combine(Path.GetTempPath(), $"TextGrab_Share_{Guid.NewGuid():N}.png");

        using (var fileStream = File.Create(tempPath))
        {
            var inputStream = stream.GetInputStreamAt(0);
            using var reader = new Windows.Storage.Streams.DataReader(inputStream);
            ulong size = stream.Size;
            await reader.LoadAsync((uint)size);
            byte[] buffer = new byte[size];
            reader.ReadBytes(buffer);
            await fileStream.WriteAsync(buffer);
        }

        GrabFrame gf = new(tempPath);
        gf.Show();
        gf.Activate();
        return true;
    }

    private static async Task<bool> HandleSharedTextAsync(DataPackageView data)
    {
        string text = await data.GetTextAsync();

        EditTextWindow etw = new();
        etw.AddThisText(text);
        etw.Show();
        etw.Activate();
        return true;
    }

    private static async Task<bool> HandleSharedUriAsync(DataPackageView data)
    {
        Uri uri = await data.GetUriAsync();

        EditTextWindow etw = new();
        etw.AddThisText(uri.ToString());
        etw.Show();
        etw.Activate();
        return true;
    }
}
