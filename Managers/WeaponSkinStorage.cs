using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Sharp.Shared.Enums;
using Sharp.Shared.Units;
using SqlSugar;
using WeaponSkin.Shared;

namespace WeaponSkin.Menu.Managers;

internal interface IWeaponSkinStorage
{
    Task<WeaponCosmetics[]> GetPlayerWeaponCosmetics(SteamID steamId);

    Task<TeamItem[]> GetPlayerTeamKnives(SteamID steamId);

    Task<TeamItem[]> GetPlayerTeamGloves(SteamID steamId);

    Task<TeamItem[]> GetPlayerTeamAgents(SteamID steamId);

    Task<TeamItem[]> GetPlayerTeamMusicKits(SteamID steamId);

    Task<TeamItem[]> GetPlayerTeamMedals(SteamID steamId);

    Task SaveWeaponCosmetic(SteamID steamId, WeaponCosmetics cosmetics);

    Task ClearWeaponCosmetic(SteamID steamId, EconItemId itemId);

    Task SaveKnife(SteamID steamId, CStrikeTeam team, EconItemId itemId);

    Task ClearKnife(SteamID steamId, CStrikeTeam team);

    Task SaveGloves(SteamID steamId, CStrikeTeam team, EconItemId itemId);

    Task ClearGloves(SteamID steamId, CStrikeTeam team);

    Task SaveAgent(SteamID steamId, CStrikeTeam team, ushort itemId);

    Task ClearAgent(SteamID steamId, CStrikeTeam team);

    Task SaveMusicKit(SteamID steamId, CStrikeTeam team, ushort itemId);

    Task ClearMusicKit(SteamID steamId, CStrikeTeam team);

    Task SaveMedal(SteamID steamId, CStrikeTeam team, ushort itemId);

    Task ClearMedal(SteamID steamId, CStrikeTeam team);
}

