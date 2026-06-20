using System.Text.RegularExpressions;

namespace Sportarr.Api.Helpers;

/// <summary>
/// Detects release "sample" clips — short, low-bitrate preview files that ship
/// alongside the real content, usually in a "Sample" subfolder and/or named
/// with a "sample" token. These must never be imported as the actual event.
///
/// The importer picks the largest loose video in a download, so when the real
/// file is still inside un-extracted archives the sample is the only candidate
/// and gets hardlinked into the library as a tens-of-MB "episode" (a real
/// session is multiple GB). Excluding samples up front means the importer finds
/// no usable video and reports the download as not-yet-importable instead of
/// silently linking the wrong file.
///
/// A path is treated as a sample when the word "sample" appears as a whole
/// token in any folder segment or in the filename — bounded by a separator
/// (slash, dot, dash, space, underscore) or the start/end of the segment, so
/// "resample" and "example" are not matched.
/// </summary>
public static class SampleFileFilter
{
    private static readonly Regex SampleToken = new(
        @"(^|[\\/.\- _])sample([\\/.\- _]|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// True when the path looks like a release sample. Pass <paramref name="basePath"/>
    /// (the download/scan root) so only the portion of the path below it is
    /// inspected — that prevents a parent folder that merely contains the word
    /// "sample" (e.g. a download base like /data/old-samples) from flagging
    /// every file beneath it.
    /// </summary>
    public static bool IsSample(string? path, string? basePath = null)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        var candidate = path;

        if (!string.IsNullOrEmpty(basePath))
        {
            try
            {
                var relative = Path.GetRelativePath(basePath, path);
                candidate = !string.IsNullOrEmpty(relative)
                            && relative != "."
                            && !relative.StartsWith("..")
                    ? relative
                    : Path.GetFileName(path);
            }
            catch
            {
                candidate = Path.GetFileName(path);
            }
        }

        return SampleToken.IsMatch(candidate);
    }

    /// <summary>Drop sample files from an enumeration of discovered paths.</summary>
    public static IEnumerable<string> FilterSamples(IEnumerable<string> paths, string? basePath = null)
        => paths.Where(p => !IsSample(p, basePath));
}
