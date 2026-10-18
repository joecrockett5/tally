using Tally.Api.Features.Foods;

namespace Tally.Api.Tests;

public sealed class BarcodeTests
{
    [Theory]
    [InlineData("5000112637922", null, "5000112637922")] // EAN-13
    [InlineData("50184453", null, "50184453")] // EAN-8
    [InlineData("036000291452", null, "0036000291452")] // UPC-A → EAN-13
    [InlineData("04252614", "upc_e", "0042100005264")] // UPC-E → UPC-A → EAN-13
    [InlineData(" 5000 1126 37922 ", null, "5000112637922")]
    [InlineData("12345", null, null)]
    [InlineData("", null, null)]
    public void Normalize_handles_retail_formats(string raw, string? format, string? expected) =>
        Assert.Equal(expected, Barcodes.Normalize(raw, format));

    [Theory]
    [InlineData("0425261", "042100005264")]
    [InlineData("01234565", "012345000065")]
    public void ExpandUpcE_matches_reference_values(string upcE, string upcA) =>
        Assert.Equal(upcA, Barcodes.ExpandUpcE(upcE));
}
