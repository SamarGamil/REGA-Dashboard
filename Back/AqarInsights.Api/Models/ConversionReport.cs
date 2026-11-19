namespace AqarInsights.Api.Models;

public class ConversionReport
{
    public bool Success { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int SkippedRows { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<ConversionWarning> Warnings { get; set; } = new();
    public List<SkippedRowDetail> SkippedDetails { get; set; } = new();
    public List<string> FilesGenerated { get; set; } = new();
}

public class SkippedRowDetail
{
    public string Sheet { get; set; } = "";
    public int Row { get; set; }
    public string Reason { get; set; } = "";
    public string? RawData { get; set; }
}

public class ConversionWarning
{
    public string Sheet { get; set; } = "";
    public int Row { get; set; }
    public string Issue { get; set; } = "";
}
