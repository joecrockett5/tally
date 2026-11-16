using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;

namespace Tally.Api.Infrastructure;

/// <summary>
/// The certificate Tally serves HTTPS with when it isn't behind a proxy. Either the files named in
/// <see cref="HttpsOptions"/>, or a self-signed certificate that is generated once and kept in
/// <see cref="HttpsOptions.DataDirectory"/> so devices that accepted it aren't asked again after a restart.
/// </summary>
public sealed class TlsCertificates(IOptions<TallyOptions> options, TimeProvider time, ILogger<TlsCertificates> logger)
{
    public const string CertificateFile = "tally.crt";
    public const string KeyFile = "tally.key";

    // Apple devices reject server certificates valid for more than 825 days.
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(820);
    private static readonly TimeSpan RenewBefore = TimeSpan.FromDays(30);

    private HttpsOptions O => options.Value.Https;

    /// <summary>The server certificate and, for a supplied one, any intermediates to send with it.</summary>
    public (X509Certificate2 Certificate, X509Certificate2Collection? Chain) Get() => _cached ??= Load();
    private (X509Certificate2, X509Certificate2Collection?)? _cached;

    private (X509Certificate2, X509Certificate2Collection?) Load()
    {
        if (!string.IsNullOrWhiteSpace(O.CertificatePath))
        {
            var keyPath = string.IsNullOrWhiteSpace(O.KeyPath) ? null : O.KeyPath;
            var supplied = X509Certificate2.CreateFromPemFile(O.CertificatePath, keyPath);
            var chain = new X509Certificate2Collection();
            chain.ImportFromPemFile(O.CertificatePath);
            var intermediates = new X509Certificate2Collection(chain.Where(c => c.Thumbprint != supplied.Thumbprint).ToArray());
            logger.LogInformation("HTTPS is using the certificate at {Path} (expires {Expiry:yyyy-MM-dd})", O.CertificatePath, supplied.NotAfter);
            return (supplied, intermediates.Count > 0 ? intermediates : null);
        }

        var hosts = ConfiguredHosts(O.Hosts);
        var directory = string.IsNullOrWhiteSpace(O.DataDirectory) ? null : O.DataDirectory;
        if (directory is not null && TryLoadStored(directory, hosts) is { } stored)
        {
            logger.LogInformation("HTTPS is using the self-signed certificate in {Directory} (expires {Expiry:yyyy-MM-dd})", directory, stored.NotAfter);
            return (stored, null);
        }

        var created = CreateSelfSigned(hosts.Concat(LocalNames()), time.GetUtcNow());
        if (directory is null)
            logger.LogWarning("HTTPS is using a temporary self-signed certificate. Set Tally:Https:DataDirectory to keep it across restarts");
        else if (TryStore(directory, created))
            logger.LogInformation("Created a self-signed HTTPS certificate in {Directory} for {Names}", directory, string.Join(", ", SubjectAlternativeNames(created)));
        else
            logger.LogWarning("Couldn't write to {Directory}, so the self-signed HTTPS certificate will change on every restart and devices will be asked to accept it again", directory);
        return (created, null);
    }

    private X509Certificate2? TryLoadStored(string directory, IReadOnlyCollection<string> hosts)
    {
        var certPath = Path.Combine(directory, CertificateFile);
        var keyPath = Path.Combine(directory, KeyFile);
        if (!File.Exists(certPath) || !File.Exists(keyPath)) return null;
        try
        {
            var cert = X509Certificate2.CreateFromPemFile(certPath, keyPath);
            var now = time.GetUtcNow();
            if (cert.NotAfter.ToUniversalTime() - RenewBefore < now)
            {
                logger.LogInformation("The stored HTTPS certificate is about to expire, so a new one will be created");
                return null;
            }
            var names = SubjectAlternativeNames(cert);
            if (hosts.Any(h => !names.Contains(h, StringComparer.OrdinalIgnoreCase)))
            {
                logger.LogInformation("The stored HTTPS certificate doesn't cover every configured host, so a new one will be created");
                return null;
            }
            return cert;
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The stored HTTPS certificate in {Directory} couldn't be read, so a new one will be created", directory);
            return null;
        }
    }

    private static bool TryStore(string directory, X509Certificate2 cert)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var keyPath = Path.Combine(directory, KeyFile);
            File.Delete(keyPath);
            // Created owner-only from the start, so the key is never briefly readable by others.
            var keyOptions = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) keyOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var key = new FileStream(keyPath, keyOptions))
            using (var writer = new StreamWriter(key))
                writer.Write(cert.GetECDsaPrivateKey()!.ExportPkcs8PrivateKeyPem());
            File.WriteAllText(Path.Combine(directory, CertificateFile), cert.ExportCertificatePem());
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// A self-signed certificate for <paramref name="names"/> (IP addresses and DNS names). It is marked as its
    /// own authority so that it can also be installed as trusted on a phone.
    /// </summary>
    public static X509Certificate2 CreateSelfSigned(IEnumerable<string> names, DateTimeOffset now)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Tally", key, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(certificateAuthority: true, hasPathLengthConstraint: true, pathLengthConstraint: 0, critical: true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyCertSign, critical: true));
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false)); // serverAuth
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, critical: false));

        var san = new SubjectAlternativeNameBuilder();
        foreach (var name in names.Append("localhost").Append("127.0.0.1").Append("::1").Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (IPAddress.TryParse(name, out var ip)) san.AddIpAddress(ip);
            else san.AddDnsName(name);
        }
        request.CertificateExtensions.Add(san.Build());

        using var cert = request.CreateSelfSigned(now.AddDays(-1), now.Add(Lifetime));
        // Round-trip through PEM so the result owns an exportable copy of the key.
        return X509Certificate2.CreateFromPem(cert.ExportCertificatePem(), key.ExportPkcs8PrivateKeyPem());
    }

    /// <summary>The DNS names and IP addresses a certificate covers.</summary>
    public static IReadOnlyList<string> SubjectAlternativeNames(X509Certificate2 cert)
    {
        var san = cert.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        return san is null ? [] : [.. san.EnumerateDnsNames(), .. san.EnumerateIPAddresses().Select(ip => ip.ToString())];
    }

    /// <summary>Splits the Hosts setting; IP addresses are normalised so they compare equal to the certificate's.</summary>
    public static IReadOnlyList<string> ConfiguredHosts(string? hosts) =>
        (hosts ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(h => Uri.CheckHostName(h) != UriHostNameType.Unknown)
            .Select(h => IPAddress.TryParse(h, out var ip) ? ip.ToString() : h.ToLowerInvariant())
            .Distinct()
            .ToList();

    /// <summary>
    /// This machine's own name and addresses. In a container these are the container's, not the host's,
    /// which is why the addresses people actually browse to are set with Hosts.
    /// </summary>
    private static IEnumerable<string> LocalNames()
    {
        var names = new List<string>();
        try
        {
            if (Uri.CheckHostName(Dns.GetHostName()) == UriHostNameType.Dns) names.Add(Dns.GetHostName());
            names.AddRange(NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .Select(a => a.Address)
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork || (a.AddressFamily == AddressFamily.InterNetworkV6 && !a.IsIPv6LinkLocal))
                .Select(a => a.ToString()));
        }
        catch (Exception ex) when (ex is NetworkInformationException or SocketException or PlatformNotSupportedException)
        {
            // The names are a convenience; localhost and the configured hosts are always included.
        }
        return names;
    }
}