internal sealed class WeaponSkinStorage(
    InterfaceBridge bridge,
    ILogger<WeaponSkinStorage> logger) : IWeaponSkinStorage, IManager
{
    private const string ConfigFileName = "weaponskin.jsonc";
    private const string DefaultMySqlPort = "3306";
    private const string DefaultPostgreSqlPort = "5432";

    private static readonly JsonDocumentOptions JsonDocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    private SqlSugarScope? _db;

    public bool Init()
    {
        try
        {
            var configPath = Path.Combine(bridge.SharpPath, "configs", ConfigFileName);

            if (!File.Exists(configPath))
            {
                logger.LogError("WeaponSkin config was not found at {path}", configPath);
                return false;
            }

            var configuration = LoadConfiguration(configPath);
            var connectionString = BuildConnectionString(configuration);

            _db = new SqlSugarScope(new ConnectionConfig
            {
                ConnectionString = connectionString,
                DbType = configuration.DatabaseType,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
                MoreSettings = new ConnMoreSettings
                {
                    DisableNvarchar = true,
                },
                LanguageType = LanguageType.English,
            });

            InitializeTables();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize WeaponSkin storage.");
            return false;
        }
    }

    public void Shutdown()
    {
        _db?.Dispose();
    }

    public async Task<WeaponCosmetics[]> GetPlayerWeaponCosmetics(SteamID steamId)
    {
        var steamIdValue = ToSteamIdValue(steamId);
        var entities = await GetDatabase()
            .Queryable<WeaponCosmeticsEntity>()
            .Where(x => x.SteamId == steamIdValue)
            .ToArrayAsync()
            .ConfigureAwait(false);

        return entities.Select(MapToWeaponCosmetics).ToArray();
    }

    public Task<TeamItem[]> GetPlayerTeamKnives(SteamID steamId)
        => GetPlayerTeamItems<TeamKnifeEntity>(steamId);

    public Task<TeamItem[]> GetPlayerTeamGloves(SteamID steamId)
        => GetPlayerTeamGlovesInternal(steamId);

    public Task<TeamItem[]> GetPlayerTeamAgents(SteamID steamId)
        => GetPlayerTeamItems<TeamAgentEntity>(steamId);

    public Task<TeamItem[]> GetPlayerTeamMusicKits(SteamID steamId)
        => GetPlayerTeamItems<TeamMusicKitEntity>(steamId);

    public Task<TeamItem[]> GetPlayerTeamMedals(SteamID steamId)
        => GetPlayerTeamItems<TeamMedalEntity>(steamId);

    public Task SaveWeaponCosmetic(SteamID steamId, WeaponCosmetics cosmetics)
        => SaveWeaponCosmeticEntity(MapToEntity(ToSteamIdValue(steamId), cosmetics));

    public Task ClearWeaponCosmetic(SteamID steamId, EconItemId itemId)
    {
        var steamIdValue = ToSteamIdValue(steamId);
        return GetDatabase()
            .Deleteable<WeaponCosmeticsEntity>()
            .Where(x => x.SteamId == steamIdValue && x.ItemId == (int)itemId)
            .ExecuteCommandAsync();
    }

    public Task SaveKnife(SteamID steamId, CStrikeTeam team, EconItemId itemId)
        => SaveTeamItem(new TeamKnifeEntity
        {
            SteamId = ToSteamIdValue(steamId),
            Team = (int)team,
            ItemId = (int)itemId,
        });

    public Task ClearKnife(SteamID steamId, CStrikeTeam team)
        => ClearTeamItem<TeamKnifeEntity>(steamId, team);

    public async Task SaveGloves(SteamID steamId, CStrikeTeam team, EconItemId itemId)
    {
        var steamIdValue = ToSteamIdValue(steamId);
        var db = GetDatabase();
        db.Ado.BeginTran();

        try
        {
            await db.Deleteable<TeamGloveEntity>()
                .Where(x => x.SteamId == steamIdValue && x.Team == (int)team)
                .ExecuteCommandAsync()
                .ConfigureAwait(false);

            await db.Deleteable<MenuTeamGloveEntity>()
                .Where(x => x.SteamId == steamIdValue && x.Team == (int)team)
                .ExecuteCommandAsync()
                .ConfigureAwait(false);

            if (IsSupportedOriginalGlove((int)itemId))
            {
                await db.Insertable(new TeamGloveEntity
                    {
                        SteamId = steamIdValue,
                        Team = (int)team,
                        ItemId = (int)itemId,
                    })
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);
            }
            else
            {
                await db.Insertable(new MenuTeamGloveEntity
                    {
                        SteamId = steamIdValue,
                        Team = (int)team,
                        ItemId = (int)itemId,
                    })
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);
            }

            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    public async Task ClearGloves(SteamID steamId, CStrikeTeam team)
    {
        await ClearTeamItem<TeamGloveEntity>(steamId, team).ConfigureAwait(false);
        await ClearTeamItem<MenuTeamGloveEntity>(steamId, team).ConfigureAwait(false);
    }

    public Task SaveAgent(SteamID steamId, CStrikeTeam team, ushort itemId)
        => SaveTeamItem(new TeamAgentEntity
        {
            SteamId = ToSteamIdValue(steamId),
            Team = (int)team,
            ItemId = itemId,
        });

    public Task ClearAgent(SteamID steamId, CStrikeTeam team)
        => ClearTeamItem<TeamAgentEntity>(steamId, team);

    public Task SaveMusicKit(SteamID steamId, CStrikeTeam team, ushort itemId)
        => SaveTeamItem(new TeamMusicKitEntity
        {
            SteamId = ToSteamIdValue(steamId),
            Team = (int)team,
            ItemId = itemId,
        });

    public Task ClearMusicKit(SteamID steamId, CStrikeTeam team)
        => ClearTeamItem<TeamMusicKitEntity>(steamId, team);

    public Task SaveMedal(SteamID steamId, CStrikeTeam team, ushort itemId)
        => SaveTeamItem(new TeamMedalEntity
        {
            SteamId = ToSteamIdValue(steamId),
            Team = (int)team,
            ItemId = itemId,
        });

    public Task ClearMedal(SteamID steamId, CStrikeTeam team)
        => ClearTeamItem<TeamMedalEntity>(steamId, team);

    private void InitializeTables()
    {
        var db = GetDatabase();
        db.CodeFirst.InitTables<WeaponCosmeticsEntity>();
        db.CodeFirst.InitTables<TeamKnifeEntity>();
        db.CodeFirst.InitTables<TeamGloveEntity>();
        db.CodeFirst.InitTables<MenuTeamGloveEntity>();
        db.CodeFirst.InitTables<TeamAgentEntity>();
        db.CodeFirst.InitTables<TeamMusicKitEntity>();
        db.CodeFirst.InitTables<TeamMedalEntity>();
    }

    private static StorageConfiguration LoadConfiguration(string configPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(configPath), JsonDocumentOptions);
        var database = document.RootElement.GetProperty("Database");

        var type = database.TryGetProperty("Type", out var typeElement)
            ? typeElement.GetString()
            : null;
        var host = database.TryGetProperty("Host", out var hostElement)
            ? hostElement.GetString()
            : null;
        var port = database.TryGetProperty("Port", out var portElement)
            ? portElement.ToString()
            : null;
        var name = database.TryGetProperty("Database", out var databaseElement)
            ? databaseElement.GetString()
            : null;
        var user = database.TryGetProperty("User", out var userElement)
            ? userElement.GetString()
            : null;
        var password = database.TryGetProperty("Password", out var passwordElement)
            ? passwordElement.GetString()
            : null;

        return new StorageConfiguration(
            Enum.Parse<DbType>(type ?? "Sqlite", true),
            host,
            port,
            name,
            user,
            password);
    }

    private string BuildConnectionString(StorageConfiguration configuration)
    {
        return configuration.DatabaseType switch
        {
            DbType.Sqlite => BuildSqliteConnectionString(configuration.Database),
            DbType.MySql => BuildMySqlConnectionString(configuration),
            DbType.PostgreSQL => BuildPostgreSqlConnectionString(configuration),
            _ => throw new NotSupportedException($"WeaponSkin.Menu does not support database type '{configuration.DatabaseType}'."),
        };
    }

    private string BuildSqliteConnectionString(string? databaseName)
    {
        var dbFileName = string.IsNullOrWhiteSpace(databaseName) ? "weaponskin.db" : databaseName;

        if (string.IsNullOrEmpty(Path.GetExtension(dbFileName)))
        {
            dbFileName += ".db";
        }

        var dataDirectory = Path.Combine(bridge.SharpPath, "data");
        Directory.CreateDirectory(dataDirectory);

        var dbPath = Path.Combine(dataDirectory, dbFileName);
        logger.LogDebug("WeaponSkin.Menu is using SQLite database: {path}", dbPath);
        return $"Data Source={dbPath}";
    }

    private static string BuildMySqlConnectionString(StorageConfiguration configuration)
    {
        ValidateRequiredServerConfiguration(configuration);
        return $"Server={configuration.Host};Port={configuration.Port ?? DefaultMySqlPort};Database={configuration.Database};User={configuration.User};Password={configuration.Password};";
    }

    private static string BuildPostgreSqlConnectionString(StorageConfiguration configuration)
    {
        ValidateRequiredServerConfiguration(configuration);
        return $"Host={configuration.Host};Port={configuration.Port ?? DefaultPostgreSqlPort};Database={configuration.Database};Username={configuration.User};Password={configuration.Password};";
    }

    private static void ValidateRequiredServerConfiguration(StorageConfiguration configuration)
    {
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(configuration.Host))
        {
            missingFields.Add("Database:Host");
        }

        if (string.IsNullOrWhiteSpace(configuration.Database))
        {
            missingFields.Add("Database:Database");
        }

        if (string.IsNullOrWhiteSpace(configuration.User))
        {
            missingFields.Add("Database:User");
        }

        if (string.IsNullOrWhiteSpace(configuration.Password))
        {
            missingFields.Add("Database:Password");
        }

        if (missingFields.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException($"Missing required database configuration fields: {string.Join(", ", missingFields)}");
    }

    private async Task<TeamItem[]> GetPlayerTeamItems<TEntity>(SteamID steamId)
        where TEntity : class, ITeamSelectionEntity, new()
    {
        var steamIdValue = ToSteamIdValue(steamId);
        var entities = await GetDatabase()
            .Queryable<TEntity>()
            .Where(x => x.SteamId == steamIdValue)
            .ToArrayAsync()
            .ConfigureAwait(false);

        return entities
            .Select(entity => new TeamItem
            {
                Team = (CStrikeTeam)entity.Team,
                ItemId = (EconItemId)entity.ItemId,
            })
            .ToArray();
    }

    private async Task<TeamItem[]> GetPlayerTeamGlovesInternal(SteamID steamId)
    {
        var steamIdValue = ToSteamIdValue(steamId);
        var db = GetDatabase();

        var originalRows = await db.Queryable<TeamGloveEntity>()
            .Where(x => x.SteamId == steamIdValue)
            .ToArrayAsync()
            .ConfigureAwait(false);
        var sidecarRows = await db.Queryable<MenuTeamGloveEntity>()
            .Where(x => x.SteamId == steamIdValue)
            .ToArrayAsync()
            .ConfigureAwait(false);

        var supportedRows = originalRows.Where(static row => IsSupportedOriginalGlove(row.ItemId)).ToArray();
        var unsupportedRows = originalRows.Where(static row => !IsSupportedOriginalGlove(row.ItemId)).ToArray();

        if (unsupportedRows.Length > 0)
        {
            await MigrateUnsupportedLegacyGloves(db, unsupportedRows).ConfigureAwait(false);
        }

        var merged = new Dictionary<int, TeamItem>();

        foreach (var row in supportedRows)
        {
            merged[row.Team] = new TeamItem
            {
                Team = (CStrikeTeam)row.Team,
                ItemId = (EconItemId)row.ItemId,
            };
        }

        foreach (var row in unsupportedRows)
        {
            merged[row.Team] = new TeamItem
            {
                Team = (CStrikeTeam)row.Team,
                ItemId = (EconItemId)row.ItemId,
            };
        }

        foreach (var row in sidecarRows)
        {
            merged[row.Team] = new TeamItem
            {
                Team = (CStrikeTeam)row.Team,
                ItemId = (EconItemId)row.ItemId,
            };
        }

        return [.. merged.Values];
    }

    private static async Task MigrateUnsupportedLegacyGloves(SqlSugarScope db, TeamGloveEntity[] unsupportedRows)
    {
        db.Ado.BeginTran();

        try
        {
            foreach (var row in unsupportedRows)
            {
                await db.Deleteable<MenuTeamGloveEntity>()
                    .Where(x => x.SteamId == row.SteamId && x.Team == row.Team)
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                await db.Insertable(new MenuTeamGloveEntity
                    {
                        SteamId = row.SteamId,
                        Team = row.Team,
                        ItemId = row.ItemId,
                    })
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);
            }

            await db.Deleteable<TeamGloveEntity>()
                .Where(x => unsupportedRows.Select(row => row.Id).Contains(x.Id))
                .ExecuteCommandAsync()
                .ConfigureAwait(false);

            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    private async Task SaveWeaponCosmeticEntity(WeaponCosmeticsEntity entity)
    {
        var db = GetDatabase();
        db.Ado.BeginTran();

        try
        {
            await db.Deleteable<WeaponCosmeticsEntity>()
                .Where(x => x.SteamId == entity.SteamId && x.ItemId == entity.ItemId)
                .ExecuteCommandAsync()
                .ConfigureAwait(false);

            await db.Insertable(entity)
                .ExecuteCommandAsync()
                .ConfigureAwait(false);

            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    private async Task SaveTeamItem<TEntity>(TEntity entity)
        where TEntity : class, ITeamSelectionEntity, new()
    {
        var db = GetDatabase();
        db.Ado.BeginTran();

        try
        {
            await db.Deleteable<TEntity>()
                .Where(x => x.SteamId == entity.SteamId && x.Team == entity.Team)
                .ExecuteCommandAsync()
                .ConfigureAwait(false);

            await db.Insertable(entity)
                .ExecuteCommandAsync()
                .ConfigureAwait(false);

            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    private Task ClearTeamItem<TEntity>(SteamID steamId, CStrikeTeam team)
        where TEntity : class, ITeamSelectionEntity, new()
    {
        var steamIdValue = ToSteamIdValue(steamId);
        return GetDatabase()
            .Deleteable<TEntity>()
            .Where(x => x.SteamId == steamIdValue && x.Team == (int)team)
            .ExecuteCommandAsync();
    }

    private SqlSugarScope GetDatabase()
        => _db ?? throw new InvalidOperationException("WeaponSkin storage is not initialized.");

    private static ulong ToSteamIdValue(SteamID steamId)
        => steamId;

    private static bool IsSupportedOriginalGlove(int itemId)
        => Enum.IsDefined(typeof(EconGlovesId), (ushort)itemId);

    private static WeaponCosmetics MapToWeaponCosmetics(WeaponCosmeticsEntity entity)
    {
        return new WeaponCosmetics
        {
            ItemId = (EconItemId)entity.ItemId,
            PaintId = entity.PaintId,
            Wear = entity.Wear,
            Seed = entity.Seed,
            StatTrak = entity.StatTrak,
            NameTag = entity.NameTag ?? string.Empty,
            Stickers =
            [
                ParseSticker(entity.WeaponSticker0),
                ParseSticker(entity.WeaponSticker1),
                ParseSticker(entity.WeaponSticker2),
                ParseSticker(entity.WeaponSticker3),
                ParseSticker(entity.WeaponSticker4),
            ],
            Keychain = ParseKeychain(entity.WeaponKeychain),
        };
    }

    private static WeaponCosmeticsEntity MapToEntity(ulong steamId, WeaponCosmetics cosmetics)
    {
        return new WeaponCosmeticsEntity
        {
            SteamId = steamId,
            ItemId = (int)cosmetics.ItemId,
            PaintId = cosmetics.PaintId,
            Wear = cosmetics.Wear,
            Seed = cosmetics.Seed,
            StatTrak = cosmetics.StatTrak,
            NameTag = string.IsNullOrWhiteSpace(cosmetics.NameTag) ? null : cosmetics.NameTag,
            WeaponSticker0 = SerializeSticker(cosmetics.Stickers.ElementAtOrDefault(0)),
            WeaponSticker1 = SerializeSticker(cosmetics.Stickers.ElementAtOrDefault(1)),
            WeaponSticker2 = SerializeSticker(cosmetics.Stickers.ElementAtOrDefault(2)),
            WeaponSticker3 = SerializeSticker(cosmetics.Stickers.ElementAtOrDefault(3)),
            WeaponSticker4 = SerializeSticker(cosmetics.Stickers.ElementAtOrDefault(4)),
            WeaponKeychain = SerializeKeychain(cosmetics.Keychain),
        };
    }

    private static Sticker? ParseSticker(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(';');
        if (parts.Length != 7 || !int.TryParse(parts[0], out var stickerId) || stickerId == 0)
        {
            return null;
        }

        return new Sticker
        {
            StickerId = stickerId,
            Schema = int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var schema) ? schema : 0,
            OffsetX = float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetX) ? offsetX : 0f,
            OffsetY = float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var offsetY) ? offsetY : 0f,
            Wear = float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var wear) ? wear : 0f,
            Scale = float.TryParse(parts[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) ? scale : 1f,
            Rotation = float.TryParse(parts[6], NumberStyles.Float, CultureInfo.InvariantCulture, out var rotation) ? rotation : 0f,
        };
    }

    private static Keychain? ParseKeychain(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(';');
        if (parts.Length != 5 || !int.TryParse(parts[0], out var keychainId) || keychainId == 0)
        {
            return null;
        }

        return new Keychain
        {
            KeychainId = keychainId,
            X = float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : 0f,
            Y = float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ? y : 0f,
            Z = float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var z) ? z : 0f,
            Seed = float.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var seed) ? seed : 0f,
        };
    }

    private static string SerializeSticker(Sticker? sticker)
    {
        if (sticker is null || sticker.StickerId == 0)
        {
            return "0;0;0;0;0;0;0";
        }

        return string.Join(";",
            sticker.StickerId.ToString(CultureInfo.InvariantCulture),
            sticker.Schema.ToString(CultureInfo.InvariantCulture),
            sticker.OffsetX.ToString(CultureInfo.InvariantCulture),
            sticker.OffsetY.ToString(CultureInfo.InvariantCulture),
            sticker.Wear.ToString(CultureInfo.InvariantCulture),
            sticker.Scale.ToString(CultureInfo.InvariantCulture),
            sticker.Rotation.ToString(CultureInfo.InvariantCulture));
    }

    private static string SerializeKeychain(Keychain? keychain)
    {
        if (keychain is null || keychain.KeychainId == 0)
        {
            return "0;0;0;0;0";
        }

        return string.Join(";",
            keychain.KeychainId.ToString(CultureInfo.InvariantCulture),
            keychain.X.ToString(CultureInfo.InvariantCulture),
            keychain.Y.ToString(CultureInfo.InvariantCulture),
            keychain.Z.ToString(CultureInfo.InvariantCulture),
            keychain.Seed.ToString(CultureInfo.InvariantCulture));
    }

    private sealed record StorageConfiguration(
        DbType DatabaseType,
        string? Host,
        string? Port,
        string? Database,
        string? User,
        string? Password);
}

