using System.Globalization;
using EcomAE.Platform.Erp;
using Microsoft.AspNetCore.Http;

namespace EcomAE.Platform.Cp;

/// <summary>One entry in a <c>content/files</c> directory listing.</summary>
public sealed record CpFileEntry(string Name, string RelativePath, bool IsDirectory, long SizeBytes, DateTimeOffset Modified, string Extension, bool IsImage);

/// <summary>Directory listing rooted at <c>content/files</c>.</summary>
public sealed record CpFileListing(string RelativePath, IReadOnlyList<string> Crumbs, IReadOnlyList<CpFileEntry> Entries, bool RootExists, string RootPath);

/// <summary>
/// Native twin of the PHP CP file manager (<c>cp/content/filemanager/filemanager.php</c>, elFinder over
/// <c>/content/files/</c>). Same rules as the PHP connector config: root is <c>content/files</c>, dot files are hidden and
/// locked, uploads limited to JPEG/PNG/GIF and PDF. Runs without the PHP connector.
/// </summary>
public interface ICpFileManagerService
{
    CpFileListing List(string? relativePath);

    Task<ErpSimpleWriteResult> UploadAsync(string? relativePath, IFormFile file, CancellationToken cancellationToken = default);

    ErpSimpleWriteResult CreateDirectory(string? relativePath, string? name);

    ErpSimpleWriteResult Rename(string? relativePath, string? name, string? newName);

    ErpSimpleWriteResult Delete(string? relativePath, IReadOnlyList<string> names);
}

public sealed class CpFileManagerService : ICpFileManagerService
{
    public const string RootUrl = "/content/files";
    public const long MaxUploadBytes = 16 * 1024 * 1024;
    public static readonly IReadOnlyList<string> UploadExtensions = ["jpg", "jpeg", "png", "gif", "pdf"];
    private static readonly string[] ImageExtensions = ["jpg", "jpeg", "png", "gif", "webp", "svg"];

    private readonly string _root;

    public CpFileManagerService(Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        : this(Path.Combine(Presentation.PhpLegacyAssetBridge.FindRepoRoot(env), "content", "files"), true)
    {
    }

    private CpFileManagerService(string root, bool _)
    {
        _root = Path.GetFullPath(root);
    }

    /// <summary>Service bound to an explicit root directory (tests / tooling).</summary>
    public static CpFileManagerService ForRoot(string root) => new(root, true);

    public string Root => _root;

    /// <summary>Normalizes a URL-style relative path; null when it escapes the root or touches a dot segment.</summary>
    public static string? NormalizeRelative(string? relativePath)
    {
        var raw = (relativePath ?? string.Empty).Replace('\\', '/').Trim();
        var parts = new List<string>();
        foreach (var seg in raw.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (seg == "." )
            {
                continue;
            }

            if (seg == ".." || seg.StartsWith('.') || seg.Contains('\0'))
            {
                return null;
            }

            parts.Add(seg);
        }

        return string.Join('/', parts);
    }

    /// <summary>elFinder-style name check: no separators, no dot-prefix, no control chars, ≤ 255 chars.</summary>
    public static bool IsValidName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255)
        {
            return false;
        }

        if (name.StartsWith('.') || name is "." or "..")
        {
            return false;
        }

