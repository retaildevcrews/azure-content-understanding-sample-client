namespace ContentUnderstanding.Client.Models
{
    internal record BatchSummaryRow
    {
        public string File { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;
        public string? OperationId { get; init; }
        public string? JsonPath { get; init; }
        public string? TextPath { get; init; }
        public long DurationMs { get; init; }
        public string? Error { get; init; }
    }
}
