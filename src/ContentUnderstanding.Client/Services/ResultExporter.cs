using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ContentUnderstanding.Client.Utilities;

namespace ContentUnderstanding.Client.Services
{
    internal class ResultExporter
    {
        public async Task<(string jsonPath, string formattedPath)> ExportAsync(
            JsonDocument analysisResult,
            string outputDir,
            string? documentName = null,
            string? operationId = null,
            CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(outputDir);

            // Build base filename from inputs
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var safeDoc = string.IsNullOrWhiteSpace(documentName)
                ? "operation"
                : SanitizeFileToken(Path.GetFileNameWithoutExtension(documentName));
            string? safeOperationId = null;
            if (!string.IsNullOrWhiteSpace(operationId))
            {
                var cleanOperationId = operationId.Split('?')[0];
                safeOperationId = cleanOperationId.Contains('_') ? cleanOperationId.Split('_')[0] : cleanOperationId;
                safeOperationId = SanitizeFileToken(safeOperationId);
            }
            var baseFileName = !string.IsNullOrWhiteSpace(safeOperationId)
                ? $"{safeDoc}_{safeOperationId}_{timestamp}"
                : $"{safeDoc}_{timestamp}";

            var jsonPath = Path.Combine(outputDir, $"{baseFileName}_results.json");
            var formattedPath = Path.Combine(outputDir, $"{baseFileName}_formatted.txt");

            // Write raw JSON (pretty)
            var prettyJson = JsonSerializer.Serialize(analysisResult.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(jsonPath, prettyJson, Encoding.UTF8, cancellationToken);

            // Create formatted text view
            var formattedText = CreateFormattedResults(analysisResult, documentName, operationId);
            await File.WriteAllTextAsync(formattedPath, formattedText, Encoding.UTF8, cancellationToken);

            return (jsonPath, formattedPath);
        }

        private static string SanitizeFileToken(string token)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
            {
                token = token.Replace(c, '-');
            }
            return token;
        }

        public string CreateFormattedResults(JsonDocument analysisResult, string? documentName = null, string? operationId = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Azure Content Understanding - Analysis Results");
            sb.AppendLine(new string('=', 55));
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(documentName))
            {
                sb.AppendLine($"Document: {documentName}");
            }
            if (!string.IsNullOrWhiteSpace(operationId))
            {
                sb.AppendLine($"Operation ID: {operationId}");
            }
            if (!string.IsNullOrWhiteSpace(documentName) || !string.IsNullOrWhiteSpace(operationId))
            {
                sb.AppendLine($"Processed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
            }
            sb.AppendLine("EXTRACTED FIELDS:");
            sb.AppendLine(new string('-', 50));
            sb.AppendLine();

            try
            {
                var root = analysisResult.RootElement;
                if (root.TryGetProperty("result", out var result) &&
                    result.TryGetProperty("contents", out var contents) &&
                    contents.ValueKind == JsonValueKind.Array && contents.GetArrayLength() > 0)
                {
                    var firstContent = contents[0];
                    if (firstContent.TryGetProperty("fields", out var fields))
                    {
                        foreach (var field in fields.EnumerateObject())
                        {
                            var value = FieldExtractor.ExtractFieldValue(field.Value);
                            sb.AppendLine($"• {field.Name}:");
                            sb.AppendLine($"  {value}");
                            sb.AppendLine();
                        }
                    }
                    else
                    {
                        sb.AppendLine("No specific fields were extracted from this document.");
                        sb.AppendLine("This might be normal depending on the analyzer schema.");
                        sb.AppendLine();
                    }
                }
                else
                {
                    sb.AppendLine("No analysis results found in the response.");
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"Error creating formatted results: {ex.Message}");
                sb.AppendLine();
            }

            sb.AppendLine("RAW JSON RESPONSE:");
            sb.AppendLine(new string('-', 50));
            try
            {
                var pretty = JsonSerializer.Serialize(analysisResult.RootElement, new JsonSerializerOptions { WriteIndented = true });
                sb.AppendLine(pretty);
            }
            catch
            {
                sb.AppendLine(analysisResult.RootElement.ToString());
            }

            return sb.ToString();
        }
    }
}
