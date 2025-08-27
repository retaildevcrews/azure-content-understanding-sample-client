using System;
using System.IO;

namespace ContentUnderstanding.Client.Utilities
{
    internal static class MimeTypeProvider
    {
        public static string GetContentType(string pathOrExtension)
        {
            if (string.IsNullOrWhiteSpace(pathOrExtension))
                return "application/octet-stream";

            var ext = pathOrExtension.StartsWith('.')
                ? pathOrExtension
                : Path.GetExtension(pathOrExtension);

            ext = ext?.ToLowerInvariant();

            return ext switch
            {
                ".pdf" => "application/pdf",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".tif" or ".tiff" => "image/tiff",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }
    }
}
