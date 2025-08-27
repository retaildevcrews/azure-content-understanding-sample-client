using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ContentUnderstanding.Client.Models;
using ContentUnderstanding.Client.Utilities;

namespace ContentUnderstanding.Client.Services
{
    internal class BatchSummaryWriter
    {
        public async Task<string?> ExportAsync(string directoryName, string classifierName, List<BatchSummaryRow> rows)
        {
            try
            {
                var outputDir = PathResolver.OutputDir();
                Directory.CreateDirectory(outputDir);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var baseName = $"batch_{Sanitize(directoryName)}_{Sanitize(classifierName)}_{timestamp}";
                var jsonPath = Path.Combine(outputDir, baseName + "_summary.json");

                var json = JsonSerializer.Serialize(rows, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(jsonPath, json);
                return jsonPath;
            }
            catch
            {
                return null;
            }
        }

        private static string Sanitize(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
            return new string(chars);
        }
    }
}
