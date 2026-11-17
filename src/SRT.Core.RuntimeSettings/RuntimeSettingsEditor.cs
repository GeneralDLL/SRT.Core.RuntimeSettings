using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SRT.Core.RuntimeSettings.Database;

namespace SRT.Core.RuntimeSettings
{
    internal readonly record struct RuntimeSettingActor(string UserType, int Id, string FullName);

    internal sealed class EditorOutcome
    {
        public int Status { get; private init; }
        public string? Message { get; private init; }
        public object? Body { get; private init; }

        public static EditorOutcome Ok(object body) => new() { Status = 200, Body = body };
        public static EditorOutcome Fail(int status, string message) => new() { Status = status, Message = message };
    }

    internal sealed class RuntimeSettingsEditor
    {
        public const string SaveAction = "Save";
        public const string ResetAction = "Reset";

        private readonly RuntimeSettingsRegistry _registry;
        private readonly IRuntimeSettings _settings;

        public RuntimeSettingsEditor(RuntimeSettingsRegistry registry, IRuntimeSettings settings)
        {
            _registry = registry;
            _settings = settings;
        }

        public Task<EditorOutcome> SearchAsync(RuntimeSettingAudience audience, RuntimeSettingSearchRequest? request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            request ??= new RuntimeSettingSearchRequest();
            if (!TryPage(request.page, request.pageSize, out var page, out var pageSize, out var pageError))
                return Task.FromResult(EditorOutcome.Fail(400, pageError!));
            if (!TrySort(request.sortBy, request.sortDir, out var sortBy, out var ascending, out var sortError))
                return Task.FromResult(EditorOutcome.Fail(400, sortError!));

            var visible = _registry.All().Where(item => item.Audience == audience).ToList();
            var groups = visible.Select(item => item.Group).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray();
            IEnumerable<RuntimeSettingDefinition> filtered = visible;

            if (!string.IsNullOrWhiteSpace(request.group))
            {
                filtered = filtered.Where(item => item.Group.Equals(request.group, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(request.text))
            {
                filtered = filtered.Where(item =>
                    item.Key.Contains(request.text, StringComparison.OrdinalIgnoreCase)
                    || item.Title.Contains(request.text, StringComparison.OrdinalIgnoreCase)
                    || item.Description.Contains(request.text, StringComparison.OrdinalIgnoreCase));
            }

            var items = filtered.Select(ToItem).ToList();
            if (request.onlyOverridden)
                items = items.Where(item => item.isOverridden).ToList();

            items = sortBy switch
            {
                "group" => ascending
                    ? items.OrderBy(item => item.group, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.key, StringComparer.OrdinalIgnoreCase).ToList()
                    : items.OrderByDescending(item => item.group, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.key, StringComparer.OrdinalIgnoreCase).ToList(),
                "date_Update" => ascending
                    ? items.OrderBy(item => item.date_Update is null).ThenBy(item => item.date_Update).ToList()
                    : items.OrderBy(item => item.date_Update is null).ThenByDescending(item => item.date_Update).ToList(),
                _ => ascending
                    ? items.OrderBy(item => item.key, StringComparer.OrdinalIgnoreCase).ToList()
                    : items.OrderByDescending(item => item.key, StringComparer.OrdinalIgnoreCase).ToList()
            };

            return Task.FromResult(EditorOutcome.Ok(new RuntimeSettingSearchResult
            {
                items = items.Skip((page - 1) * pageSize).Take(pageSize).ToArray(),
                total = items.Count,
                page = page,
                pageSize = pageSize,
                groups = groups
            }));
        }

        public async Task<EditorOutcome> SaveAsync(
            RuntimeSettingAudience audience,
            RuntimeSettingSaveRequest? request,
            RuntimeSettingActor actor,
            CancellationToken cancellationToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.key) || !TryVisible(audience, request.key, out var definition))
                return EditorOutcome.Fail(404, "کلید پیدا نشد.");

            var shapeError = RuntimeSettingsValues.ValidateShape(definition, request.valueJson, out var element);
            if (shapeError is not null)
                return EditorOutcome.Fail(400, shapeError);

            var overlay = new OverlayRuntimeSettings(_settings, definition, request.valueJson!);
            var jsonError = RuntimeSettingsValues.ValidateJsonCallback(definition, element, overlay);
            if (jsonError is not null)
                return EditorOutcome.Fail(400, jsonError);

            foreach (var rule in _registry.Rules())
            {
                var message = rule(overlay);
                if (!string.IsNullOrWhiteSpace(message))
                    return EditorOutcome.Fail(400, message);
            }

            await using var db = _registry.CreateContext();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var row = await db.RuntimeSettings.SingleOrDefaultAsync(item => item.key == definition.Key, cancellationToken).ConfigureAwait(false);
            if (request.expectedDate_Update is DateTime expected)
            {
                if (row is null || !RuntimeSettingsValues.SameTimestamp(row.date_Update, expected))
                    return EditorOutcome.Fail(409, "این مقدار جای دیگر عوض شده است.");
            }

            var now = DateTime.UtcNow;
            var previous = row?.valueJson;
            if (row is null)
            {
                row = new RuntimeSettingRow { key = definition.Key };
                db.RuntimeSettings.Add(row);
            }

            row.valueJson = request.valueJson!;
            row.date_Update = now;
            row.userType_AdminChange = Cut(actor.UserType, 20);
            row.id_AdminChange = actor.Id;
            row.fullName_AdminChange = Cut(actor.FullName, 200);
            db.RuntimeSettingAudits.Add(new RuntimeSettingAuditRow
            {
                key = definition.Key,
                oldValueJson = previous,
                newValueJson = request.valueJson,
                action = SaveAction,
                userType = row.userType_AdminChange,
                idUser = actor.Id,
                fullNameUser = row.fullName_AdminChange,
                date_Create = now
            });
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _registry.Provider?.Load();
            return EditorOutcome.Ok(ToItem(definition));
        }

