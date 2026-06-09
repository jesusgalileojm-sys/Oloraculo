using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Oloraculo.Web.DAL;
using Oloraculo.Web.Helpers;
using Oloraculo.Web.Models;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Oloraculo.Web.Services
{
    public class AvailabilityNewsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _http;
        private readonly OloraculoDbContext _db;
        private readonly OloraculoConfig _config;

        private bool IsConfigured => !string.IsNullOrWhiteSpace(_config.OpenRouterApiKey);

        public AvailabilityNewsService(HttpClient http, OloraculoDbContext db, IOptions<OloraculoConfig> config)
        {
            _http = http;
            _db = db;
            _config = config.Value;
        }

        public async Task<AvailabilityRefreshReport> RefreshAsync(CancellationToken ct = default)
        {
            if (!IsConfigured)
                return new AvailabilityRefreshReport { IsConfigured = false, Notes = ["La clave de OpenRouter no está configurada."] };

            var notes = new List<string>();
            var errors = new List<string>();
            var fetched = 0;
            var skipped = 0;
            var saved = 0;

            foreach (var url in _config.AvailabilitySourceUrls.Where(u => !string.IsNullOrWhiteSpace(u)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var fetch = await FetchSourceAsync(url, ct);
                await UpsertSourceAsync(url, fetch, ct);

                if (!fetch.Success || string.IsNullOrWhiteSpace(fetch.Text))
                {
                    skipped++;
                    errors.Add(fetch.Error ?? $"No se pudo leer {url}.");
                    continue;
                }

                fetched++;
                try
                {
                    var json = await ClassifyAsync(fetch, ct);
                    var claims = ParseClaimsFromJson(json, fetch.Url, fetch.Publisher)
                        .Where(c => c.Status != AvailabilityClaimStatus.NotRelevant && c.Status != AvailabilityClaimStatus.Available)
                        .ToList();

                    await ReplaceClaimsForSourceAsync(fetch.Url, claims, ct);
                    saved += claims.Count;
                    notes.Add($"{fetch.Publisher ?? fetch.Url}: {claims.Count} reclamos de disponibilidad guardados.");
                }
                catch (Exception ex)
                {
                    errors.Add($"{url}: OpenRouter no devolvió datos parseables ({ex.Message}). Se conservan reclamos previos de esa fuente.");
                }
            }

            await RecomputePredictionFlagsAsync(ct);
            var contexts = await RefreshAllFixtureContextsAsync(ct);
            var affecting = await _db.AvailabilityClaims.CountAsync(c => c.AffectsPrediction, ct);

            return new AvailabilityRefreshReport
            {
                IsConfigured = true,
                SourcesFetched = fetched,
                SourcesSkipped = skipped,
                ClaimsSaved = saved,
                ClaimsAffectingPredictions = affecting,
                ContextRowsUpdated = contexts,
                Notes = notes,
                Errors = errors
            };
        }

        public async Task<AvailabilityRefreshReport> RefreshFixtureContextAsync(string fixtureId, CancellationToken ct = default)
        {
            var updated = await RefreshFixtureContextCountsAsync(fixtureId, [], ct);
            return new AvailabilityRefreshReport
            {
                IsConfigured = IsConfigured,
                ContextRowsUpdated = updated ? 1 : 0,
                ClaimsAffectingPredictions = await _db.AvailabilityClaims.CountAsync(c => c.AffectsPrediction, ct),
                Notes = updated ? ["Contexto de disponibilidad actualizado desde noticias."] : ["No se encontró el partido seleccionado."]
            };
        }

        public async Task<IReadOnlyList<AvailabilityClaim>> ClaimsForFixtureAsync(string fixtureId, CancellationToken ct = default)
        {
            var fixture = await _db.Fixtures.FindAsync([fixtureId], ct);
            if (fixture is null)
                return [];

            return await _db.AvailabilityClaims.AsNoTracking()
                .Where(c => c.TeamId == fixture.HomeTeamId || c.TeamId == fixture.AwayTeamId)
                .OrderByDescending(c => c.AffectsPrediction)
                .ThenBy(c => c.TeamName)
                .ThenBy(c => c.Player)
                .ThenBy(c => c.Status)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<AvailabilityClaim>> AffectingClaimsForTeamsAsync(IEnumerable<string> teamIds, CancellationToken ct = default)
        {
            var teams = teamIds.ToHashSet(StringComparer.Ordinal);
            if (teams.Count == 0)
                return [];

            return await _db.AvailabilityClaims.AsNoTracking()
                .Where(c => c.AffectsPrediction && teams.Contains(c.TeamId))
                .ToListAsync(ct);
        }

        public async Task<bool> RefreshFixtureContextCountsAsync(string fixtureId, IEnumerable<(string TeamId, string PlayerKey)> externalUnavailablePlayers, CancellationToken ct = default)
        {
            var fixture = await _db.Fixtures.FindAsync([fixtureId], ct);
            if (fixture is null)
                return false;

            var newsClaims = await AffectingClaimsForTeamsAsync([fixture.HomeTeamId, fixture.AwayTeamId], ct);
            var unavailable = new HashSet<string>(StringComparer.Ordinal);

            foreach (var player in externalUnavailablePlayers)
            {
                if (player.TeamId == fixture.HomeTeamId || player.TeamId == fixture.AwayTeamId)
                    unavailable.Add($"{player.TeamId}|{player.PlayerKey}");
            }

            foreach (var claim in newsClaims)
                unavailable.Add($"{claim.TeamId}|{claim.PlayerKey}");

            var homeUnavailable = unavailable.Count(k => k.StartsWith(fixture.HomeTeamId + "|", StringComparison.Ordinal));
            var awayUnavailable = unavailable.Count(k => k.StartsWith(fixture.AwayTeamId + "|", StringComparison.Ordinal));
            var context = await _db.FixtureContexts.FindAsync([fixtureId], ct);
            if (context is null)
            {
                context = new FixtureContext { FixtureId = fixtureId };
                _db.FixtureContexts.Add(context);
            }

            var newsHome = newsClaims.Count(c => c.TeamId == fixture.HomeTeamId);
            var newsAway = newsClaims.Count(c => c.TeamId == fixture.AwayTeamId);
            context.UnavailableHomePlayers = homeUnavailable;
            context.UnavailableAwayPlayers = awayUnavailable;
            context.HasAvailabilityNews = newsHome + newsAway > 0;
            context.Notes = AppendAvailabilityNote(context.Notes, newsHome, newsAway);
            context.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return true;
        }

        public static IReadOnlyList<AvailabilityClaim> ParseClaimsFromJson(string json, string sourceUrl, string? publisher = null)
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var array = root.ValueKind == JsonValueKind.Array
                ? root
                : root.TryGetProperty("claims", out var claimsElement) && claimsElement.ValueKind == JsonValueKind.Array
                    ? claimsElement
                    : throw new JsonException("Expected an array or an object with a claims array.");

            var claims = new List<AvailabilityClaim>();
            foreach (var item in array.EnumerateArray())
            {
                var player = GetString(item, "player");
                var team = GetString(item, "team");
                if (string.IsNullOrWhiteSpace(player) || string.IsNullOrWhiteSpace(team))
                    continue;

                var status = ParseEnum(GetString(item, "status"), AvailabilityClaimStatus.NotRelevant);
                var evidence = ParseEnum(GetString(item, "evidenceLevel"), AvailabilityEvidenceLevel.Unsupported);
                var quote = GetString(item, "supportingText");
                claims.Add(new AvailabilityClaim
                {
                    Player = player.Trim(),
                    PlayerKey = NormalizePlayerKey(player),
                    TeamName = TeamNameNormalizer.CanonicalName(team),
                    TeamId = TeamNameNormalizer.ToId(team),
                    Status = status,
                    Reason = GetString(item, "reason").Trim(),
                    Confidence = GetString(item, "confidence").Trim(),
                    EvidenceLevel = evidence,
                    SourceUrl = sourceUrl,
                    Publisher = publisher,
                    SupportingQuote = quote.Trim(),
                    ObservedDate = TryParseDate(GetString(item, "publishedOrObservedDate")),
                    AffectsPrediction = false
                });
            }

            return claims;
        }

        public static void ApplyPredictionFlags(IEnumerable<AvailabilityClaim> claims, bool requireCrossCheck)
        {
            foreach (var claim in claims)
                claim.AffectsPrediction = false;

            var confirmed = claims
                .Where(c => IsConfirmedOut(c.Status))
                .GroupBy(c => $"{c.TeamId}|{c.PlayerKey}", StringComparer.Ordinal);

            foreach (var group in confirmed)
            {
                var hasOfficial = group.Any(c => c.EvidenceLevel == AvailabilityEvidenceLevel.Official);
                var reputableSourceCount = group
                    .Where(c => c.EvidenceLevel == AvailabilityEvidenceLevel.ReputableReported)
                    .Select(c => PublisherKey(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();
                var shouldAffect = hasOfficial || !requireCrossCheck || reputableSourceCount >= 2;

                if (!shouldAffect)
                    continue;

                foreach (var claim in group)
                    claim.AffectsPrediction = true;
            }
        }

        public static string NormalizePlayerKey(string player) =>
            Regex.Replace(player.ToLowerInvariant().Normalize(NormalizationForm.FormD), @"[^\p{Ll}\p{Nd}]+", "-").Trim('-');

        private async Task<SourceFetchResult> FetchSourceAsync(string url, CancellationToken ct)
        {
            try
            {
                using var response = await _http.GetAsync(url, ct);
                var html = await response.Content.ReadAsStringAsync(ct);
                var title = ExtractTitle(html);
                var publisher = PublisherFromUrl(url);

                if (!response.IsSuccessStatusCode)
                    return SourceFetchResult.Fail(url, (int)response.StatusCode, title, publisher, $"HTTP {(int)response.StatusCode} al leer {url}.");

                if (LooksBotGated(html))
                    return SourceFetchResult.Fail(url, (int)response.StatusCode, title, publisher, $"{publisher ?? url}: la página parece bloqueada por verificación o JavaScript.");

                var text = ExtractReadableText(html);
                if (text.Length > _config.AvailabilityMaxArticleChars)
                    text = text[.._config.AvailabilityMaxArticleChars];

                if (text.Length < 200)
                    return SourceFetchResult.Fail(url, (int)response.StatusCode, title, publisher, $"{publisher ?? url}: texto insuficiente para clasificar.");

                return SourceFetchResult.Ok(url, (int)response.StatusCode, title, publisher, text);
            }
            catch (Exception ex)
            {
                return SourceFetchResult.Fail(url, 0, null, PublisherFromUrl(url), ex.Message);
            }
        }

        private async Task<string> ClassifyAsync(SourceFetchResult source, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config.OpenRouterApiKey);
            request.Content = JsonContent.Create(new
            {
                model = _config.OpenRouterModel,
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = """
                        You extract source-backed football availability claims. Return JSON only:
                        {"claims":[{"player":"","team":"","status":"","reason":"","confidence":"","evidenceLevel":"","supportingText":"","sourceUrl":"","publishedOrObservedDate":""}]}
                        Allowed status values: ConfirmedOutInjury, ConfirmedOutIllness, ConfirmedOutSuspension, ConfirmedOutOther, Doubtful, FitnessConcern, Rumor, Available, NotRelevant.
                        Allowed evidenceLevel values: Official, ReputableReported, ReportedUncertain, Unsupported.
                        Use ConfirmedOut* only for clear ruled out, withdrawn, replaced, will miss, suspended, or unavailable statements. Use Doubtful/FitnessConcern for could miss, race to be fit, doubt, or fitness concern. Do not infer beyond the article text.
                        """
                    },
                    new
                    {
                        role = "user",
                        content = $"Source URL: {source.Url}\nPublisher: {source.Publisher}\nTitle: {source.Title}\nArticle text:\n{source.Text}"
                    }
                },
                response_format = new { type = "json_object" }
            }, options: JsonOptions);

            using var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(ct);
            using var document = JsonDocument.Parse(body);
            return document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? throw new JsonException("OpenRouter response did not include message content.");
        }

        private async Task UpsertSourceAsync(string url, SourceFetchResult fetch, CancellationToken ct)
        {
            var source = await _db.AvailabilitySources.SingleOrDefaultAsync(s => s.Url == url, ct);
            if (source is null)
            {
                source = new AvailabilitySource { Url = url };
                _db.AvailabilitySources.Add(source);
            }

            source.Title = fetch.Title;
            source.Publisher = fetch.Publisher;
            source.StatusCode = fetch.StatusCode;
            source.TextHash = fetch.Text is null ? null : Hash(fetch.Text);
            source.Error = fetch.Error;
            source.LastFetchedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        private async Task ReplaceClaimsForSourceAsync(string url, IReadOnlyList<AvailabilityClaim> claims, CancellationToken ct)
        {
            var existing = await _db.AvailabilityClaims.Where(c => c.SourceUrl == url).ToListAsync(ct);
            _db.AvailabilityClaims.RemoveRange(existing);
            _db.AvailabilityClaims.AddRange(claims);
            await _db.SaveChangesAsync(ct);
        }

        private async Task RecomputePredictionFlagsAsync(CancellationToken ct)
        {
            var claims = await _db.AvailabilityClaims.ToListAsync(ct);
            ApplyPredictionFlags(claims, _config.AvailabilityRequireCrossCheck);
            await _db.SaveChangesAsync(ct);
        }

        private async Task<int> RefreshAllFixtureContextsAsync(CancellationToken ct)
        {
            var fixtures = await _db.Fixtures.AsNoTracking().ToListAsync(ct);
            var updated = 0;
            foreach (var fixture in fixtures)
            {
                if (await RefreshFixtureContextCountsAsync(fixture.Id, [], ct))
                    updated++;
            }

            return updated;
        }

        private static string AppendAvailabilityNote(string existing, int home, int away)
        {
            var prefix = string.IsNullOrWhiteSpace(existing) ? "" : existing.Split(" Noticias:")[0].Trim();
            var note = $"Noticias: bajas confirmadas equipo A {home}, equipo B {away}.";
            return string.IsNullOrWhiteSpace(prefix) ? note : $"{prefix} {note}";
        }

        private static bool IsConfirmedOut(AvailabilityClaimStatus status) =>
            status is AvailabilityClaimStatus.ConfirmedOutInjury
                or AvailabilityClaimStatus.ConfirmedOutIllness
                or AvailabilityClaimStatus.ConfirmedOutSuspension
                or AvailabilityClaimStatus.ConfirmedOutOther;

        private static string PublisherKey(AvailabilityClaim claim) =>
            string.IsNullOrWhiteSpace(claim.Publisher) ? claim.SourceUrl : claim.Publisher;

        private static T ParseEnum<T>(string value, T fallback) where T : struct, Enum
        {
            var cleaned = Regex.Replace(value ?? "", @"[\s_\-]+", "");
            foreach (var name in Enum.GetNames<T>())
            {
                if (string.Equals(Regex.Replace(name, @"[\s_\-]+", ""), cleaned, StringComparison.OrdinalIgnoreCase))
                    return Enum.Parse<T>(name);
            }

            return fallback;
        }

        private static string GetString(JsonElement item, string name) =>
            item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

        private static DateTimeOffset? TryParseDate(string value) =>
            DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : null;

        private static string ExtractTitle(string html)
        {
            var match = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
            return match.Success ? WebUtility.HtmlDecode(Regex.Replace(match.Groups[1].Value, @"\s+", " ").Trim()) : "";
        }

        private static string ExtractReadableText(string html)
        {
            var text = Regex.Replace(html, @"<script\b[^<]*(?:(?!</script>)<[^<]*)*</script>", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<style\b[^<]*(?:(?!</style>)<[^<]*)*</style>", " ", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"<[^>]+>", " ");
            text = WebUtility.HtmlDecode(text);
            return Regex.Replace(text, @"\s+", " ").Trim();
        }

        private static bool LooksBotGated(string html)
        {
            var text = html.ToLowerInvariant();
            return text.Contains("enable javascript")
                || text.Contains("verify you are a human")
                || text.Contains("bot detection")
                || text.Contains("captcha");
        }

        private static string? PublisherFromUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
            return host;
        }

        private static string Hash(string text) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

        private sealed record SourceFetchResult(
            string Url,
            int StatusCode,
            string? Title,
            string? Publisher,
            string? Text,
            string? Error)
        {
            public bool Success => Error is null;

            public static SourceFetchResult Ok(string url, int statusCode, string? title, string? publisher, string text) =>
                new(url, statusCode, title, publisher, text, null);

            public static SourceFetchResult Fail(string url, int statusCode, string? title, string? publisher, string error) =>
                new(url, statusCode, title, publisher, null, error);
        }
    }
}
