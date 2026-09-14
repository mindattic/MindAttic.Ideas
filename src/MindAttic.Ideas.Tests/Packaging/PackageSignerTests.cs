using System.IO.Compression;
using System.Text;
using MindAttic.Ideas.Packaging;

namespace MindAttic.Ideas.Tests.Packaging;

[TestFixture]
public class PackageSignerTests
{
    private static MemoryStream BuildUnsigned() =>
        IdeaTestArchive.Build(new Dictionary<string, string>
        {
            ["idea.json"] = ManifestReader.Write(new IdeaManifest
            {
                ManifestVersion = 1, Category = "Plugin", Kind = "code", Key = "ui.tooltip", Version = 1,
                DisplayName = "Tooltip", Sdk = 1, EntryType = "MindAttic.Ideas.Plugin.Demo.V1", AssemblyName = "Demo",
            }),
            ["bin/Demo.dll"] = "MZ-fake",
        }, sign: false);

    [Test]
    public void RoundTrip_SignThenVerify_IsOk()
    {
        var archive = BuildUnsigned();
        PackageSigner.Sign(archive, TestSigningFixture.SigningCert);
        archive.Position = 0;

        using var reader = IdeaArchiveReader.Open(archive);
        var outcome = PackageSigner.Verify(reader, TestSigningFixture.PublicOnly);

        Assert.That(outcome, Is.EqualTo(PackageSigner.VerifyOutcome.Ok));
    }

    [Test]
    public void MissingSignature_IsNotSigned()
    {
        var archive = BuildUnsigned();
        using var reader = IdeaArchiveReader.Open(archive);

        Assert.That(PackageSigner.Verify(reader, TestSigningFixture.PublicOnly), Is.EqualTo(PackageSigner.VerifyOutcome.NotSigned));
    }

    [Test]
    public void TamperedContent_AfterSigning_IsBadSignature()
    {
        var archive = BuildUnsigned();
        PackageSigner.Sign(archive, TestSigningFixture.SigningCert);
        var tampered = IdeaTestArchive.Tamper(archive, "bin/Demo.dll", "TAMPERED");

        using var reader = IdeaArchiveReader.Open(tampered);
        Assert.That(PackageSigner.Verify(reader, TestSigningFixture.PublicOnly), Is.EqualTo(PackageSigner.VerifyOutcome.BadSignature));
    }

    [Test]
    public void WrongTrustedCertificate_IsUntrustedSigner()
    {
        var archive = BuildUnsigned();
        PackageSigner.Sign(archive, TestSigningFixture.SigningCert);
        archive.Position = 0;

        using var reader = IdeaArchiveReader.Open(archive);
        var wrongCert = TestSigningFixture.CreateUntrusted();
        Assert.That(PackageSigner.Verify(reader, wrongCert), Is.EqualTo(PackageSigner.VerifyOutcome.UntrustedSigner));
    }

    [Test]
    public void MalformedSignatureJson_IsMalformed()
    {
        var archive = BuildUnsigned();
        archive.Position = 0;
        using (var zip = new ZipArchive(archive, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.CreateEntry(PackageSigner.SignatureEntryName);
            using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            w.Write("{ not valid json");
        }
        archive.Position = 0;

        using var reader = IdeaArchiveReader.Open(archive);
        Assert.That(PackageSigner.Verify(reader, TestSigningFixture.PublicOnly), Is.EqualTo(PackageSigner.VerifyOutcome.Malformed));
    }

    [Test]
    public void ReSigning_IsIdempotent_ReplacesPriorSignature()
    {
        var archive = BuildUnsigned();
        PackageSigner.Sign(archive, TestSigningFixture.SigningCert);
        PackageSigner.Sign(archive, TestSigningFixture.SigningCert);   // sign again
        archive.Position = 0;

        using var reader = IdeaArchiveReader.Open(archive);
        Assert.That(PackageSigner.Verify(reader, TestSigningFixture.PublicOnly), Is.EqualTo(PackageSigner.VerifyOutcome.Ok));
    }
}