internal interface ITeamSelectionEntity
{
    ulong SteamId { get; set; }

    int Team { get; set; }

    int ItemId { get; set; }
}

[SugarTable("ws_weapon_cosmetics")]
[SugarIndex($"unique_{{table}}_{nameof(WeaponCosmeticsEntity.SteamId)}_{nameof(WeaponCosmeticsEntity.ItemId)}",
    nameof(WeaponCosmeticsEntity.SteamId), OrderByType.Asc,
    nameof(WeaponCosmeticsEntity.ItemId), OrderByType.Asc, IsUnique = true)]
internal sealed class WeaponCosmeticsEntity
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = false)]
    public ulong SteamId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int ItemId { get; set; }

    [SugarColumn(IsNullable = false)]
    public ushort PaintId { get; set; }

    [SugarColumn(IsNullable = false, ColumnDataType = "float")]
    public float Wear { get; set; }

    [SugarColumn(IsNullable = false, ColumnDataType = "float")]
    public float Seed { get; set; }

    [SugarColumn(IsNullable = true)]
    public int? StatTrak { get; set; }

    [SugarColumn(IsNullable = true, Length = 255)]
    public string? NameTag { get; set; }

    [SugarColumn(IsNullable = false, Length = 128)]
    public string WeaponSticker0 { get; set; } = "0;0;0;0;0;0;0";

    [SugarColumn(IsNullable = false, Length = 128)]
    public string WeaponSticker1 { get; set; } = "0;0;0;0;0;0;0";

    [SugarColumn(IsNullable = false, Length = 128)]
    public string WeaponSticker2 { get; set; } = "0;0;0;0;0;0;0";

    [SugarColumn(IsNullable = false, Length = 128)]
    public string WeaponSticker3 { get; set; } = "0;0;0;0;0;0;0";

    [SugarColumn(IsNullable = false, Length = 128)]
    public string WeaponSticker4 { get; set; } = "0;0;0;0;0;0;0";

    [SugarColumn(IsNullable = false, Length = 128)]
    public string WeaponKeychain { get; set; } = "0;0;0;0;0";
}

