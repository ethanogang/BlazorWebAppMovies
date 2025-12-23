namespace MaCaveServeur.Services;

public record SheetImportInfo(string Sheet, string? SiteCode, int Added);

public class ImportWorkbookResult
{
    public int TotalAdded { get; set; }
    public List<SheetImportInfo> Sheets { get; set; } = new();
}
