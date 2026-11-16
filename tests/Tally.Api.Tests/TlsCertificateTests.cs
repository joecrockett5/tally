using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tally.Api.Infrastructure;

namespace Tally.Api.Tests;

public sealed class TlsCertificateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tally-tls-" + Guid.NewGuid().ToString("N"));
    private readonly TestTime _time = new();

    private sealed class TestTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 11, 16, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private TlsCertificates Create(Action<HttpsOptions>? configure = null)
    {
        var options = new TallyOptions { Https = { DataDirectory = _dir } };
        configure?.Invoke(options.Https);
        return new TlsCertificates(Options.Create(options), _time, NullLogger<TlsCertificates>.Instance);
    }

    [Fact]
    public void A_self_signed_certificate_is_created_once_and_reused_after_a_restart()
    {
        var first = Create().Get().Certificate;
        Assert.True(first.HasPrivateKey);
        Assert.Contains("localhost", TlsCertificates.SubjectAlternativeNames(first));
        Assert.Contains("127.0.0.1", TlsCertificates.SubjectAlternativeNames(first));
        Assert.True(first.NotAfter.ToUniversalTime() - _time.GetUtcNow() < TimeSpan.FromDays(825));

        _time.Advance(TimeSpan.FromDays(200));
        Assert.Equal(first.Thumbprint, Create().Get().Certificate.Thumbprint);

        if (!OperatingSystem.IsWindows())
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(Path.Combine(_dir, TlsCertificates.KeyFile)));
    }

    [Fact]
    public void Configured_hosts_are_covered_and_a_new_host_replaces_the_certificate()
    {
        var first = Create(o => o.Hosts = "192.168.1.20, Tally.lan").Get().Certificate;
        var names = TlsCertificates.SubjectAlternativeNames(first);
        Assert.Contains("192.168.1.20", names);
        Assert.Contains("tally.lan", names);

        Assert.Equal(first.Thumbprint, Create(o => o.Hosts = "tally.lan;192.168.1.20").Get().Certificate.Thumbprint);

        var second = Create(o => o.Hosts = "192.168.1.20,10.0.0.5").Get().Certificate;
        Assert.NotEqual(first.Thumbprint, second.Thumbprint);
        Assert.Contains("10.0.0.5", TlsCertificates.SubjectAlternativeNames(second));
    }

    [Fact]
    public void A_certificate_close_to_expiry_is_replaced()
    {
        var first = Create().Get().Certificate;
        _time.Advance(TimeSpan.FromDays(800));
        var second = Create().Get().Certificate;
        Assert.NotEqual(first.Thumbprint, second.Thumbprint);
        Assert.True(second.NotAfter.ToUniversalTime() > _time.GetUtcNow().AddDays(365));
    }

    [Fact]
    public void A_damaged_stored_certificate_is_replaced()
    {
        Create().Get();
        File.WriteAllText(Path.Combine(_dir, TlsCertificates.CertificateFile), "not a certificate");
        Assert.True(Create().Get().Certificate.HasPrivateKey);
    }

    [Fact]
    public void Without_a_data_directory_the_certificate_is_only_kept_in_memory()
    {
        var certificates = Create(o => o.DataDirectory = null);
        Assert.True(certificates.Get().Certificate.HasPrivateKey);
        Assert.Same(certificates.Get().Certificate, certificates.Get().Certificate);
        Assert.False(Directory.Exists(_dir));
    }

    [Fact]
    public void A_supplied_certificate_is_used_with_its_intermediates()
    {
        Directory.CreateDirectory(_dir);
        using var leaf = TlsCertificates.CreateSelfSigned(["tally.example.com"], _time.GetUtcNow());
        using var other = TlsCertificates.CreateSelfSigned(["intermediate.example.com"], _time.GetUtcNow());
        var certPath = Path.Combine(_dir, "fullchain.pem");
        var keyPath = Path.Combine(_dir, "privkey.pem");
        File.WriteAllText(certPath, leaf.ExportCertificatePem() + "\n" + other.ExportCertificatePem() + "\n");
        File.WriteAllText(keyPath, leaf.GetECDsaPrivateKey()!.ExportPkcs8PrivateKeyPem());

        var (certificate, chain) = Create(o => (o.CertificatePath, o.KeyPath) = (certPath, keyPath)).Get();

        Assert.Equal(leaf.Thumbprint, certificate.Thumbprint);
        Assert.True(certificate.HasPrivateKey);
        Assert.Equal(other.Thumbprint, Assert.Single(chain!).Thumbprint);
        Assert.False(File.Exists(Path.Combine(_dir, TlsCertificates.CertificateFile)));
    }
}
