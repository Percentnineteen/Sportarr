using Sportarr.Api.Helpers;
using FluentAssertions;

namespace Sportarr.Api.Tests.Services;

/// <summary>
/// Coverage for release-sample detection. A field report showed 47 MB sample
/// clips being hardlinked into the library as the real 1080p session (the
/// importer takes the largest loose video, and the sample was the only one when
/// the real file was still in archives). These tests pin the cases that must be
/// excluded and, just as importantly, guard the false positives ("resample",
/// "example", and a download root that merely contains the word "sample").
/// </summary>
public class SampleFileFilterTests
{
    [Theory]
    // Sample subfolder (the reported shape).
    [InlineData("/dl/F1.Practice.Two/Sample/f1-practice-two-sample.mkv")]
    [InlineData("/dl/Race/Sample/whatever.mkv")]
    // "sample" token in the filename, in every common separator position.
    [InlineData("/dl/release/release-sample.mkv")]
    [InlineData("/dl/release/release.sample.mkv")]
    [InlineData("/dl/release/sample-release.mkv")]
    [InlineData("/dl/release/sample.mkv")]
    [InlineData("/dl/release/Movie.Name.Sample.1080p.mkv")]
    [InlineData("/dl/release/Movie Name sample.mkv")]
    public void IsSample_FlagsSamples(string path)
    {
        SampleFileFilter.IsSample(path).Should().BeTrue();
    }

    [Theory]
    [InlineData("/dl/F1/Formula.1.S2026E33.Canada.Race.1080p.WEB.x264-GROUP.mkv")]
    [InlineData("/dl/release/resample.mkv")]       // "re" prefix is not a boundary
    [InlineData("/dl/release/example.mkv")]        // contains "ample", not "sample"
    [InlineData("/dl/release/Resampled.Audio.mkv")]
    public void IsSample_DoesNotFlagRealOrLookalikes(string path)
    {
        SampleFileFilter.IsSample(path).Should().BeFalse();
    }

    [Fact]
    public void IsSample_BasePath_IgnoresSampleInTheRootItself()
    {
        // The download/scan root contains "sample" but the file below it does
        // not, so it must NOT be flagged.
        SampleFileFilter
            .IsSample("/data/old-samples/F1/session.mkv", "/data/old-samples")
            .Should().BeFalse();
    }

    [Fact]
    public void IsSample_BasePath_StillFlagsSampleBelowTheRoot()
    {
        SampleFileFilter
            .IsSample("/data/old-samples/F1/Sample/clip.mkv", "/data/old-samples")
            .Should().BeTrue();
    }

    [Fact]
    public void FilterSamples_DropsSamplesKeepsRealFiles()
    {
        var input = new[]
        {
            "/dl/F1/Race.1080p.mkv",
            "/dl/F1/Sample/race-sample.mkv",
            "/dl/F1/Qualifying.1080p.mkv",
        };

        SampleFileFilter.FilterSamples(input)
            .Should()
            .BeEquivalentTo("/dl/F1/Race.1080p.mkv", "/dl/F1/Qualifying.1080p.mkv");
    }
}
