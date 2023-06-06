namespace Text_Grab.Models;

public record OcrDirectoryOptions
{
    public string Path { get; set; } = string.Empty;
    public bool IsRecursive { get; set; } = false;
    public bool WriteTxtFiles { get; set; } = false;
    public bool OutputFileNames { get; set; } = true;
    public bool OutputFooter { get; set; } = true;
    public bool OutputHeader { get; set; } = true;
}