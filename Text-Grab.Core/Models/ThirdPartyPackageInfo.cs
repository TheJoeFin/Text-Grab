namespace Text_Grab.Models;

public sealed record ThirdPartyPackageInfo(
    string PackageId,
    string Version,
    string Scope,
    string License,
    string ProjectUrl,
    string NoticeTarget,
    bool NoticeIsLocal = false,
    string Notes = "")
{
    public string DisplayNotes => string.IsNullOrWhiteSpace(Notes) ? "\u2014" : Notes;
}