[SugarTable("ws_team_knives")]
[SugarIndex($"unique_{{table}}_{nameof(TeamKnifeEntity.SteamId)}_{nameof(TeamKnifeEntity.Team)}",
    nameof(TeamKnifeEntity.SteamId), OrderByType.Asc,
    nameof(TeamKnifeEntity.Team), OrderByType.Asc, IsUnique = true)]
internal sealed class TeamKnifeEntity : ITeamSelectionEntity
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = false)]
    public ulong SteamId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int Team { get; set; }

    [SugarColumn(IsNullable = false)]
    public int ItemId { get; set; }
}

[SugarTable("ws_team_gloves")]
[SugarIndex($"unique_{{table}}_{nameof(TeamGloveEntity.SteamId)}_{nameof(TeamGloveEntity.Team)}",
    nameof(TeamGloveEntity.SteamId), OrderByType.Asc,
    nameof(TeamGloveEntity.Team), OrderByType.Asc, IsUnique = true)]
internal sealed class TeamGloveEntity : ITeamSelectionEntity
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = false)]
    public ulong SteamId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int Team { get; set; }

    [SugarColumn(IsNullable = false)]
    public int ItemId { get; set; }
}

[SugarTable("ws_menu_team_gloves")]
[SugarIndex($"unique_{{table}}_{nameof(MenuTeamGloveEntity.SteamId)}_{nameof(MenuTeamGloveEntity.Team)}",
    nameof(MenuTeamGloveEntity.SteamId), OrderByType.Asc,
    nameof(MenuTeamGloveEntity.Team), OrderByType.Asc, IsUnique = true)]