        public async Task<EditorOutcome> ResetAsync(
            RuntimeSettingAudience audience,
            RuntimeSettingResetRequest? request,
            RuntimeSettingActor actor,
            CancellationToken cancellationToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.key) || !TryVisible(audience, request.key, out var definition))
                return EditorOutcome.Fail(404, "کلید پیدا نشد.");

            await using var db = _registry.CreateContext();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            var row = await db.RuntimeSettings.SingleOrDefaultAsync(item => item.key == definition.Key, cancellationToken).ConfigureAwait(false);
            if (row is not null)
            {
                var now = DateTime.UtcNow;
                db.RuntimeSettingAudits.Add(new RuntimeSettingAuditRow
                {
                    key = definition.Key,
                    oldValueJson = row.valueJson,
                    newValueJson = null,
                    action = ResetAction,
                    userType = Cut(actor.UserType, 20),
                    idUser = actor.Id,
                    fullNameUser = Cut(actor.FullName, 200),
                    date_Create = now
                });
                db.RuntimeSettings.Remove(row);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                _registry.Provider?.Load();
            }

            return EditorOutcome.Ok(ToItem(definition));
        }

        public async Task<EditorOutcome> HistoryAsync(
            RuntimeSettingAudience audience,
            RuntimeSettingHistoryRequest? request,
            CancellationToken cancellationToken)
        {
            request ??= new RuntimeSettingHistoryRequest();
            if (!TryPage(request.page, request.pageSize, out var page, out var pageSize, out var pageError))
                return EditorOutcome.Fail(400, pageError!);

            var keys = _registry.All().Where(item => item.Audience == audience).Select(item => item.Key).ToArray();
            if (!string.IsNullOrWhiteSpace(request.key))
            {
                var match = keys.FirstOrDefault(item => item.Equals(request.key, StringComparison.OrdinalIgnoreCase));
                keys = match is null ? [] : [match];
            }

            if (keys.Length == 0)
            {
                return EditorOutcome.Ok(new RuntimeSettingHistoryResult
                {
                    items = [],
                    total = 0,
                    page = page,
                    pageSize = pageSize
                });
            }

            await using var db = _registry.CreateContext();
            var query = db.RuntimeSettingAudits.AsNoTracking().Where(item => keys.Contains(item.key));
            var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
            var rows = await query
                .OrderByDescending(item => item.date_Create)
                .ThenByDescending(item => item.id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return EditorOutcome.Ok(new RuntimeSettingHistoryResult
            {
                items = rows.Select(row => new RuntimeSettingHistoryItem
                {
                    id = row.id,
                    key = row.key,
                    oldValueJson = row.oldValueJson,
                    newValueJson = row.newValueJson,
                    action = row.action,
                    userType = row.userType,
                    idUser = row.idUser,
                    fullNameUser = row.fullNameUser,
                    date_Create = DateTime.SpecifyKind(row.date_Create, DateTimeKind.Utc)
                }).ToArray(),
                total = total,
                page = page,
                pageSize = pageSize
            });
        }

        private bool TryVisible(RuntimeSettingAudience audience, string key, out RuntimeSettingDefinition definition)
        {
            if (_registry.TryGet(key, out definition!) && definition.Audience == audience)
                return true;
            definition = null!;
            return false;
        }

        private RuntimeSettingItem ToItem(RuntimeSettingDefinition definition)
        {
            var baseline = _registry.Baseline ?? new ConfigurationBuilder().Build();
            var defaultJson = RuntimeSettingsValues.BaselineJson(definition, baseline);
            StoredSettingState? state = null;
            var known = _registry.Provider?.TryGetState(definition.Key, out state!) == true;
            var applied = known && state!.Applied;
            return new RuntimeSettingItem
            {
                key = definition.Key,
                group = definition.Group,
                title = definition.Title,
                description = definition.Description,
                type = definition.Type.ToString(),
                min = definition.Type switch
                {
                    RuntimeSettingType.Int => definition.IntMin,
                    RuntimeSettingType.Decimal => definition.DecimalMin,
                    _ => null
                },
                max = definition.Type switch
                {
                    RuntimeSettingType.Int => definition.IntMax,
                    RuntimeSettingType.Decimal => definition.DecimalMax,
                    _ => null
                },
                maxLength = definition.MaxLength,
                maxItems = definition.MaxItems,
                allowedValues = definition.AllowedValues,
                requiresRestart = definition.RequiresRestart,
                defaultValueJson = defaultJson,
                valueJson = applied ? state!.ValueJson : defaultJson,
                isOverridden = applied,
                hasInvalidStoredValue = known && state!.Invalid,
                date_Update = known ? DateTime.SpecifyKind(state!.UpdatedUtc, DateTimeKind.Utc) : null,
                fullName_AdminChange = known ? state!.FullName : null
            };
        }

        private static bool TryPage(int? pageValue, int? pageSizeValue, out int page, out int pageSize, out string? error)
        {
            page = pageValue ?? 1;
            pageSize = pageSizeValue ?? 20;
            if (page < 1 || pageSize < 1 || pageSize > 100)
            {
                error = "صفحه‌بندی نامعتبر است.";
                return false;
            }

            error = null;
            return true;
        }

        private static bool TrySort(string? sortByValue, string? sortDirValue, out string sortBy, out bool ascending, out string? error)
        {
            sortBy = string.IsNullOrWhiteSpace(sortByValue) ? "key" : sortByValue;
            var dir = string.IsNullOrWhiteSpace(sortDirValue) ? "asc" : sortDirValue;
            if (sortBy is not ("key" or "group" or "date_Update") || dir is not ("asc" or "desc"))
            {
                ascending = true;
                error = "مرتب‌سازی نامعتبر است.";
                return false;
            }

            ascending = dir == "asc";
            error = null;
            return true;
        }

        private static string Cut(string? value, int max)
        {
            value ??= "";
            return value.Length <= max ? value : value[..max];
        }
    }
}
