using SharpCompress.Archives;
using SharpCompress.Readers;

namespace EcomAE.Platform.Cp.PriceImport;

/// <summary>
/// pyprices <c>file_receiver.extract_archive</c> + the post-extraction loop: unpacks ZIP/RAR/7Z/TAR into the task folder,
/// keeps only <c>txt/csv/xls/xlsx</c> entries whose file name contains <c>file_name_substring</c>, and reports each decision
/// with the same wording. Entry paths are flattened to their file name, so nothing is written outside the folder.
/// </summary>
public static class PriceArchiveExtractor
{
    /// <summary>Upper bound for the unpacked bytes of one archive (zip-bomb guard).</summary>
    public const long MaxUnpackedBytes = 2L * 1024 * 1024 * 1024;

    public static IReadOnlyList<string> Extract(string archivePath, string targetDirectory, string? fileNameSubstring, ICollection<string> messages)
    {
        Directory.CreateDirectory(targetDirectory);
        var kept = new List<string>();
        long unpacked = 0;
        IArchive archive;
        try
        {
            archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions());
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or ArgumentException
                                       or SharpCompress.Common.ArchiveException or NotSupportedException)
        {
            throw new PriceFileFormatException("Archive " + Path.GetFileName(archivePath) + " cannot be extracted: " + ex.Message);
        }

        using (archive)
        {
            foreach (var entry in archive.Entries)
            {
                if (entry.IsDirectory)
                {
                    continue;
                }

                var name = Path.GetFileName((entry.Key ?? string.Empty).Replace('\\', '/'));
                if (name.Length == 0)
                {
                    continue;
                }

                if (!PriceFileReader.IsSuitable(name))
                {
                    messages.Add("Extracted file [" + name + "] has wrong type. Accepted file types: " + string.Join(", ", PriceFileReader.SuitableExtensions));
                    continue;
                }

                if (!PriceFileNameFilter.Matches(name, fileNameSubstring))
                {
                    messages.Add("Extracted file [" + name + "] does not match the substring in the file name: " + fileNameSubstring);
                    continue;
                }

                unpacked += Math.Max(0, entry.Size);
                if (unpacked > MaxUnpackedBytes)
                {
                    throw new PriceFileFormatException("Archive " + Path.GetFileName(archivePath) + " unpacks to more than 2 GB; refused.");
                }

                var destination = UniquePath(targetDirectory, name);
                using (var output = File.Create(destination))
                using (var input = entry.OpenEntryStream())
                {
                    input.CopyTo(output);
                }

                kept.Add(destination);
                messages.Add("Extracted file [" + name + "] has SUCCESSFULLY passed the checks and remains for further processing");
            }
        }

        return kept;
    }

    private static string UniquePath(string directory, string name)
    {
        var candidate = Path.Combine(directory, name);
        var n = 1;
        while (File.Exists(candidate))
        {
            candidate = Path.Combine(directory, n.ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + name);
            n++;
        }

        return candidate;
    }
}

/// <summary>pyprices <c>is_file_suitable_by_substring</c>: case-sensitive <c>str.find</c>; an empty filter matches every file.</summary>
public static class PriceFileNameFilter
{
    public static bool Matches(string fileName, string? substring)
        => string.IsNullOrEmpty(substring) || fileName.Contains(substring, StringComparison.Ordinal);
}
