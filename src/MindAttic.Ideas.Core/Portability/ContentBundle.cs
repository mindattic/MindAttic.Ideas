namespace MindAttic.Ideas.Core.Portability;

/// <summary>
/// The wire format for moving AUTHORED CONTENT between environments — the thing a `.idea` package
/// deliberately is not. A package carries a citizen (a Theme, a Plugin, a Component, a compiled Page
/// TYPE); a bundle carries what an author made with them: pages, their bodies, their theme and plugin
/// selections, their SEO, their per-component metadata, and the media they reference.
/// <para>
/// Why this exists: a dev database can hold a hand-curated site — a composed home page, extracted
/// media, FromMd slots pointing at local files — that <c>--seed</c> can regenerate the shape of but
/// never the curation of. Without a bundle, "promote what I built to production" has no answer but
/// re-doing it by hand.
/// </para>
/// <para>
/// Identity travels on <see cref="Entities.ContentEntityBase.Uid"/>, the portable secondary identity;
/// integer ids are environment-local and never cross the boundary. Media is the exception — see
/// <see cref="BundleMedia"/> — because the store, not the bundle, mints a media uid.
/// </para>
/// </summary>
public sealed class ContentBundle
{
    /// <summary>Bundle format version. Whole numbers; a reader refuses a version it does not know.</summary>
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public const int CurrentFormatVersion = 1;

    /// <summary>The file inside the archive that carries this manifest.</summary>
    public const string ManifestEntryName = "bundle.json";

    /// <summary>Archive folder holding media payloads, one file per item, named by its source uid.</summary>
    public const string MediaFolder = "media/";

    public DateTime ExportedUtc { get; set; }

    /// <summary>Free text: which machine/environment produced this, for the import log.</summary>
    public string? ExportedFrom { get; set; }

    public BundleSite? Site { get; set; }
    public List<BundleSetting> Settings { get; set; } = [];
    public List<BundlePage> Pages { get; set; } = [];
    public List<BundleComponentMetadata> ComponentMetadata { get; set; } = [];
    public List<BundleMedia> Media { get; set; } = [];
}

/// <summary>The site row. Matched on <see cref="Key"/>, not uid — "default" means the same thing everywhere.</summary>
public sealed class BundleSite
{
    public string Key { get; set; } = "default";
    public string Name { get; set; } = "";
    public string HostBindings { get; set; } = "";
    public string DefaultThemeKey { get; set; } = "";
    public int DefaultThemeVersion { get; set; } = 1;
    public string? SettingsJson { get; set; }
}

/// <summary>
/// A setting from the override chain. Only Host- and Site-scope entries travel: a Page-scope entry
/// keys on an environment-local page id, and everything a page needs is already on the page row.
/// </summary>
public sealed class BundleSetting
{
    public string Scope { get; set; } = "Host";
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}

/// <summary>An authored page, with everything that makes it render the way the author left it.</summary>
public sealed class BundlePage
{
    public Guid Uid { get; set; }
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? SeoTitle { get; set; }

    /// <summary>Parent page's uid, so the nav tree survives without exporting integer ids.</summary>
    public Guid? ParentUid { get; set; }

    public string Kind { get; set; } = "Data";
    public string? BodyHtml { get; set; }
    public string? PageCss { get; set; }
    public string? PageJs { get; set; }

    /// <summary>
    /// Author or Untrusted. Import does NOT blindly trust this — see ImportContentCli: a bundle is a
    /// file from elsewhere, and Author trust means raw markup passthrough.
    /// </summary>
    public string BodyTrust { get; set; } = "Untrusted";

    public string? ThemeKey { get; set; }
    public int? ThemeVersion { get; set; }
    public string? ActivePluginsJson { get; set; }

    public string? ComponentTypeName { get; set; }
    public string? AssemblyName { get; set; }
    public string? SettingsJson { get; set; }

    public bool IsPublished { get; set; }
    public bool Enabled { get; set; } = true;
    public bool IsRestricted { get; set; }
    public bool OpenInNewWindow { get; set; }
    public int SortOrder { get; set; }
    public string? WorkflowState { get; set; }

    public Dictionary<string, string> MetaTags { get; set; } = [];

    /// <summary>Role NAMES granted access. Users are deliberately not exported — accounts are per-environment.</summary>
    public List<string> RoleAccess { get; set; } = [];

    /// <summary>Old slugs that still resolve to this page, so 301s survive the move.</summary>
    public List<BundleSlugAlias> SlugHistory { get; set; } = [];
}

public sealed class BundleSlugAlias
{
    public string OldSlug { get; set; } = "";
    public bool IsVanity { get; set; }
}

/// <summary>Per-component per-page metadata (e.g. FromMd's markdown + source path), keyed by page uid.</summary>
public sealed class BundleComponentMetadata
{
    public Guid PageUid { get; set; }
    public string ComponentKey { get; set; } = "";
    public string SlotName { get; set; } = "main";
    public string MetadataJson { get; set; } = "{}";
}

/// <summary>
/// A media item and the archive entry holding its bytes.
/// <para>
/// <see cref="SourceUid"/> is the uid the item had in the EXPORTING environment, and it is not a
/// promise about the importing one: <c>IMediaStore.UploadAsync</c> mints the uid, so an imported item
/// gets a new one. That is why import rewrites every <c>/_media/{uid}</c> reference through an
/// old→new map instead of forcing the uid, which would work for the local disk store and quietly
/// corrupt the Azure one (where the blob is addressed by uid).
/// </para>
/// </summary>
public sealed class BundleMedia
{
    public Guid SourceUid { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public string Folder { get; set; } = "";
    public string MediaType { get; set; } = "";
    public long SizeBytes { get; set; }
    /// <summary>Content hash — the reuse key, so a re-import uploads nothing.</summary>
    public string Sha256 { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Notes { get; set; }
    /// <summary>Path of the payload inside the archive, e.g. <c>media/3f2b…-….png</c>.</summary>
    public string EntryName { get; set; } = "";
}
