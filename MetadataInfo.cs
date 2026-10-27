namespace QuestMetadataEditor;

internal sealed class MetadataInfo
{
    public string PackageName { get; set; } = "Not detected";
    public string AppLabel { get; set; } = "Not detected";
    public string VersionName { get; set; } = "Not detected";
    public string VersionCode { get; set; } = "Not detected";
    public string MinSdk { get; set; } = "Not detected";
    public string TargetSdk { get; set; } = "Not detected";
    public List<string> Permissions { get; } = new();
    public string ManifestFormat { get; set; } = "Unknown";
    public long FileSize { get; set; }
    public int ArchiveEntryCount { get; set; }
    public string SourcePath { get; set; } = string.Empty;

    public bool HasUsefulData => PackageName != "Not detected" || AppLabel != "Not detected" ||
                                 VersionName != "Not detected" || VersionCode != "Not detected";
}
