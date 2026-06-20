using Sportarr.Api.Services;
using FluentAssertions;

namespace Sportarr.Api.Tests.Services;

/// <summary>
/// Coverage for NZBGet history status mapping. A user hit an import loop where the
/// same F1 event was re-grabbed repeatedly; NZBGet refused each duplicate with a
/// DELETED/COPY history status, but Sportarr's old mapping defaulted everything
/// that wasn't an exact "success"/"failure" string to "completed" and then tried
/// to import the unfinished intermediate directory (/incomplete/<name>.#<id>)
/// over and over. These tests pin the category-based mapping.
/// </summary>
public class NzbGetClientTests
{
    [Theory]
    [InlineData("SUCCESS/ALL")]
    [InlineData("SUCCESS/UNPACK")]
    [InlineData("SUCCESS/PAR")]
    [InlineData("SUCCESS/HEALTH")]
    [InlineData("SUCCESS/GOOD")]
    [InlineData("success")]
    public void MapHistoryStatus_Success_IsCompleted(string raw)
    {
        NzbGetClient.MapHistoryStatus(raw).Should().Be("completed");
    }

    [Theory]
    [InlineData("FAILURE/PAR")]
    [InlineData("FAILURE/UNPACK")]
    [InlineData("FAILURE/MOVE")]
    [InlineData("FAILURE/HEALTH")]
    [InlineData("FAILURE/BAD")]
    public void MapHistoryStatus_Failure_IsFailed(string raw)
    {
        NzbGetClient.MapHistoryStatus(raw).Should().Be("failed");
    }

    [Theory]
    [InlineData("DELETED/COPY")]   // the user's case: NZBGet already had an identical download
    [InlineData("DELETED/DUPE")]
    [InlineData("DELETED/MANUAL")]
    [InlineData("DELETED/HEALTH")]
    [InlineData("DELETED/GOOD")]
    public void MapHistoryStatus_Deleted_IsNullGone(string raw)
    {
        NzbGetClient.MapHistoryStatus(raw).Should().BeNull(
            because: "DELETED/* downloads were removed or refused and have no finished files to import");
    }

    [Theory]
    [InlineData("WARNING/HEALTH")]
    [InlineData("WARNING/SPACE")]
    [InlineData("WARNING/SCRIPT")]
    public void MapHistoryStatus_Warning_IsCompleted(string raw)
    {
        // WARNING items finished with non-fatal warnings; files are present, let the
        // importer decide.
        NzbGetClient.MapHistoryStatus(raw).Should().Be("completed");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("SOMETHING_UNEXPECTED")]
    public void MapHistoryStatus_UnknownOrEmpty_DefaultsToCompleted(string? raw)
    {
        NzbGetClient.MapHistoryStatus(raw).Should().Be("completed");
    }
}
