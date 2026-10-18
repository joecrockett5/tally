using System.Globalization;
using Microsoft.Extensions.Options;

namespace Tally.Api.Infrastructure;

/// <summary>Calendar dates in the instance's configured timezone.</summary>
public sealed class Clock(TimeProvider time, IOptions<TallyOptions> options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);

    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), _zone).DateTime);

    public static bool TryParseDate(string? s, out DateOnly date) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
