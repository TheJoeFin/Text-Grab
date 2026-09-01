// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Text_Grab.Extensions;

internal static class StorageFileExtensions
{
    private static bool IsPackagedApp = (Environment.GetEnvironmentVariable("PACKAGED_PRODUCT_ID") != null);

    internal static async Task<string> ReadTextAsync(this string filepath)
    {
        if (string.IsNullOrWhiteSpace(filepath))
        {
            return string.Empty;
        }

        if (IsPackagedApp)
        {
            StorageFile file = await CreateStorageFile(filepath);
            string content = await FileIO.ReadTextAsync(file);
            return content;
        }
        else
        {
            string filePath = CombineWithBasePath(filepath);
            string content = await File.ReadAllTextAsync(filePath);
            return content;
        }
    }

    internal static async Task<IRandomAccessStream> CreateStreamAsync(this string filepath)
    {
        if (string.IsNullOrWhiteSpace(filepath))
        {
            return MemoryStream.Null.AsRandomAccessStream();
        }

        if (IsPackagedApp)
        {
            StorageFile file = await CreateStorageFile(filepath);
            return await file.OpenAsync(FileAccessMode.Read);
        }
        else
        {
            string filePath = CombineWithBasePath(filepath);
            return File.OpenRead(filePath).AsRandomAccessStream();
        }
    }
    private static Task<StorageFile> CreateStorageFile(string filepath)
    {
        Uri uri = new Uri("ms-appx:///" + filepath);
        return StorageFile.GetFileFromApplicationUriAsync(uri).AsTask();
    }

    private static string CombineWithBasePath(string filepath)
    {
        return Path.Combine(AppContext.BaseDirectory, filepath);
    }
}
