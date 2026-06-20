using Sportarr.Api.Services;
using Sportarr.Api.Models;
using FluentAssertions;

namespace Sportarr.Api.Tests.Services;

/// <summary>
/// Match-score regression coverage for cases reported in the field. Each test
/// pairs a real-world release-title shape with the event shape Sportarr stores
/// after syncing from TheSportsDB. A score of 0 means "Release doesn't match
/// event" and the user sees the result rejected; verifying these scores stay
/// non-zero protects against the user-reported regressions where the correct
/// release was being thrown away.
/// </summary>
public class ReleaseMatchScorerTests
{
    private readonly ReleaseMatchScorer _scorer = new();

    /// <summary>
    /// User-reported case: Anaheim Ducks vs Edmonton Oilers, NHL Stanley Cup
    /// Round 1 Game 6, played Apr 30 in venue-local (ET) but stored on May 1
    /// for a UK-timezone user. The release is correctly named with venue-local
    /// date "30.04.2026" and Round 1 / Game 6 markers. Both teams are in the
    /// release title. This MUST score above MinimumMatchScore.
    /// </summary>
    [Fact]
    public void NhlPlayoffGame_WithBroadcastDateSet_ScoresAboveMinimum()
    {
        var evt = new Event
        {
            Id = 1,
            Title = "Anaheim Ducks vs Edmonton Oilers",
            Sport = "Ice Hockey",
            EventDate = new DateTime(2026, 5, 1, 1, 0, 0, DateTimeKind.Utc),
            BroadcastDate = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc),
            HomeTeamName = "Anaheim Ducks",
            AwayTeamName = "Edmonton Oilers",
            Round = "125", // TheSportsDB encoding for playoff Round 1
            League = new League { Id = 1, Name = "NHL", Sport = "Ice Hockey" }
        };