internal sealed class MenuTeamGloveEntity : ITeamSelectionEntity
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = false)]
    public ulong SteamId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int Team { get; set; }

    [SugarColumn(IsNullable = false)]
    public int ItemId { get; set; }
}

[SugarTable("ws_team_agents")]
[SugarIndex($"unique_{{table}}_{nameof(TeamAgentEntity.SteamId)}_{nameof(TeamAgentEntity.Team)}",
    nameof(TeamAgentEntity.SteamId), OrderByType.Asc,
    nameof(TeamAgentEntity.Team), OrderByType.Asc, IsUnique = true)]
internal sealed class TeamAgentEntity : ITeamSelectionEntity
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = false)]
    public ulong SteamId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int Team { get; set; }

    [SugarColumn(IsNullable = false)]
    public int ItemId { get; set; }
}

[SugarTable("ws_team_musickits")]
[SugarIndex($"unique_{{table}}_{nameof(TeamMusicKitEntity.SteamId)}_{nameof(TeamMusicKitEntity.Team)}",
    nameof(TeamMusicKitEntity.SteamId), OrderByType.Asc,
    nameof(TeamMusicKitEntity.Team), OrderByType.Asc, IsUnique = true)]
internal sealed class TeamMusicKitEntity : ITeamSelectionEntity
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = false)]
    public ulong SteamId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int Team { get; set; }

    [SugarColumn(IsNullable = false)]
    public int ItemId { get; set; }
}

[SugarTable("ws_team_medals")]
[SugarIndex($"unique_{{table}}_{nameof(TeamMedalEntity.SteamId)}_{nameof(TeamMedalEntity.Team)}",
    nameof(TeamMedalEntity.SteamId), OrderByType.Asc,
    nameof(TeamMedalEntity.Team), OrderByType.Asc, IsUnique = true)]
internal sealed class TeamMedalEntity : ITeamSelectionEntity
{
    [SugarColumn(IsPrimaryKey = true, IsIdentity = true)]
    public int Id { get; set; }

    [SugarColumn(IsNullable = false)]
    public ulong SteamId { get; set; }

    [SugarColumn(IsNullable = false)]
    public int Team { get; set; }

    [SugarColumn(IsNullable = false)]
    public int ItemId { get; set; }
}
