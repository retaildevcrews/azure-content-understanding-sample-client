using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
            // Use HTML for the human-readable formatted output
            var formattedPath = Path.Combine(outputDir, $"{baseFileName}_formatted.html");

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
            // Produce an HTML document with tables for array/object fields.
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">\n<head>");
            sb.AppendLine("  <meta charset=\"utf-8\" />");
            sb.AppendLine("  <title>Azure Content Understanding - Analysis Results</title>");
            sb.AppendLine("  <style>");
            sb.AppendLine("body{font-family:'Segoe UI',Arial,sans-serif;margin:1.5rem;line-height:1.45;}h1{font-size:1.4rem;margin:.2rem 0 0.6rem;}h2{margin-top:2rem;font-size:1.2rem;}table{border-collapse:collapse;width:100%;margin:.75rem 0 1.25rem;}th,td{border:1px solid #ccc;padding:.4rem .55rem;vertical-align:top;font-size:.85rem;}th{background:#f5f5f5;text-align:left;}tr:nth-child(even){background:#fafafa;}code{background:#f2f2f2;padding:2px 4px;border-radius:3px;}footer{margin-top:3rem;color:#777;font-size:.7rem;} .field{margin-bottom:1rem;} .field-name{font-weight:600;margin-bottom:.2rem;} .empty{color:#777;font-style:italic;} .pill{display:inline-block;background:#eef;border:1px solid #ccd;padding:2px 6px;margin:2px;border-radius:12px;font-size:.7rem;} ");
            sb.AppendLine("  </style>");
            sb.AppendLine("</head><body>");
            sb.AppendLine("<h1>Azure Content Understanding - Analysis Results</h1>");
            sb.AppendLine("<div class=\"meta\">");
            if (!string.IsNullOrWhiteSpace(documentName)) sb.AppendLine($"<div><strong>Document:</strong> {System.Net.WebUtility.HtmlEncode(documentName)}</div>");
            if (!string.IsNullOrWhiteSpace(operationId)) sb.AppendLine($"<div><strong>Operation ID:</strong> {System.Net.WebUtility.HtmlEncode(operationId)}</div>");
            sb.AppendLine($"<div><strong>Processed:</strong> {DateTime.Now:yyyy-MM-dd HH:mm:ss}</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("<h2>Extracted Fields</h2>");
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
                            RenderFieldHtml(sb, field.Name, field.Value);
                        }
                    }
                    else
                    {
                        sb.AppendLine("<p class=\"empty\">No specific fields were extracted from this document. This may be normal depending on the analyzer schema.</p>");
                    }
                }
                else
                {
                    sb.AppendLine("<p class=\"empty\">No analysis results found in the response.</p>");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"<p class=\"empty\">Error creating formatted results: {System.Net.WebUtility.HtmlEncode(ex.Message)}</p>");
            }
            sb.AppendLine("<footer>Formatted HTML excludes raw JSON (available separately in *_results.json).</footer>");
            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        private static void RenderFieldHtml(StringBuilder sb, string fieldName, JsonElement fieldValue)
        {
            var type = fieldValue.TryGetProperty("type", out var tEl) ? tEl.GetString()?.ToLowerInvariant() : null;
            if (type == "array" && fieldValue.TryGetProperty("valueArray", out var arrayEl))
            {
                RenderArrayField(sb, fieldName, arrayEl);
                return;
            }
            if (type == "object" && fieldValue.TryGetProperty("valueObject", out var objEl))
            {
                sb.AppendLine("<div class=\"field\">");
                sb.AppendLine($"<div class=\"field-name\">{System.Net.WebUtility.HtmlEncode(fieldName)}</div>");
                sb.AppendLine("<table><tbody>");
                foreach (var prop in objEl.EnumerateObject())
                {
                    var val = FieldExtractor.ExtractFieldValue(prop.Value);
                    sb.AppendLine("<tr>");
                    sb.AppendLine($"<th style=\"width:25%\">{System.Net.WebUtility.HtmlEncode(prop.Name)}</th>");
                    sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(val)}</td>");
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</tbody></table></div>");
                return;
            }

            var valueStr = FieldExtractor.ExtractFieldValue(fieldValue);
            sb.AppendLine("<div class=\"field\">");
            sb.AppendLine($"<div class=\"field-name\">{System.Net.WebUtility.HtmlEncode(fieldName)}</div>");
            sb.AppendLine($"<div class=\"field-value\">{System.Net.WebUtility.HtmlEncode(valueStr)}</div>");
            sb.AppendLine("</div>");
        }

        private static void RenderArrayField(StringBuilder sb, string fieldName, JsonElement arrayEl)
        {
            sb.AppendLine("<div class=\"field\">");
            sb.AppendLine($"<div class=\"field-name\">{System.Net.WebUtility.HtmlEncode(fieldName)}</div>");
            var items = arrayEl.EnumerateArray().ToList();
            if (items.Count == 0)
            {
                sb.AppendLine("<div class=\"empty\">(Empty array)</div></div>");
                return;
            }
            var firstObj = items.FirstOrDefault(i => i.TryGetProperty("type", out var t) && t.GetString() == "object" && i.TryGetProperty("valueObject", out _));
            if (firstObj.ValueKind != JsonValueKind.Undefined && firstObj.TryGetProperty("valueObject", out var firstValueObject))
            {
                var headers = new List<string>();
                foreach (var p in firstValueObject.EnumerateObject()) headers.Add(p.Name);
                sb.AppendLine("<table><thead><tr>");
                foreach (var h in headers) sb.AppendLine($"<th>{System.Net.WebUtility.HtmlEncode(h)}</th>");
                sb.AppendLine("</tr></thead><tbody>");
                foreach (var item in items)
                {
                    if (item.TryGetProperty("valueObject", out var valueObject))
                    {
                        sb.AppendLine("<tr>");
                        foreach (var h in headers)
                        {
                            if (valueObject.TryGetProperty(h, out var cell))
                            {
                                var cellStr = FieldExtractor.ExtractFieldValue(cell);
                                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(cellStr)}</td>");
                            }
                            else sb.AppendLine("<td></td>");
                        }
                        sb.AppendLine("</tr>");
                    }
                }
                sb.AppendLine("</tbody></table></div>");
                return;
            }
            // Primitive array -> single column table
            sb.AppendLine("<table><thead><tr><th>Value</th></tr></thead><tbody>");
            foreach (var item in items)
            {
                var v = FieldExtractor.ExtractFieldValue(item);
                sb.AppendLine($"<tr><td>{System.Net.WebUtility.HtmlEncode(v)}</td></tr>");
            }
            sb.AppendLine("</tbody></table></div>");
        }

    // (HtmlEncode helper removed – using System.Net.WebUtility.HtmlEncode directly.)
    }
}