        return name.IndexOfAny(['/', '\\', '\0', ':', '*', '?', '"', '<', '>', '|']) < 0;
    }

    public static bool IsAllowedUpload(string? fileName)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).TrimStart('.').ToLowerInvariant();
        return UploadExtensions.Contains(ext, StringComparer.Ordinal);
    }

    public static string HumanSize(long bytes)
    {
        if (bytes < 1024)
        {
            return bytes.ToString(CultureInfo.InvariantCulture) + " B";
        }

        var kb = bytes / 1024d;
        if (kb < 1024)
        {
            return kb.ToString("0.#", CultureInfo.InvariantCulture) + " KB";
        }

        var mb = kb / 1024d;
        if (mb < 1024)
        {
            return mb.ToString("0.#", CultureInfo.InvariantCulture) + " MB";
        }

        return (mb / 1024d).ToString("0.##", CultureInfo.InvariantCulture) + " GB";
    }

    private string? Resolve(string? relativePath)
    {
        var rel = NormalizeRelative(relativePath);
        if (rel is null)
        {
            return null;
        }

        var full = Path.GetFullPath(Path.Combine(_root, rel.Replace('/', Path.DirectorySeparatorChar)));
        if (full != _root && !full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        return full;
    }

    public CpFileListing List(string? relativePath)
    {
        var rel = NormalizeRelative(relativePath) ?? string.Empty;
        var dir = Resolve(rel);
        if (dir is null || !Directory.Exists(dir))
        {
            rel = string.Empty;
            dir = _root;
        }

        var crumbs = rel.Length == 0 ? [] : rel.Split('/');
        if (!Directory.Exists(_root))
        {
            return new CpFileListing(rel, crumbs, [], false, _root);
        }

        var entries = new List<CpFileEntry>();
        foreach (var path in Directory.EnumerateFileSystemEntries(dir))
        {
            var name = Path.GetFileName(path);
            if (name.Length == 0 || name.StartsWith('.'))
            {
                continue;
            }

            var isDir = Directory.Exists(path);
            long size = 0;
            DateTimeOffset modified;
            try
            {
                if (isDir)
                {
                    modified = Directory.GetLastWriteTimeUtc(path);
                }
                else
                {
                    var fi = new FileInfo(path);
                    size = fi.Length;
                    modified = fi.LastWriteTimeUtc;
                }
            }
            catch (IOException)
            {
                modified = DateTimeOffset.MinValue;
            }
            catch (UnauthorizedAccessException)
            {
                modified = DateTimeOffset.MinValue;
            }

            var ext = isDir ? string.Empty : Path.GetExtension(name).TrimStart('.').ToLowerInvariant();
            var childRel = rel.Length == 0 ? name : rel + "/" + name;
            entries.Add(new CpFileEntry(name, childRel, isDir, size, modified, ext, !isDir && ImageExtensions.Contains(ext, StringComparer.Ordinal)));
        }

        entries.Sort((a, b) =>
        {
            if (a.IsDirectory != b.IsDirectory)
            {
                return a.IsDirectory ? -1 : 1;
            }

            return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return new CpFileListing(rel, crumbs, entries, true, _root);
    }

    public async Task<ErpSimpleWriteResult> UploadAsync(string? relativePath, IFormFile file, CancellationToken cancellationToken = default)
    {
        var dir = Resolve(relativePath);
        if (dir is null || !Directory.Exists(dir))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Target folder not found.");
        }

        var name = Path.GetFileName(file.FileName ?? string.Empty);
        if (!IsValidName(name))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid file name.");
        }

        if (!IsAllowedUpload(name))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Upload images (JPEG, PNG, GIF) and PDFs only.");
        }

        if (file.Length <= 0 || file.Length > MaxUploadBytes)
        {
            return ErpSimpleWriteResult.Fail("invalid", "File must be between 1 byte and 16 MB.");
        }

        var target = Path.Combine(dir, name);
        if (File.Exists(target) || Directory.Exists(target))
        {
            return ErpSimpleWriteResult.Fail("exists", "A file with this name already exists.");
        }

        try
        {
            await using var stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            return ErpSimpleWriteResult.Fail("io", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ErpSimpleWriteResult.Fail("io", ex.Message);
        }

        return ErpSimpleWriteResult.Ok("Uploaded " + name + ".", 0);
    }

    public ErpSimpleWriteResult CreateDirectory(string? relativePath, string? name)
    {
        var dir = Resolve(relativePath);
        if (dir is null || !Directory.Exists(dir))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Target folder not found.");
        }

        var clean = (name ?? string.Empty).Trim();
        if (!IsValidName(clean))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid folder name.");
        }

        var target = Path.Combine(dir, clean);
        if (Directory.Exists(target) || File.Exists(target))
        {
            return ErpSimpleWriteResult.Fail("exists", "An item with this name already exists.");
        }

        try
        {
            Directory.CreateDirectory(target);
        }
        catch (IOException ex)
        {
            return ErpSimpleWriteResult.Fail("io", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ErpSimpleWriteResult.Fail("io", ex.Message);
        }

        return ErpSimpleWriteResult.Ok("Folder " + clean + " created.", 0);
    }

    public ErpSimpleWriteResult Rename(string? relativePath, string? name, string? newName)
    {
        var dir = Resolve(relativePath);
        if (dir is null || !Directory.Exists(dir))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Target folder not found.");
        }

        var from = (name ?? string.Empty).Trim();
        var to = (newName ?? string.Empty).Trim();
        if (!IsValidName(from) || !IsValidName(to))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Invalid name.");
        }

        if (from == to)
        {
            return ErpSimpleWriteResult.Fail("noop", "Name unchanged.");
        }

        var source = Path.Combine(dir, from);
        var target = Path.Combine(dir, to);
        var isDir = Directory.Exists(source);
        if (!isDir && !File.Exists(source))
        {
            return ErpSimpleWriteResult.Fail("missing", "Item not found.");
        }

        if (!isDir && !IsAllowedUpload(to) && Path.GetExtension(to).Length > 0 &&
            !string.Equals(Path.GetExtension(from), Path.GetExtension(to), StringComparison.OrdinalIgnoreCase))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Files may only be renamed to JPEG, PNG, GIF or PDF extensions.");
        }

        if (Directory.Exists(target) || File.Exists(target))
        {
            return ErpSimpleWriteResult.Fail("exists", "An item with this name already exists.");
        }

        try
        {
            if (isDir)
            {
                Directory.Move(source, target);
            }
            else
            {
                File.Move(source, target);
            }
        }
        catch (IOException ex)
        {
            return ErpSimpleWriteResult.Fail("io", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return ErpSimpleWriteResult.Fail("io", ex.Message);
        }

        return ErpSimpleWriteResult.Ok("Renamed to " + to + ".", 0);
    }

    public ErpSimpleWriteResult Delete(string? relativePath, IReadOnlyList<string> names)
    {
        var dir = Resolve(relativePath);
        if (dir is null || !Directory.Exists(dir))
        {
            return ErpSimpleWriteResult.Fail("invalid", "Target folder not found.");
        }

        var removed = 0;
        foreach (var raw in names)
        {
            var name = raw.Trim();
            if (!IsValidName(name))
            {
                return ErpSimpleWriteResult.Fail("invalid", "Invalid name: " + raw);
            }

            var target = Path.Combine(dir, name);
            try
            {
                if (Directory.Exists(target))
                {
                    if (Directory.EnumerateFileSystemEntries(target).Any())
                    {
                        return ErpSimpleWriteResult.Fail("not_empty", "Folder " + name + " is not empty.");
                    }

                    Directory.Delete(target);
                    removed++;
                }
                else if (File.Exists(target))
                {
                    File.Delete(target);
                    removed++;
                }
            }
            catch (IOException ex)
            {
                return ErpSimpleWriteResult.Fail("io", ex.Message);
            }
            catch (UnauthorizedAccessException ex)
            {
                return ErpSimpleWriteResult.Fail("io", ex.Message);
            }
        }

        if (removed == 0)
        {
            return ErpSimpleWriteResult.Fail("missing", "Nothing to delete.");
        }

        return new ErpSimpleWriteResult(true, "ok", removed.ToString(CultureInfo.InvariantCulture) + " item(s) deleted.", 0, removed);
    }
}
