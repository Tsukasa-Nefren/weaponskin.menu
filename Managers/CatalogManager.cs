using System.Collections.Frozen;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Sharp.Shared.Enums;
using Sharp.Shared.GameObjects;
using Sharp.Shared.Objects;
using Sharp.Shared.Units;

namespace WeaponSkin.Menu.Managers;

internal sealed record PaintOption(ushort PaintId, string Name);

internal sealed record WeaponPaintCatalog(EconItemId ItemId, string DisplayName, IReadOnlyList<PaintOption> Paints);

internal sealed record KnifeOption(string Name, EconItemId? ItemId);

internal sealed record GloveOption(EconItemId ItemId, ushort PaintId, string Name);

internal sealed record AgentOption(ushort ItemId, CStrikeTeam Team, string Name);

internal sealed record MusicKitOption(ushort ItemId, string Name);

internal sealed record MedalOption(ushort ItemId, string Name);

internal interface ICatalogManager
{
    IReadOnlyList<WeaponPaintCatalog> GetWeaponPaints(IGameClient? client = null);

    IReadOnlyList<KnifeOption> GetKnives(IGameClient? client = null);

    IReadOnlyList<PaintOption> GetKnifePaints(IGameClient? client, EconItemId itemId);

    IReadOnlyList<GloveOption> GetGloves(IGameClient? client = null);

    IReadOnlyList<AgentOption> GetAgents(IGameClient? client, CStrikeTeam team);

    IReadOnlyList<MusicKitOption> GetMusicKits(IGameClient? client = null);

    IReadOnlyList<MedalOption> GetMedals(IGameClient? client = null);

    string GetWeaponName(IGameClient? client, EconItemId itemId);
}

