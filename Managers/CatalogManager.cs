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

    private static readonly FrozenDictionary<int, string> WeaponClassByDefindex = new Dictionary<int, string>
    {
        [1] = "weapon_deagle",
        [2] = "weapon_elite",
        [3] = "weapon_fiveseven",
        [4] = "weapon_glock",
        [7] = "weapon_ak47",
        [8] = "weapon_aug",
        [9] = "weapon_awp",
        [10] = "weapon_famas",
        [11] = "weapon_g3sg1",
        [13] = "weapon_galilar",
        [14] = "weapon_m249",
        [16] = "weapon_m4a1",
        [17] = "weapon_mac10",
        [19] = "weapon_p90",
        [23] = "weapon_mp5sd",
        [24] = "weapon_ump45",
        [25] = "weapon_xm1014",
        [26] = "weapon_bizon",
        [27] = "weapon_mag7",
        [28] = "weapon_negev",
        [29] = "weapon_sawedoff",
        [30] = "weapon_tec9",
        [31] = "weapon_taser",
        [32] = "weapon_hkp2000",
        [33] = "weapon_mp7",
        [34] = "weapon_mp9",
        [35] = "weapon_nova",
        [36] = "weapon_p250",
        [38] = "weapon_scar20",
        [39] = "weapon_sg556",
        [40] = "weapon_ssg08",
        [60] = "weapon_m4a1_silencer",
        [61] = "weapon_usp_silencer",
        [63] = "weapon_cz75a",
        [64] = "weapon_revolver",
        [500] = "weapon_bayonet",
        [503] = "weapon_knife_css",
        [505] = "weapon_knife_flip",
        [506] = "weapon_knife_gut",
        [507] = "weapon_knife_karambit",
        [508] = "weapon_knife_m9_bayonet",
        [509] = "weapon_knife_tactical",
        [512] = "weapon_knife_falchion",
        [514] = "weapon_knife_survival_bowie",
        [515] = "weapon_knife_butterfly",
        [516] = "weapon_knife_push",
        [517] = "weapon_knife_cord",
        [518] = "weapon_knife_canis",
        [519] = "weapon_knife_ursus",
        [520] = "weapon_knife_gypsy_jackknife",
        [521] = "weapon_knife_outdoor",
        [522] = "weapon_knife_stiletto",
        [523] = "weapon_knife_widowmaker",
        [525] = "weapon_knife_skeleton",
        [526] = "weapon_knife_kukri",
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<ushort, string> PaintVariantLocaleKeys = new Dictionary<ushort, string>
    {
        [415] = "ws.variant.ruby",
        [416] = "ws.variant.sapphire",
        [417] = "ws.variant.black_pearl",
        [418] = "ws.variant.phase_1",
        [419] = "ws.variant.phase_2",
        [420] = "ws.variant.phase_3",
        [421] = "ws.variant.phase_4",
        [568] = "ws.variant.emerald",
        [569] = "ws.variant.phase_1",
        [570] = "ws.variant.phase_2",
        [571] = "ws.variant.phase_3",
        [572] = "ws.variant.phase_4",
        [1119] = "ws.variant.emerald",
        [1120] = "ws.variant.phase_1",
        [1121] = "ws.variant.phase_2",
        [1122] = "ws.variant.phase_3",
        [1123] = "ws.variant.phase_4",
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
    private readonly Dictionary<string, LocalizedCatalogBundle> _catalogBundlesByLanguage = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Dictionary<string, string>>? _menuLocaleTexts;

    public bool Init()
    {
        var weaponPaints = GetOrLoadCatalog(_weaponPaintsByLanguage, "en", LoadWeaponPaints);
        var knives = GetOrLoadCatalog(_knivesByLanguage, "en", LoadKnives);
        var knifePaints = GetOrLoadCatalog(_knifePaintsByLanguage, "en", LoadKnifePaints);
        var gloves = GetOrLoadCatalog(_glovesByLanguage, "en", LoadGloves);
        var agents = GetOrLoadCatalog(_agentsByLanguage, "en", LoadAgents);
        var musicKits = GetOrLoadCatalog(_musicKitsByLanguage, "en", LoadMusicKits);
        var medals = GetOrLoadCatalog(_medalsByLanguage, "en", LoadMedals);

        logger.LogDebug(
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
        var data = GetCatalogBundle(language).Skins;
        var knifeItemIds = KnifeDefinitions
            .Select(static option => (int)option.ItemId!.Value)
            .ToHashSet();

        if (data.Count == 0)
        {
            logger.LogWarning("No weapon skin catalog entries were loaded for language {language}", language);
        }

        return BuildWeaponPaintCatalogs(language, data.Where(item => !knifeItemIds.Contains(item.WeaponDefIndex)));
    }

    private IReadOnlyDictionary<EconItemId, IReadOnlyList<PaintOption>> LoadKnifePaints(string language)
    {
        var data = GetCatalogBundle(language).Skins;
        var knifeItemIds = KnifeDefinitions
            .Select(static option => (int)option.ItemId!.Value)
            .ToHashSet();

        return BuildWeaponPaintCatalogs(language, data.Where(item => knifeItemIds.Contains(item.WeaponDefIndex)))
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
        var data = GetCatalogBundle(language).Gloves;

        if (data.Count == 0)
        {
            logger.LogWarning("No glove catalog entries were loaded for language {language}", language);
        }

        var options = new List<GloveOption>
        {
            new(0, 0, GetMenuLocaleText(language, "ws.menu.default_weapon_skin", "Default")),
        };

        options.AddRange(data
            .Where(static item => item.WeaponDefIndex > 0)
            .Select(item => new GloveOption((EconItemId)item.WeaponDefIndex, (ushort)item.Paint, item.PaintName))
            .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase));

        return options;
    }

    private IReadOnlyList<MusicKitOption> LoadMusicKits(string language)
    {
        var data = GetCatalogBundle(language).Music;

        if (data.Count == 0)
        {
            logger.LogWarning("No music kit catalog entries were loaded for language {language}", language);
        }

        var options = new List<MusicKitOption>
        {
            new(0, GetMenuLocaleText(language, "ws.menu.default_weapon_skin", "Default")),
        };

        var normalized = data
            .Select(item => new MusicKitOption((ushort)item.Id, NormalizeOptionName(item.Name)))
            .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        options.AddRange(EnsureDistinctOptionNames(
            normalized,
            static option => option.Name,
            static option => option.ItemId,
            static (option, name) => option with { Name = name }));

        return options;
    }

    private IReadOnlyList<MedalOption> LoadMedals(string language)
    {
        var data = GetCatalogBundle(language).Collectibles;

        if (data.Count == 0)
        {
            logger.LogWarning("No collectible catalog entries were loaded for language {language}", language);
        }

        var options = new List<MedalOption>
        {
            new(0, GetMenuLocaleText(language, "ws.menu.default_weapon_skin", "Default")),
        };

        var normalized = data
            .Select(item => new MedalOption((ushort)item.Id, NormalizeOptionName(item.Name)))
            .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        options.AddRange(EnsureDistinctOptionNames(
            normalized,
            static option => option.Name,
            static option => option.ItemId,
            static (option, name) => option with { Name = name }));

        return options;
    }

    private IReadOnlyList<AgentOption> LoadAgents(string language)
    {
        var agents = new List<AgentOption>
        {
            new(0, CStrikeTeam.CT, GetMenuLocaleText(language, "ws.menu.default_weapon_skin", "Default")),
            new(0, CStrikeTeam.TE, GetMenuLocaleText(language, "ws.menu.default_weapon_skin", "Default")),
        };

        var definitions = bridge.EconItemManager?.GetEconItems()?.Values;
        if (definitions is null)
        {
            return agents
                .OrderBy(static agent => agent.Team)
                .ThenBy(static agent => agent.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        foreach (var definition in definitions)
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

    private LocalizedCatalogBundle GetCatalogBundle(string language)
        => GetOrLoadCatalog(_catalogBundlesByLanguage, language, LoadCatalogBundle);

    private LocalizedCatalogBundle LoadCatalogBundle(string language)
    {
        var dataRoot = Path.Combine(bridge.ModuleDirectory, "Data");
        var bundlePath = ResolveLocalizedFile("catalog", language, dataRoot);
        if (File.Exists(bundlePath))
        {
            return ReadJson<LocalizedCatalogBundle>(bundlePath);
        }

        return new LocalizedCatalogBundle
        {
            Skins = ReadJson<List<SkinData>>(ResolveLocalizedFile("skins", language, dataRoot)),
            Gloves = ReadJson<List<GloveData>>(ResolveLocalizedFile("gloves", language, dataRoot)),
            Music = ReadJson<List<NamedIdData>>(ResolveLocalizedFile("music", language, dataRoot)),
            Collectibles = ReadJson<List<NamedIdData>>(ResolveLocalizedFile("collectibles", language, dataRoot)),
        };
    }

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

    private IReadOnlyList<WeaponPaintCatalog> BuildWeaponPaintCatalogs(string language, IEnumerable<SkinData> data)
    {
        return data
            .Select(NormalizeSkinData)
            .Where(static item => item.WeaponDefIndex > 0)
            .Where(IsSupportedPaintSelection)
            .GroupBy(static item => item.WeaponDefIndex)
            .Select(group =>
            {
                var paints = EnsureDistinctPaintNames(language, group
                    .OrderBy(static item => item.PaintName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static item => item.Paint)
                    .Select(item => new PaintOption((ushort)item.Paint, DecoratePaintName(language, item.PaintName, (ushort)item.Paint)))
                    .ToArray());

                var weaponClass = group
                    .Select(static item => item.WeaponName)
                    .FirstOrDefault(static name => !string.IsNullOrWhiteSpace(name))
                    ?? string.Empty;

                var fallbackName = WeaponNamesByClass.TryGetValue(weaponClass, out var name)
                    ? name
                    : weaponClass;

                return new WeaponPaintCatalog(
                    (EconItemId)group.Key,
                    ExtractDisplayName(fallbackName, paints),
                    paints);
            })
            .OrderBy(static group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private SkinData NormalizeSkinData(SkinData item)
        => new()
        {
            WeaponDefIndex = item.WeaponDefIndex,
            WeaponName = ResolveCanonicalWeaponClass(item.WeaponDefIndex, item.WeaponName),
            Paint = item.Paint,
            PaintName = item.PaintName,
        };

    private bool IsSupportedPaintSelection(SkinData item)
        => item.Paint == 0 || bridge.EconItemManager.GetPaintKits().ContainsKey((uint)item.Paint);

    private IReadOnlyList<PaintOption> EnsureDistinctPaintNames(string language, IReadOnlyList<PaintOption> paints)
    {
        var duplicateNames = paints
            .GroupBy(static paint => paint.Name, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        if (duplicateNames.Count == 0)
        {
            return paints.ToArray();
        }

        var result = new PaintOption[paints.Count];

        for (var index = 0; index < paints.Count; index++)
        {
            var paint = paints[index];

            result[index] = duplicateNames.Contains(paint.Name)
                ? paint with { Name = DisambiguatePaintName(language, paint.Name, paint.PaintId) }
                : paint;
        }

        return result;
    }

    private string DisambiguatePaintName(string language, string name, ushort paintId)
    {
        if (TryResolvePaintVariant(language, paintId, out var variant)
            && !string.IsNullOrWhiteSpace(variant)
            && !name.Contains(variant, StringComparison.OrdinalIgnoreCase))
        {
            return $"{name} ({variant})";
        }

        return $"{name} [Paint {paintId}]";
    }

    private static string ResolveCanonicalWeaponClass(int weaponDefIndex, string weaponName)
        => WeaponClassByDefindex.TryGetValue(weaponDefIndex, out var canonical)
            ? canonical
            : weaponName;

    private string DecoratePaintName(string language, string rawName, ushort paintId)
    {
        var name = NormalizePaintName(rawName);

        return TryResolvePaintVariant(language, paintId, out var variant)
               && !name.Contains(variant, StringComparison.OrdinalIgnoreCase)
            ? $"{name} ({variant})"
            : name;
    }

    private bool TryResolvePaintVariant(string language, ushort paintId, out string variant)
    {
        if (bridge.EconItemManager.GetPaintKits().TryGetValue(paintId, out var paintKit)
            && TryParsePaintVariant($"{paintKit.Name}|{paintKit.DescriptionString}|{paintKit.DescriptionTag}", out var variantKey))
        {
            variant = GetMenuLocaleText(language, variantKey, GetDefaultVariantText(variantKey));
            return true;
        }

        if (PaintVariantLocaleKeys.TryGetValue(paintId, out var fallbackKey))
        {
            variant = GetMenuLocaleText(language, fallbackKey, GetDefaultVariantText(fallbackKey));
            return true;
        }

        variant = string.Empty;
        return false;
    }

    private static bool TryParsePaintVariant(string source, out string variantKey)
    {
        var normalized = source
            .Replace('_', ' ')
            .Replace('-', ' ')
            .ToLowerInvariant();

        if (normalized.Contains("blackpearl", StringComparison.Ordinal)
            || normalized.Contains("black pearl", StringComparison.Ordinal))
        {
            variantKey = "ws.variant.black_pearl";
            return true;
        }

        if (normalized.Contains("sapphire", StringComparison.Ordinal))
        {
            variantKey = "ws.variant.sapphire";
            return true;
        }

        if (normalized.Contains("ruby", StringComparison.Ordinal))
        {
            variantKey = "ws.variant.ruby";
            return true;
        }

        if (normalized.Contains("emerald", StringComparison.Ordinal))
        {
            variantKey = "ws.variant.emerald";
            return true;
        }

        if (normalized.Contains("phase 1", StringComparison.Ordinal) || normalized.Contains("phase1", StringComparison.Ordinal))
        {
            variantKey = "ws.variant.phase_1";
            return true;
        }

        if (normalized.Contains("phase 2", StringComparison.Ordinal) || normalized.Contains("phase2", StringComparison.Ordinal))
        {
            variantKey = "ws.variant.phase_2";
            return true;
        }

        if (normalized.Contains("phase 3", StringComparison.Ordinal) || normalized.Contains("phase3", StringComparison.Ordinal))
        {
            variantKey = "ws.variant.phase_3";
            return true;
        }

        if (normalized.Contains("phase 4", StringComparison.Ordinal) || normalized.Contains("phase4", StringComparison.Ordinal))
        {
            variantKey = "ws.variant.phase_4";
            return true;
        }

        variantKey = string.Empty;
        return false;
    }

    private static string GetDefaultVariantText(string variantKey)
        => variantKey switch
        {
            "ws.variant.ruby" => "Ruby",
            "ws.variant.sapphire" => "Sapphire",
            "ws.variant.black_pearl" => "Black Pearl",
            "ws.variant.emerald" => "Emerald",
            "ws.variant.phase_1" => "Phase 1",
            "ws.variant.phase_2" => "Phase 2",
            "ws.variant.phase_3" => "Phase 3",
            "ws.variant.phase_4" => "Phase 4",
            _ => variantKey,
        };

    private static string NormalizePaintName(string rawName)
    {
        var name = NormalizeOptionName(rawName);

        if (name.StartsWith("?克 ", StringComparison.Ordinal))
        {
            name = $"??{name["?克 ".Length..]}";
        }

        return name;
    }

    private static IReadOnlyList<TOption> EnsureDistinctOptionNames<TOption>(
        IReadOnlyList<TOption> options,
        Func<TOption, string> getName,
        Func<TOption, ushort> getId,
        Func<TOption, string, TOption> withName)
    {
        var duplicateNames = options
            .GroupBy(getName, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToFrozenSet(StringComparer.OrdinalIgnoreCase);

        if (duplicateNames.Count == 0)
        {
            return options.ToArray();
        }

        return options
            .Select(option =>
            {
                var name = getName(option);
                return duplicateNames.Contains(name)
                    ? withName(option, $"{name} [ID {getId(option)}]")
                    : option;
            })
            .ToArray();
    }

    private static string NormalizeOptionName(string rawName)
    {
        var name = rawName.Trim();
        var pipeIndex = name.IndexOf('|');

        if (pipeIndex < 0)
        {
            return CollapseWhitespace(name);
        }

        var left = CollapseWhitespace(name[..pipeIndex]);
        var right = CollapseWhitespace(name[(pipeIndex + 1)..]);

        return string.IsNullOrWhiteSpace(left) ? right : $"{left} | {right}";
    }

    private static string CollapseWhitespace(string value)
        => string.Join(" ", value.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries));

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

    private string GetMenuLocaleText(string language, string key, string fallback)
    {
        var texts = _menuLocaleTexts ??= LoadMenuLocaleTexts();
        if (!texts.TryGetValue(key, out var translations))
        {
            return fallback;
        }

        foreach (var localeKey in EnumerateMenuLocaleKeys(language))
        {
            if (translations.TryGetValue(localeKey, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return fallback;
    }

    private Dictionary<string, Dictionary<string, string>> LoadMenuLocaleTexts()
    {
        var localePath = Path.Combine(bridge.ModuleDirectory, "locale", "WeaponSkin.Menu.json");
        if (!File.Exists(localePath))
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }

        using var document = JsonDocument.Parse(File.ReadAllText(localePath));
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in document.RootElement.EnumerateObject())
        {
            if (key.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var translations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var translation in key.Value.EnumerateObject())
            {
                translations[translation.Name] = translation.Value.GetString() ?? string.Empty;
            }

            result[key.Name] = translations;
        }

        return result;
    }

    private static IEnumerable<string> EnumerateMenuLocaleKeys(string language)
    {
        if (CatalogLanguageToLocaleKey.TryGetValue(language, out var localeKey))
        {
            yield return localeKey;
        }

        yield return "en-us";
    }

    private static readonly FrozenDictionary<string, string> CatalogLanguageToLocaleKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["bg"] = "bg-bg",
        ["cs"] = "cs-cz",
        ["da"] = "da-dk",
        ["de"] = "de-de",
        ["el"] = "el-gr",
        ["en"] = "en-us",
        ["es-ES"] = "es-es",
        ["es-MX"] = "es-419",
        ["fi"] = "fi-fi",
        ["fr"] = "fr-fr",
        ["hu"] = "hu-hu",
        ["id"] = "id-id",
        ["it"] = "it-it",
        ["ja"] = "ja-jp",
        ["ko"] = "ko-kr",
        ["no"] = "nb-no",
        ["nl"] = "nl-nl",
        ["pl"] = "pl-pl",
        ["pt-BR"] = "pt-br",
        ["pt-PT"] = "pt-pt",
        ["ro"] = "ro-ro",
        ["ru"] = "ru-ru",
        ["sv"] = "sv-se",
        ["th"] = "th-th",
        ["tr"] = "tr-tr",
        ["uk"] = "uk-ua",
        ["vi"] = "vi-vn",
        ["zh-CN"] = "zh-cn",
        ["zh-TW"] = "zh-tw",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

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

    private sealed record LocalizedCatalogBundle
    {
        [JsonPropertyName("skins")]
        public List<SkinData> Skins { get; init; } = [];

        [JsonPropertyName("gloves")]
        public List<GloveData> Gloves { get; init; } = [];

        [JsonPropertyName("music")]
        public List<NamedIdData> Music { get; init; } = [];

        [JsonPropertyName("collectibles")]
        public List<NamedIdData> Collectibles { get; init; } = [];
    }
}

