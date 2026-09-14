using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using MindAttic.Ideas.Core.Services;

namespace MindAttic.Ideas.Core.Secrets;

/// <summary>
/// Reads the host's ONE trusted package-signing certificate from the MindAttic.Vault
/// <c>PackageSigning</c> bucket (<c>MindAttic:Vault:PackageSigning:signing-cert-public</c>) — base64
/// DER of the PUBLIC certificate only, the same base64-in-config blueprint
/// <c>MindAttic.Authentication</c>'s <c>ConfigAuthSecrets.GetRequiredBytes</c> already uses for the
/// auth pepper and Data-Protection key. The running CMS host only ever verifies signatures, so it never
/// needs the private key/pfx — that stays on whatever machine publishes packages.
/// </summary>
public sealed class VaultPackageSigningTrust(IConfiguration configuration) : IPackageSigningTrust
{
    private const string Key = "MindAttic:Vault:PackageSigning:signing-cert-public";
    private X509Certificate2? _cached;

    public X509Certificate2 TrustedCertificate => _cached ??= Load();

    private X509Certificate2 Load()
    {
        var b64 = configuration[Key] ?? throw new InvalidOperationException(
            $"No trusted package-signing certificate at {Key}. Provision it in the MindAttic.Vault " +
            "PackageSigning bucket (%APPDATA%\\MindAttic\\PackageSigning\\providers.json in dev; " +
            "Key Vault/env in prod).");
        return X509CertificateLoader.LoadCertificate(Convert.FromBase64String(b64));
    }
}
