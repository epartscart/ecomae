namespace EcomAE.Platform.Storage;

/// <summary>
/// Object storage scaffolding options (Azure Blob / S3 / MinIO).
/// Not bound in <c>Program.cs</c>; CloudPanel local/env paths remain current backup/file path.
/// </summary>
public sealed class EcomAeObjectStorageScaffoldOptions
{
    public const string SectionName = "EcomAe:ObjectStorage";

    public string Provider { get; set; } = "minio";

    public string Endpoint { get; set; } = string.Empty;

    public string Bucket { get; set; } = "ecomae";

    public bool Enabled { get; set; }

    /// <summary>Always false in scaffolding — do not move production backups/files yet.</summary>
    public bool ReplaceLocalFilePaths { get; set; }
}