internal sealed class CatalogManager(
    InterfaceBridge bridge,
    IPlayerLanguageManager playerLanguages,
    ILogger<CatalogManager> logger) : ICatalogManager, IManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly FrozenDictionary<string, string> WeaponNamesByClass = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["weapon_deagle"] = "Desert Eagle",
        ["weapon_elite"] = "Dual Berettas",
        ["weapon_fiveseven"] = "Five-SeveN",
        ["weapon_glock"] = "Glock-18",
        ["weapon_ak47"] = "AK-47",
        ["weapon_aug"] = "AUG",
        ["weapon_awp"] = "AWP",
        ["weapon_famas"] = "FAMAS",
        ["weapon_g3sg1"] = "G3SG1",
        ["weapon_galilar"] = "Galil AR",
        ["weapon_m249"] = "M249",
        ["weapon_m4a1"] = "M4A4",
        ["weapon_mac10"] = "MAC-10",
        ["weapon_p90"] = "P90",
        ["weapon_mp5sd"] = "MP5-SD",
        ["weapon_ump45"] = "UMP-45",
        ["weapon_xm1014"] = "XM1014",
        ["weapon_bizon"] = "PP-Bizon",
        ["weapon_mag7"] = "MAG-7",
        ["weapon_negev"] = "Negev",
        ["weapon_sawedoff"] = "Sawed-Off",
        ["weapon_tec9"] = "Tec-9",
        ["weapon_taser"] = "Zeus x27",
        ["weapon_hkp2000"] = "P2000",
        ["weapon_mp7"] = "MP7",
        ["weapon_mp9"] = "MP9",
        ["weapon_nova"] = "Nova",
        ["weapon_p250"] = "P250",
        ["weapon_scar20"] = "SCAR-20",
        ["weapon_sg556"] = "SG 553",
        ["weapon_ssg08"] = "SSG 08",
        ["weapon_m4a1_silencer"] = "M4A1-S",
        ["weapon_usp_silencer"] = "USP-S",
        ["weapon_cz75a"] = "CZ75-Auto",
        ["weapon_revolver"] = "R8 Revolver",
        ["weapon_knife"] = "Default Knife",
        ["weapon_bayonet"] = "Bayonet",
        ["weapon_knife_css"] = "Classic Knife",
        ["weapon_knife_flip"] = "Flip Knife",
        ["weapon_knife_gut"] = "Gut Knife",
        ["weapon_knife_karambit"] = "Karambit",
        ["weapon_knife_m9_bayonet"] = "M9 Bayonet",
        ["weapon_knife_tactical"] = "Huntsman Knife",
        ["weapon_knife_falchion"] = "Falchion Knife",
        ["weapon_knife_survival_bowie"] = "Bowie Knife",
        ["weapon_knife_butterfly"] = "Butterfly Knife",
        ["weapon_knife_push"] = "Shadow Daggers",
        ["weapon_knife_cord"] = "Paracord Knife",
        ["weapon_knife_canis"] = "Survival Knife",
        ["weapon_knife_ursus"] = "Ursus Knife",
        ["weapon_knife_gypsy_jackknife"] = "Navaja Knife",
        ["weapon_knife_outdoor"] = "Nomad Knife",
        ["weapon_knife_stiletto"] = "Stiletto Knife",
        ["weapon_knife_widowmaker"] = "Talon Knife",
        ["weapon_knife_skeleton"] = "Skeleton Knife",
        ["weapon_knife_kukri"] = "Kukri Knife",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<ushort, string> PaintVariantFallbacks = new Dictionary<ushort, string>
    {
        [415] = "Ruby",
        [416] = "Sapphire",
        [417] = "Black Pearl",
        [418] = "Phase 1",
        [419] = "Phase 2",
        [420] = "Phase 3",
        [421] = "Phase 4",
        [568] = "Emerald",
        [569] = "Phase 1",
        [570] = "Phase 2",
        [571] = "Phase 3",
        [572] = "Phase 4",
        [1119] = "Emerald",
        [1120] = "Phase 1",
        [1121] = "Phase 2",
        [1122] = "Phase 3",
        [1123] = "Phase 4",
    }.ToFrozenDictionary();

    private static readonly IReadOnlyList<KnifeOption> KnifeDefinitions =
    [
        new("Bayonet", (EconItemId)500),
        new("Classic Knife", (EconItemId)503),
        new("Flip Knife", (EconItemId)505),
        new("Gut Knife", (EconItemId)506),
        new("Karambit", (EconItemId)507),
        new("M9 Bayonet", (EconItemId)508),
        new("Huntsman Knife", (EconItemId)509),
        new("Falchion Knife", (EconItemId)512),
        new("Bowie Knife", (EconItemId)514),
        new("Butterfly Knife", (EconItemId)515),
        new("Shadow Daggers", (EconItemId)516),
        new("Paracord Knife", (EconItemId)517),
        new("Survival Knife", (EconItemId)518),
        new("Ursus Knife", (EconItemId)519),
        new("Navaja Knife", (EconItemId)520),
        new("Nomad Knife", (EconItemId)521),
        new("Stiletto Knife", (EconItemId)522),
        new("Talon Knife", (EconItemId)523),
        new("Skeleton Knife", (EconItemId)525),
        new("Kukri Knife", (EconItemId)526),
    ];

    private readonly Dictionary<string, IReadOnlyList<WeaponPaintCatalog>> _weaponPaintsByLanguage = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<KnifeOption>> _knivesByLanguage = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyDictionary<EconItemId, IReadOnlyList<PaintOption>>> _knifePaintsByLanguage = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<GloveOption>> _glovesByLanguage = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<AgentOption>> _agentsByLanguage = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<MusicKitOption>> _musicKitsByLanguage = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<MedalOption>> _medalsByLanguage = new(StringComparer.OrdinalIgnoreCase);

    public bool Init()
    {
        var weaponPaints = GetOrLoadCatalog(_weaponPaintsByLanguage, "en", LoadWeaponPaints);
        var knives = GetOrLoadCatalog(_knivesByLanguage, "en", LoadKnives);
        var knifePaints = GetOrLoadCatalog(_knifePaintsByLanguage, "en", LoadKnifePaints);
        var gloves = GetOrLoadCatalog(_glovesByLanguage, "en", LoadGloves);
        var agents = GetOrLoadCatalog(_agentsByLanguage, "en", LoadAgents);
        var musicKits = GetOrLoadCatalog(_musicKitsByLanguage, "en", LoadMusicKits);
        var medals = GetOrLoadCatalog(_medalsByLanguage, "en", LoadMedals);

        logger.LogInformation(
            "Loaded {weaponGroupCount} weapon groups ({weaponPaintCount} paints), {knifeCount} knives, {gloveCount} gloves, {agentCount} agents, {musicCount} music kits, {medalCount} pins",
            weaponPaints.Count,
            weaponPaints.Sum(static group => group.Paints.Count),
            knives.Count,
            gloves.Count,
            agents.Count,
            musicKits.Count,
            medals.Count);

        _ = knifePaints;
        return true;
    }

    public IReadOnlyList<WeaponPaintCatalog> GetWeaponPaints(IGameClient? client = null)
        => GetOrLoadCatalog(_weaponPaintsByLanguage, GetCatalogLanguage(client), LoadWeaponPaints);

    public IReadOnlyList<KnifeOption> GetKnives(IGameClient? client = null)
        => GetOrLoadCatalog(_knivesByLanguage, GetCatalogLanguage(client), LoadKnives);

    public IReadOnlyList<PaintOption> GetKnifePaints(IGameClient? client, EconItemId itemId)
        => GetOrLoadCatalog(_knifePaintsByLanguage, GetCatalogLanguage(client), LoadKnifePaints)
            .TryGetValue(itemId, out var paints)
            ? paints
            : [];

    public IReadOnlyList<GloveOption> GetGloves(IGameClient? client = null)
        => GetOrLoadCatalog(_glovesByLanguage, GetCatalogLanguage(client), LoadGloves);

    public IReadOnlyList<AgentOption> GetAgents(IGameClient? client, CStrikeTeam team)
        => GetOrLoadCatalog(_agentsByLanguage, GetCatalogLanguage(client), LoadAgents)
            .Where(option => option.Team == team)
            .ToArray();

    public IReadOnlyList<MusicKitOption> GetMusicKits(IGameClient? client = null)
        => GetOrLoadCatalog(_musicKitsByLanguage, GetCatalogLanguage(client), LoadMusicKits);

    public IReadOnlyList<MedalOption> GetMedals(IGameClient? client = null)
        => GetOrLoadCatalog(_medalsByLanguage, GetCatalogLanguage(client), LoadMedals);

    public string GetWeaponName(IGameClient? client, EconItemId itemId)
    {
        if (GetWeaponPaints(client).FirstOrDefault(group => group.ItemId == itemId) is { } catalog)
        {
            return catalog.DisplayName;
        }

        if (bridge.EconItemManager.GetEconItemDefinitionByIndex(itemId) is { } definition)
        {
            return BeautifyName(definition.ItemBaseName, definition.DefinitionName);
        }

        return itemId.ToString();
    }

    private IReadOnlyList<WeaponPaintCatalog> LoadWeaponPaints(string language)
    {
        var filePath = ResolveLocalizedCatalogFile("skins", language);
        var data = ReadJson<List<SkinData>>(filePath);
        var knifeItemIds = KnifeDefinitions
            .Select(static option => (int)option.ItemId!.Value)
            .ToHashSet();

        if (data.Count == 0)
        {
            logger.LogWarning("No weapon skin catalog entries were loaded from {filePath}", filePath);
        }

        return BuildWeaponPaintCatalogs(data.Where(item => !knifeItemIds.Contains(item.WeaponDefIndex)));
    }

    private IReadOnlyDictionary<EconItemId, IReadOnlyList<PaintOption>> LoadKnifePaints(string language)
    {
        var filePath = ResolveLocalizedCatalogFile("skins", language);
        var data = ReadJson<List<SkinData>>(filePath);
        var knifeItemIds = KnifeDefinitions
            .Select(static option => (int)option.ItemId!.Value)
            .ToHashSet();

        return BuildWeaponPaintCatalogs(data.Where(item => knifeItemIds.Contains(item.WeaponDefIndex)))
            .ToDictionary(static catalog => catalog.ItemId, static catalog => catalog.Paints);
    }

    private IReadOnlyList<KnifeOption> LoadKnives(string language)
    {
        var paintsByKnife = LoadKnifePaints(language);

        return KnifeDefinitions
            .Select(option =>
            {
                var localizedName = paintsByKnife.TryGetValue(option.ItemId!.Value, out var paints)
                    ? ExtractDisplayName(option.Name, paints)
                    : option.Name;

                return new KnifeOption(localizedName, option.ItemId);
            })
            .OrderBy(static option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private IReadOnlyList<GloveOption> LoadGloves(string language)
    {
        var filePath = ResolveLocalizedCatalogFile("gloves", language);
        var data = ReadJson<List<GloveData>>(filePath);

        if (data.Count == 0)
        {
            logger.LogWarning("No glove catalog entries were loaded from {filePath}", filePath);
        }

        var options = new List<GloveOption>
        {
            new(0, 0, GetDefaultLabel(language, "Gloves | Default", "\uC7A5\uAC11 | \uAE30\uBCF8")),
        };

        options.AddRange(data
            .Where(static item => item.WeaponDefIndex > 0)
            .Select(item => new GloveOption((EconItemId)item.WeaponDefIndex, (ushort)item.Paint, item.PaintName))
            .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase));

        return options;
    }

    private IReadOnlyList<MusicKitOption> LoadMusicKits(string language)
    {
        var filePath = ResolveLocalizedCatalogFile("music", language);
        var data = ReadJson<List<NamedIdData>>(filePath);

        if (data.Count == 0)
        {
            logger.LogWarning("No music kit catalog entries were loaded from {filePath}", filePath);
        }

        var options = new List<MusicKitOption>
        {
            new(0, GetDefaultLabel(language, "Default Music Kit", "\uAE30\uBCF8 \uBBA4\uC9C1 \uD0B7")),
        };

        options.AddRange(data
            .Select(item => new MusicKitOption((ushort)item.Id, item.Name))
            .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase));

        return options;
    }

    private IReadOnlyList<MedalOption> LoadMedals(string language)
    {
        var filePath = ResolveLocalizedCatalogFile("collectibles", language);
        var data = ReadJson<List<NamedIdData>>(filePath);

        if (data.Count == 0)
        {
            logger.LogWarning("No collectible catalog entries were loaded from {filePath}", filePath);
        }

        var options = new List<MedalOption>
        {
            new(0, GetDefaultLabel(language, "Default Pin", "\uAE30\uBCF8 \uD540")),
        };

        options.AddRange(data
            .Select(item => new MedalOption((ushort)item.Id, item.Name))
            .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase));

        return options;
    }

    private IReadOnlyList<AgentOption> LoadAgents(string language)
    {
        var agents = new List<AgentOption>
        {
            new(0, CStrikeTeam.CT, GetDefaultLabel(language, "Default Agent", "\uAE30\uBCF8 \uC694\uC6D0")),
            new(0, CStrikeTeam.TE, GetDefaultLabel(language, "Default Agent", "\uAE30\uBCF8 \uC694\uC6D0")),
        };

        foreach (var definition in bridge.EconItemManager.GetEconItems().Values)
        {
            if (definition.DefaultLoadoutSlot != 38)
            {
                continue;
            }

            var team = ResolveAgentTeam(definition);
            if (team is null)
            {
                continue;
            }

            agents.Add(new AgentOption(definition.Index, team.Value, BeautifyName(definition.ItemBaseName, definition.DefinitionName)));
        }

        return agents
            .DistinctBy(static agent => (agent.Team, agent.ItemId))
            .OrderBy(static agent => agent.Team)
            .ThenBy(static agent => agent.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string GetCatalogLanguage(IGameClient? client)
        => playerLanguages.GetCatalogLanguage(client);

    private string ResolveLocalizedCatalogFile(string baseName, string language)
        => ResolveLocalizedFile(baseName, language, Path.Combine(bridge.ModuleDirectory, "Data"));

    private static string ResolveLocalizedFile(string baseName, string language, params string[] roots)
    {
        foreach (var fileName in EnumerateLocalizedFileNames(baseName, language))
        {
            foreach (var root in roots)
            {
                var candidate = Path.Combine(root, fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }

        return Path.Combine(roots[0], $"{baseName}_en.json");
    }

    private static IEnumerable<string> EnumerateLocalizedFileNames(string baseName, string language)
    {
        yield return $"{baseName}_{language}.json";

        if (!language.Equals("en", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{baseName}_en.json";
        }
    }

    private static TValue GetOrLoadCatalog<TValue>(IDictionary<string, TValue> cache, string language, Func<string, TValue> factory)
    {
        if (cache.TryGetValue(language, out var value))
        {
            return value;
        }

        value = factory(language);
        cache[language] = value;
        return value;
    }

    private IReadOnlyList<WeaponPaintCatalog> BuildWeaponPaintCatalogs(IEnumerable<SkinData> data)
    {
        return data
            .GroupBy(static item => new { item.WeaponDefIndex, item.WeaponName })
            .Select(group =>
            {
                var paints = group
                    .OrderBy(static item => item.PaintName, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new PaintOption((ushort)item.Paint, DecoratePaintName(item.PaintName, (ushort)item.Paint)))
                    .ToArray();

                var fallbackName = WeaponNamesByClass.TryGetValue(group.Key.WeaponName, out var name)
                    ? name
                    : group.Key.WeaponName;

                return new WeaponPaintCatalog(
                    (EconItemId)group.Key.WeaponDefIndex,
                    ExtractDisplayName(fallbackName, paints),
                    paints);
            })
            .OrderBy(static group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private string DecoratePaintName(string rawName, ushort paintId)
    {
        var name = NormalizePaintName(rawName);

        return TryResolvePaintVariant(paintId, out var variant)
               && !name.Contains(variant, StringComparison.OrdinalIgnoreCase)
            ? $"{name} ({variant})"
            : name;
    }

    private bool TryResolvePaintVariant(ushort paintId, out string variant)
    {
        if (bridge.EconItemManager.GetPaintKits().TryGetValue(paintId, out var paintKit)
            && TryParsePaintVariant($"{paintKit.Name}|{paintKit.DescriptionString}|{paintKit.DescriptionTag}", out variant))
        {
            return true;
        }

        return PaintVariantFallbacks.TryGetValue(paintId, out variant!);
    }

    private static bool TryParsePaintVariant(string source, out string variant)
    {
        var normalized = source
            .Replace('_', ' ')
            .Replace('-', ' ')
            .ToLowerInvariant();

        if (normalized.Contains("blackpearl", StringComparison.Ordinal)
            || normalized.Contains("black pearl", StringComparison.Ordinal))
        {
            variant = "Black Pearl";
            return true;
        }

        if (normalized.Contains("sapphire", StringComparison.Ordinal))
        {
            variant = "Sapphire";
            return true;
        }

        if (normalized.Contains("ruby", StringComparison.Ordinal))
        {
            variant = "Ruby";
            return true;
        }

        if (normalized.Contains("emerald", StringComparison.Ordinal))
        {
            variant = "Emerald";
            return true;
        }

        if (normalized.Contains("phase 1", StringComparison.Ordinal) || normalized.Contains("phase1", StringComparison.Ordinal))
        {
            variant = "Phase 1";
            return true;
        }

        if (normalized.Contains("phase 2", StringComparison.Ordinal) || normalized.Contains("phase2", StringComparison.Ordinal))
        {
            variant = "Phase 2";
            return true;
        }

        if (normalized.Contains("phase 3", StringComparison.Ordinal) || normalized.Contains("phase3", StringComparison.Ordinal))
        {
            variant = "Phase 3";
            return true;
        }

        if (normalized.Contains("phase 4", StringComparison.Ordinal) || normalized.Contains("phase4", StringComparison.Ordinal))
        {
            variant = "Phase 4";
            return true;
        }

        variant = string.Empty;
        return false;
    }

    private static string NormalizePaintName(string rawName)
    {
        var name = rawName.Trim();

        if (name.StartsWith("Ёк ", StringComparison.Ordinal))
        {
            name = $"★ {name["Ёк ".Length..]}";
        }

        return name;
    }

    private static string ExtractDisplayName(string fallbackName, IReadOnlyList<PaintOption> paints)
    {
        var sampleName = paints
            .Select(static paint => paint.Name)
            .FirstOrDefault(static name => name.Contains('|', StringComparison.Ordinal))
            ?? paints.FirstOrDefault()?.Name;

        if (string.IsNullOrWhiteSpace(sampleName))
        {
            return fallbackName;
        }

        var prefix = sampleName.Split('|', 2)[0]
            .Replace('\u2605', ' ')
            .Trim();

        return string.IsNullOrWhiteSpace(prefix) ? fallbackName : prefix;
    }

    private static string GetDefaultLabel(string language, string english, string korean)
        => language.Equals("ko", StringComparison.OrdinalIgnoreCase) ? korean : english;

    private static CStrikeTeam? ResolveAgentTeam(IEconItemDefinition definition)
    {
        var source = $"{definition.DefinitionName}|{definition.BaseDisplayModel}".ToLowerInvariant();

        if (source.Contains("ctm_", StringComparison.Ordinal))
        {
            return CStrikeTeam.CT;
        }

        if (source.Contains("tm_", StringComparison.Ordinal))
        {
            return CStrikeTeam.TE;
        }

        return null;
    }
    private static string BeautifyName(string preferred, string fallback)
    {
        var value = string.IsNullOrWhiteSpace(preferred) || preferred.StartsWith("#", StringComparison.Ordinal)
            ? fallback
            : preferred;

        value = value
            .Replace("customplayer_", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('_', ' ')
            .Trim('#', ' ');

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());
    }

    private static T ReadJson<T>(string filePath)
        where T : new()
    {
        if (!File.Exists(filePath))
        {
            return new();
        }

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? new();
    }

    private sealed record SkinData
    {
        [JsonPropertyName("weapon_defindex")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int WeaponDefIndex { get; init; }

        [JsonPropertyName("weapon_name")]
        public string WeaponName { get; init; } = string.Empty;

        [JsonPropertyName("paint")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Paint { get; init; }

        [JsonPropertyName("paint_name")]
        public string PaintName { get; init; } = string.Empty;
    }

    private sealed record GloveData
    {
        [JsonPropertyName("weapon_defindex")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int WeaponDefIndex { get; init; }

        [JsonPropertyName("paint")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Paint { get; init; }

        [JsonPropertyName("paint_name")]
        public string PaintName { get; init; } = string.Empty;
    }

    private sealed record NamedIdData
    {
        [JsonPropertyName("id")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int Id { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;
    }
}