        var releaseTitle = "NHL SC 2026 / Round 1 / Game 6 / 30.04.2026 / Edmonton Oilers @ Anaheim Ducks [Hockey, WEB-DL HD/1080p/60fps, MKV/H.264, EN/TNT]";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().BeGreaterThan(ReleaseMatchScorer.MinimumMatchScore,
            because: "release titled with both teams plus playoff round/game markers and a date one day off from the UTC event date should pass the matcher; any score-0 here means a real bug");
    }

    /// <summary>
    /// Variant: BroadcastDate is null (older sync that didn't populate it).
    /// Stored EventDate is May 1 UTC, release shows venue-local Apr 30 — the
    /// matcher's ±1-day date tolerance must handle this fall-back path.
    /// </summary>
    [Fact]
    public void NhlPlayoffGame_WithNullBroadcastDate_ScoresAboveMinimum()
    {
        var evt = new Event
        {
            Id = 1,
            Title = "Anaheim Ducks vs Edmonton Oilers",
            Sport = "Ice Hockey",
            EventDate = new DateTime(2026, 5, 1, 1, 0, 0, DateTimeKind.Utc),
            BroadcastDate = null,
            HomeTeamName = "Anaheim Ducks",
            AwayTeamName = "Edmonton Oilers",
            Round = "125",
            League = new League { Id = 1, Name = "NHL", Sport = "Ice Hockey" }
        };

        var releaseTitle = "NHL SC 2026 / Round 1 / Game 6 / 30.04.2026 / Edmonton Oilers @ Anaheim Ducks [Hockey, WEB-DL HD/1080p/60fps, MKV/H.264, EN/TNT]";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().BeGreaterThan(ReleaseMatchScorer.MinimumMatchScore,
            because: "the ±1-day date tolerance must catch the venue-local-vs-UTC mismatch even without an explicit BroadcastDate");
    }

    /// <summary>
    /// Negative case: a release for a different NHL playoff matchup (Tampa Bay
    /// vs Montreal, same round, same game number) MUST score below the
    /// auto-grab threshold. This is what the team-mismatch hard-reject is for.
    /// </summary>
    [Fact]
    public void NhlPlayoffGame_DifferentTeams_ScoresZero()
    {
        var evt = new Event
        {
            Id = 1,
            Title = "Anaheim Ducks vs Edmonton Oilers",
            Sport = "Ice Hockey",
            EventDate = new DateTime(2026, 5, 1, 1, 0, 0, DateTimeKind.Utc),
            BroadcastDate = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc),
            HomeTeamName = "Anaheim Ducks",
            AwayTeamName = "Edmonton Oilers",
            Round = "125",
            League = new League { Id = 1, Name = "NHL", Sport = "Ice Hockey" }
        };

        var releaseTitle = "NHL SC 2026 / Round 1 / Game 6 / 01.05.2026 / Tampa Bay Lightning @ Montreal Canadiens [Hockey, WEB-DL HD/1080p/60fps]";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().Be(0,
            because: "neither home nor away team appears in this release - it's a different game, must hard-reject");
    }

    /// <summary>
    /// Negative case: NBA release matched against an NHL event must also score 0.
    /// Cross-sport contamination would happen if the indexer returns broad results.
    /// </summary>
    [Fact]
    public void NbaRelease_AgainstNhlEvent_ScoresZero()
    {
        var evt = new Event
        {
            Id = 1,
            Title = "Anaheim Ducks vs Edmonton Oilers",
            Sport = "Ice Hockey",
            EventDate = new DateTime(2026, 5, 1, 1, 0, 0, DateTimeKind.Utc),
            BroadcastDate = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc),
            HomeTeamName = "Anaheim Ducks",
            AwayTeamName = "Edmonton Oilers",
            Round = "125",
            League = new League { Id = 1, Name = "NHL", Sport = "Ice Hockey" }
        };

        var releaseTitle = "NBA Playoffs 2026 / Round 1 / Game 7 / 02.05.2026 / Philadelphia 76ers @ Boston Celtics [Basketball, WEB-DL HD/1080p/60fps]";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().Be(0, because: "NBA release shouldn't match an NHL event");
    }

    /// <summary>
    /// User-reported case: a Porsche Supercup support-series release ("La Course"
    /// = the race) shares the F1 race weekend, circuit and date with the Monaco
    /// Grand Prix, so without a support-series guard it was grabbed as the F1
    /// race. Porsche Supercup is NOT Formula 1 and must score zero against an F1
    /// event.
    /// </summary>
    [Fact]
    public void PorscheSupercupRelease_AgainstF1Event_ScoresZero()
    {
        var evt = new Event
        {
            Id = 1,
            Title = "Monaco Grand Prix",
            Sport = "Motorsport",
            EventDate = new DateTime(2026, 6, 7, 13, 0, 0, DateTimeKind.Utc),
            League = new League { Id = 1, Name = "Formula 1", Sport = "Motorsport" }
        };

        var releaseTitle = "PorscheSupercup.La.Course.GP.Monaco.07.06.2026.VFF.1080p.WEBRip.AAC.2.0.x264-DN55";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().Be(0, because: "a Porsche Supercup support-series release must not match the F1 Monaco GP");
    }

    /// <summary>
    /// User-reported case: searching "UFC Freedom 250 Topuria vs Gaethje" the broad
    /// fallback query "UFC 2026" returned unrelated UFC content and Sportarr grabbed
    /// "UFC.Road.to.UFC.5.2026.EP02" - a different, episodic series that shares only
    /// the org and the year. It MUST score 0 (rejected), not ride org+year to the
    /// auto-grab threshold.
    /// </summary>
    [Fact]
    public void UfcEvent_RoadToUfcEpisode_ScoresZero()
    {
        var evt = new Event
        {
            Id = 1,
            Title = "UFC Freedom 250 Topuria vs Gaethje",
            Sport = "Fighting",
            EventDate = new DateTime(2026, 6, 15, 2, 0, 0, DateTimeKind.Utc),
            League = new League { Id = 1, Name = "UFC", Sport = "Fighting" }
        };

        var releaseTitle = "UFC.Road.to.UFC.5.2026.EP02.WEB-DL.H264.Fight-BB";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().Be(0, because: "Road to UFC 5 EP02 is a different series sharing only the org and year - it must not match UFC Freedom 250");
    }

    /// <summary>
    /// Negative case: a different week's numbered card from the same year must also
    /// be rejected. The event number is the strongest identity signal.
    /// </summary>
    [Fact]
    public void UfcEvent_DifferentNumberedCard_ScoresZero()
    {
        var evt = new Event
        {
            Id = 1,
            Title = "UFC Freedom 250 Topuria vs Gaethje",
            Sport = "Fighting",
            EventDate = new DateTime(2026, 6, 15, 2, 0, 0, DateTimeKind.Utc),
            League = new League { Id = 1, Name = "UFC", Sport = "Fighting" }
        };

        var releaseTitle = "UFC.318.Smith.vs.Jones.2026.1080p.WEB-DL.H264";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().Be(0, because: "UFC 318 is a different card from UFC (Freedom) 250 - wrong event number must hard-reject");
    }

    /// <summary>
    /// Positive case: the correct release for the same event, named by its number,
    /// must still match even though the event title carries an extra word ("Freedom")
    /// between "UFC" and the number.
    /// </summary>
    [Fact]
    public void UfcEvent_CorrectNumberedRelease_ScoresAboveMinimum()
    {
        var evt = new Event
        {
            Id = 1,
            Title = "UFC Freedom 250 Topuria vs Gaethje",
            Sport = "Fighting",
            EventDate = new DateTime(2026, 6, 15, 2, 0, 0, DateTimeKind.Utc),
            League = new League { Id = 1, Name = "UFC", Sport = "Fighting" }
        };

        var releaseTitle = "UFC.250.Topuria.vs.Gaethje.2026.1080p.WEB-DL.H264";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().BeGreaterThan(ReleaseMatchScorer.MinimumMatchScore,
            because: "the matching event number plus both fighters is the correct release and must pass");
    }

    /// <summary>
    /// Positive case: a fighter-named event without a number must still match a
    /// release named by its headliners (no number on either side).
    /// </summary>
    [Fact]
    public void UfcFightNight_HeadlinerNamedRelease_ScoresAboveMinimum()
    {
        var evt = new Event
        {
            Id = 1,
            Title = "UFC Fight Night Whittaker vs Aliskerov",
            Sport = "Fighting",
            EventDate = new DateTime(2026, 6, 22, 2, 0, 0, DateTimeKind.Utc),
            League = new League { Id = 1, Name = "UFC", Sport = "Fighting" }
        };

        var releaseTitle = "UFC.Fight.Night.Whittaker.vs.Aliskerov.2026.1080p.WEB-DL.H264";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().BeGreaterThan(ReleaseMatchScorer.MinimumMatchScore,
            because: "both headliner surnames appear in the release for a numberless Fight Night event");
    }

    /// <summary>
    /// User-reported case: the Spanish Grand Prix at Circuit de Barcelona-Catalunya.
    /// A release that (correctly) mentions "Spain" was hard-rejected with score 0
    /// because the location check matched Belgium's circuit alias "Spa" as a
    /// substring of "Spain". The Barcelona-Catalunya GP IS in Spain, so this must
    /// score above the minimum.
    /// </summary>
    [Fact]
    public void SpanishGp_WithSpainInTitle_ScoresAboveMinimum()
    {
        var evt = new Event
        {
            Id = 1,
            Title = "Barcelona-Catalunya Grand Prix - Race",
            Sport = "Motorsport",
            EventDate = new DateTime(2026, 6, 14, 13, 0, 0, DateTimeKind.Utc),
            League = new League { Id = 1, Name = "Formula 1", Sport = "Motorsport" }
        };

        var releaseTitle = "Formula 1 - S2026E41 - Barcelona-Catalunya Grand Prix - Race - 2026x07.Spain.SkyF1HD.SD-smcgill1969";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().BeGreaterThan(ReleaseMatchScorer.MinimumMatchScore,
            because: "the Barcelona-Catalunya GP is in Spain - 'Spain' must not be mistaken for Belgium's 'Spa' circuit");
    }

    /// <summary>
    /// Same bug via the demonym "Spanish" (contains the "Spa" substring too).
    /// </summary>
    [Fact]
    public void SpanishGp_WithSpanishInTitle_ScoresAboveMinimum()
    {
        var evt = new Event
        {
            Id = 1,
            Title = "Barcelona-Catalunya Grand Prix - Race",
            Sport = "Motorsport",
            EventDate = new DateTime(2026, 6, 14, 13, 0, 0, DateTimeKind.Utc),
            League = new League { Id = 1, Name = "Formula 1", Sport = "Motorsport" }
        };

        var releaseTitle = "Formula 1 - S2026E41 - Barcelona-Catalunya Grand Prix - Race - Spanish Sky 1080p50fps-SportVideo";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().BeGreaterThan(ReleaseMatchScorer.MinimumMatchScore,
            because: "'Spanish' also contains the 'Spa' substring and must not trigger a Belgium location conflict");
    }

    /// <summary>
    /// Negative control: a genuinely different location (Monaco) for the same
    /// Barcelona-Catalunya event must STILL be rejected. This proves the word-
    /// boundary fix tightened the substring matching without disabling the real
    /// wrong-location hard-reject.
    /// </summary>
    [Fact]
    public void SpanishGp_DifferentLocationRelease_ScoresZero()
    {
        var evt = new Event
        {
            Id = 1,
            Title = "Barcelona-Catalunya Grand Prix - Race",
            Sport = "Motorsport",
            EventDate = new DateTime(2026, 6, 14, 13, 0, 0, DateTimeKind.Utc),
            League = new League { Id = 1, Name = "Formula 1", Sport = "Motorsport" }
        };

        var releaseTitle = "Formula 1 - S2026E38 - Monaco Grand Prix - Race - 2026x05.Monaco.SkyF1HD.1080p-smcgill1969";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().Be(0, because: "a Monaco release is the wrong race for the Barcelona-Catalunya GP and must hard-reject");
    }

    // ---- Same-country distinct-race matching (F1 revisits a country in a season) ----

    private static Event F1Event(string title, int year, string? venue = null, string? location = null) => new Event
    {
        Id = 1,
        Title = title,
        Sport = "Motorsport",
        Venue = venue,
        Location = location,
        EventDate = new DateTime(year, 6, 14, 13, 0, 0, DateTimeKind.Utc),
        League = new League { Id = 1, Name = "Formula 1", Sport = "Motorsport" }
    };

    // NOTE: the release titles below are synthetic fixtures built from sport, year,
    // circuit, and a generic source tag only. They intentionally carry no real
    // release-group or broadcaster names.

    /// <summary>
    /// User-reported case: a whole-league search grabbed a Miami release for the
    /// United States Grand Prix (Austin). Both are in the USA, so the country-level
    /// check treated them as the same place. The event's venue (Austin) identifies
    /// the real circuit, so a Miami release must not match.
    /// </summary>
    [Fact]
    public void F1MiamiRelease_AgainstUnitedStatesGp_ScoresZero()
    {
        var evt = F1Event("United States Grand Prix - Race", 2026,
            venue: "Circuit of the Americas", location: "Austin, USA");
        var releaseTitle = "Formula1.2026.USA.Miami.Race.1080p.WEB-DL";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().Be(0, because: "the event venue is Austin, so a Miami release is the wrong US race");
    }

    /// <summary>
    /// The actual United States GP release (named by its circuit, Austin/COTA) must
    /// still match the event whose venue is Austin.
    /// </summary>
    [Fact]
    public void F1AustinRelease_AgainstUnitedStatesGp_ScoresAboveMinimum()
    {
        var evt = F1Event("United States Grand Prix - Race", 2026,
            venue: "Circuit of the Americas", location: "Austin, USA");
        var releaseTitle = "Formula1.2026.USA.Austin.Race.1080p.WEB-DL";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().BeGreaterThan(ReleaseMatchScorer.MinimumMatchScore,
            because: "Austin matches the event's Austin venue");
    }

    /// <summary>
    /// User-reported case: a Barcelona release grabbed for the (Madrid) Spanish GP.
    /// In 2026 the Spanish GP venue is Madrid, so a Barcelona release must not match.
    /// </summary>
    [Fact]
    public void F1BarcelonaRelease_Against2026MadridSpanishGp_ScoresZero()
    {
        var evt = F1Event("Spanish Grand Prix - Race", 2026,
            venue: "Madring", location: "Madrid, Spain");
        var releaseTitle = "Formula1.2026.Spain.Barcelona.Race.1080p.WEB-DL";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().Be(0, because: "the 2026 Spanish GP venue is Madrid, so a Barcelona release is the wrong race");
    }

    /// <summary>
    /// HISTORICAL correctness: pre-2026 the Spanish GP was at Barcelona, so a
    /// Barcelona release MUST match an old Spanish GP whose venue is Barcelona. This
    /// is the case the previous hardcoded "Spanish -> Madrid" mapping broke; keying
    /// off the event's own venue gets it right for every season.
    /// </summary>
    [Fact]
    public void F1BarcelonaRelease_AgainstHistoricalSpanishGp_ScoresAboveMinimum()
    {
        var evt = F1Event("Spanish Grand Prix - Race", 2025,
            venue: "Circuit de Barcelona-Catalunya", location: "Barcelona, Spain");
        var releaseTitle = "Formula1.2025.Spain.Barcelona.Race.1080p.WEB-DL";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().BeGreaterThan(ReleaseMatchScorer.MinimumMatchScore,
            because: "pre-2026 the Spanish GP was at Barcelona, so the Barcelona release is correct");
    }

    /// <summary>
    /// The Madrid release IS the 2026 Spanish GP and must still match.
    /// </summary>
    [Fact]
    public void F1MadridRelease_AgainstSpanishGp_ScoresAboveMinimum()
    {
        var evt = F1Event("Spanish Grand Prix - Race", 2026,
            venue: "Madring", location: "Madrid, Spain");
        var releaseTitle = "Formula1.2026.Spain.Madrid.Race.1080p.WEB-DL";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().BeGreaterThan(ReleaseMatchScorer.MinimumMatchScore,
            because: "Madrid matches the 2026 Spanish GP venue");
    }

    /// <summary>
    /// A broad/demonym-named release ("Spanish GP", no city) is still permitted to
    /// match - it names no specific circuit, so it falls through to the country-level
    /// match instead of being rejected.
    /// </summary>
    [Fact]
    public void F1BroadSpanishRelease_AgainstSpanishGp_ScoresAboveMinimum()
    {
        var evt = F1Event("Spanish Grand Prix - Race", 2026,
            venue: "Madring", location: "Madrid, Spain");
        var releaseTitle = "Formula1.2026.Spanish.Grand.Prix.Race.1080p.WEB-DL";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().BeGreaterThan(ReleaseMatchScorer.MinimumMatchScore,
            because: "a release with no specific circuit must still match by country/title");
    }

    /// <summary>
    /// The Miami release must still match the Miami GP event.
    /// </summary>
    [Fact]
    public void F1MiamiRelease_AgainstMiamiGp_ScoresAboveMinimum()
    {
        var evt = F1Event("Miami Grand Prix - Race", 2026,
            venue: "Miami International Autodrome", location: "Miami Gardens, USA");
        var releaseTitle = "Formula1.2026.USA.Miami.Race.1080p.WEB-DL";

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().BeGreaterThan(ReleaseMatchScorer.MinimumMatchScore,
            because: "the Miami release is exactly the Miami GP");
    }

    /// <summary>
    /// A correctly-named Barcelona release must still match the Barcelona-Catalunya GP -
    /// the circuit check only rejects cross-circuit matches, never the correct one. Both
    /// the dotted and spaced naming shapes are covered, with and without a stored venue.
    /// </summary>
    [Theory]
    [InlineData("Formula1.2026.Spain.Barcelona.Race.2160p.WEB-DL", true)]
    [InlineData("Formula1 2026 Barcelona Grand Prix Race 2160p WEB-DL", true)]
    [InlineData("Formula1.2026.Spain.Barcelona.Race.2160p.WEB-DL", false)]
    [InlineData("Formula1 2026 Barcelona Grand Prix Race 2160p WEB-DL", false)]
    public void F1BarcelonaRelease_AgainstBarcelonaGp_ScoresAboveMinimum(string releaseTitle, bool withVenue)
    {
        var evt = withVenue
            ? F1Event("Barcelona-Catalunya Grand Prix - Race", 2026,
                venue: "Circuit de Barcelona-Catalunya", location: "Barcelona, Spain")
            : F1Event("Barcelona-Catalunya Grand Prix - Race", 2026);

        var score = _scorer.CalculateMatchScore(releaseTitle, evt);

        score.Should().BeGreaterThan(ReleaseMatchScorer.MinimumMatchScore,
            because: "a Barcelona release must match the Barcelona-Catalunya GP (venue is a bonus, not required)");
    }
}
