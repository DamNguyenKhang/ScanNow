using FluentValidation;
using Microsoft.Extensions.Configuration;
using QRCoder;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.MenuManagement.DTOs;
using ScanNow.Application.Features.TableQr.DTOs;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;

namespace ScanNow.Application.Features.TableQr
{
    public class TableQrService : ITableQrService
    {
        private const int SessionCodeLength = 6;
        private static readonly char[] SessionCodeChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();
        private static readonly string OwnerRole = UserRole.OWNER.ToString();
        private static readonly string BranchManagerRole = UserRole.BRANCH_MANAGER.ToString();
        private static readonly string StaffRole = UserRole.STAFF.ToString();
        private static readonly string KitchenRole = UserRole.KITCHEN.ToString();

        private readonly ITableQrRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly IValidator<TableQuery> _tableQueryValidator;
        private readonly IValidator<CreateTableRequest> _createTableValidator;
        private readonly IValidator<UpdateTableRequest> _updateTableValidator;
        private readonly IValidator<UpdateTableStatusRequest> _statusValidator;
        private readonly IValidator<JoinSessionRequest> _joinSessionValidator;
        private readonly IValidator<MenuQuery> _menuQueryValidator;
        private readonly IConfiguration _configuration;

        public TableQrService(
            ITableQrRepository repository,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            IValidator<TableQuery> tableQueryValidator,
            IValidator<CreateTableRequest> createTableValidator,
            IValidator<UpdateTableRequest> updateTableValidator,
            IValidator<UpdateTableStatusRequest> statusValidator,
            IValidator<JoinSessionRequest> joinSessionValidator,
            IValidator<MenuQuery> menuQueryValidator,
            IConfiguration configuration)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _tableQueryValidator = tableQueryValidator;
            _createTableValidator = createTableValidator;
            _updateTableValidator = updateTableValidator;
            _statusValidator = statusValidator;
            _joinSessionValidator = joinSessionValidator;
            _menuQueryValidator = menuQueryValidator;
            _configuration = configuration;
        }

        public async Task<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>> GetManageTablesAsync(Guid branchId, TableQuery query)
        {
            await EnsureCanManageBranchAsync(branchId);
            return await GetTablesCoreAsync(branchId, query);
        }

        public async Task<TableResponse> GetManageTableAsync(Guid branchId, Guid tableId)
        {
            await EnsureCanManageBranchAsync(branchId);
            var table = await GetTableInBranchAsync(branchId, tableId);
            return MapTable(table);
        }

        public async Task<TableResponse> CreateTableAsync(Guid branchId, CreateTableRequest request)
        {
            await _createTableValidator.ValidateAndThrowAsync(request);
            await EnsureCanManageBranchAsync(branchId);

            var tableNumber = request.TableNumber.Trim();
            if (await _repository.TableNumberExistsAsync(branchId, tableNumber))
            {
                throw new ConflictException("Table number already exists in this branch");
            }

            var qrCodeToken = await GenerateUniqueQrCodeTokenAsync();
            var table = new RestaurantTable
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                TableNumber = tableNumber,
                Capacity = request.Capacity,
                QrCodeToken = qrCodeToken,
                QrCodeUrl = BuildQrCodeUrl(qrCodeToken),
                Status = TableStatus.AVAILABLE,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.AddTableAsync(table);
            await _unitOfWork.SaveChangesAsync();
            return MapTable(await GetTableOrThrowAsync(table.Id));
        }

        public async Task<TableResponse> UpdateTableAsync(Guid tableId, UpdateTableRequest request)
        {
            await _updateTableValidator.ValidateAndThrowAsync(request);
            var table = await GetManageTableOrThrowAsync(tableId);
            var tableNumber = request.TableNumber.Trim();

            if (await _repository.TableNumberExistsAsync(table.BranchId, tableNumber, table.Id))
            {
                throw new ConflictException("Table number already exists in this branch");
            }

            table.TableNumber = tableNumber;
            table.Capacity = request.Capacity;
            table.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return MapTable(table);
        }

        public async Task<TableResponse> UpdateTableStatusAsync(Guid tableId, UpdateTableStatusRequest request)
        {
            await _statusValidator.ValidateAndThrowAsync(request);
            var table = await GetManageTableOrThrowAsync(tableId);

            if (table.Status == TableStatus.OCCUPIED)
            {
                throw new BusinessRuleException("Occupied table status is managed by session flow");
            }

            table.Status = request.Status;
            table.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return MapTable(table);
        }

        public Task<TableResponse> ActivateTableAsync(Guid tableId)
        {
            return SetTableActiveAsync(tableId, true);
        }

        public Task<TableResponse> DeactivateTableAsync(Guid tableId)
        {
            return SetTableActiveAsync(tableId, false);
        }

        public async Task<TableResponse> RegenerateQrAsync(Guid tableId)
        {
            var table = await GetManageTableOrThrowAsync(tableId);
            var qrCodeToken = await GenerateUniqueQrCodeTokenAsync();
            table.QrCodeToken = qrCodeToken;
            table.QrCodeUrl = BuildQrCodeUrl(qrCodeToken);
            table.QrCodeImageUrl = null;
            table.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return MapTable(table);
        }

        public async Task<byte[]> GetQrImageAsync(Guid tableId)
        {
            var table = await GetManageTableOrThrowAsync(tableId);
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(table.QrCodeUrl ?? BuildQrCodeUrl(table.QrCodeToken), QRCodeGenerator.ECCLevel.Q);
            var qrCode = new PngByteQRCode(data);
            return qrCode.GetGraphic(12);
        }

        public async Task<QrSessionResponse> OpenTableAsync(Guid branchId, Guid tableId)
        {
            await EnsureCanWorkInBranchAsync(branchId);
            var table = await GetTableInBranchAsync(branchId, tableId);
            CloseExpiredActiveSessions(table);

            if (!table.IsActive)
            {
                throw new BusinessRuleException("Table is inactive");
            }

            if (table.Status is TableStatus.OCCUPIED or TableStatus.DISABLED)
            {
                throw new BusinessRuleException("Table cannot be opened");
            }

            if (await _repository.GetActiveSessionByTableIdAsync(table.Id) is not null)
            {
                throw new ConflictException("Table already has an active session");
            }

            var session = new QrSession
            {
                Id = Guid.NewGuid(),
                TableId = table.Id,
                BranchId = branchId,
                SessionToken = await GenerateUniqueSessionCodeAsync(),
                ExpiresAt = DateTime.UtcNow.AddHours(6),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            table.Status = TableStatus.OCCUPIED;
            table.UpdatedAt = DateTime.UtcNow;
            await _repository.AddSessionAsync(session);
            await _unitOfWork.SaveChangesAsync();
            return MapSession(session);
        }

        public async Task<QrSessionResponse> CloseSessionAsync(Guid sessionId)
        {
            var session = await _repository.GetSessionByIdAsync(sessionId)
                ?? throw new NotFoundException("Session not found");

            await EnsureCanWorkInBranchAsync(session.BranchId);
            if (!session.IsActive)
            {
                throw new BusinessRuleException("Session is already closed");
            }

            session.IsActive = false;
            session.UpdatedAt = DateTime.UtcNow;
            session.Table.Status = TableStatus.AVAILABLE;
            session.Table.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return MapSession(session);
        }

        public async Task<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>> GetMyTablesAsync(Guid branchId, TableQuery query)
        {
            await EnsureCanWorkInBranchAsync(branchId);
            return await GetTablesCoreAsync(branchId, query);
        }

        public async Task<TableResponse> GetMyTableAsync(Guid tableId)
        {
            var table = await GetTableOrThrowAsync(tableId);
            await EnsureCanWorkInBranchAsync(table.BranchId);
            return MapTable(table);
        }

        public async Task<PublicTableResponse> GetPublicTableAsync(string qrCodeToken)
        {
            var table = await _repository.GetTableByQrCodeTokenAsync(qrCodeToken)
                ?? throw new NotFoundException("Table not found");

            if (!table.IsActive || !table.Branch.IsActive || !table.Branch.Restaurant.IsActive)
            {
                throw new NotFoundException("Table not found");
            }

            return new PublicTableResponse
            {
                TableId = table.Id,
                BranchId = table.BranchId,
                BranchName = table.Branch.Name,
                TableNumber = table.TableNumber,
                Status = table.Status
            };
        }

        public async Task<JoinSessionResponse> JoinSessionAsync(JoinSessionRequest request)
        {
            await _joinSessionValidator.ValidateAndThrowAsync(request);
            var session = await GetActiveSessionByCodeOrThrowAsync(request.SessionCode.Trim().ToUpperInvariant());
            return MapJoinSession(session);
        }

        public async Task<SessionMenuResponse> GetSessionMenuAsync(string sessionCode, MenuQuery query)
        {
            await _menuQueryValidator.ValidateAndThrowAsync(query);
            ValidateMenuSort(query.SortBy);
            var session = await GetActiveSessionByCodeOrThrowAsync(sessionCode.Trim().ToUpperInvariant());
            return new SessionMenuResponse
            {
                Session = MapJoinSession(session),
                Menu = BuildMenu(session.BranchId, await _repository.GetCategoriesByBranchIdAsync(session.BranchId), await _repository.GetMenuItemsByBranchIdAsync(session.BranchId), query)
            };
        }

        public async Task<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>> GetAdminTablesAsync(Guid branchId, TableQuery query)
        {
            await GetBranchOrThrowAsync(branchId);
            return await GetTablesCoreAsync(branchId, query);
        }

        public async Task<TableResponse> GetAdminTableAsync(Guid branchId, Guid tableId)
        {
            await GetBranchOrThrowAsync(branchId);
            return MapTable(await GetTableInBranchAsync(branchId, tableId));
        }

        public async Task<IReadOnlyList<QrSessionResponse>> GetAdminSessionsAsync(Guid branchId)
        {
            await GetBranchOrThrowAsync(branchId);
            return (await _repository.GetSessionsByBranchIdAsync(branchId)).Select(MapSession).ToList();
        }

        private async Task<TableResponse> SetTableActiveAsync(Guid tableId, bool isActive)
        {
            var table = await GetManageTableOrThrowAsync(tableId);
            table.IsActive = isActive;
            table.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return MapTable(table);
        }

        private async Task<ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<TableResponse>> GetTablesCoreAsync(Guid branchId, TableQuery query)
        {
            await _tableQueryValidator.ValidateAndThrowAsync(query);
            ValidateTableSort(query.SortBy);
            IEnumerable<RestaurantTable> tables = await _repository.GetTablesByBranchIdAsync(branchId);

            if (query.Status.HasValue)
            {
                tables = tables.Where(x => x.Status == query.Status.Value);
            }

            if (query.Capacity.HasValue)
            {
                tables = tables.Where(x => x.Capacity == query.Capacity.Value);
            }

            if (query.IsActive.HasValue)
            {
                tables = tables.Where(x => x.IsActive == query.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                tables = tables.Where(x => x.TableNumber.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            tables = ApplyTableSort(tables, query);
            return ToPagedResult(tables.Select(MapTable), query.PageNumber, query.PageSize);
        }

        private async Task<RestaurantTable> GetManageTableOrThrowAsync(Guid tableId)
        {
            var table = await GetTableOrThrowAsync(tableId);
            await EnsureCanManageBranchAsync(table.BranchId);
            return table;
        }

        private async Task<RestaurantTable> GetTableOrThrowAsync(Guid tableId)
        {
            return await _repository.GetTableByIdAsync(tableId)
                ?? throw new NotFoundException("Table not found");
        }

        private async Task<RestaurantTable> GetTableInBranchAsync(Guid branchId, Guid tableId)
        {
            var table = await GetTableOrThrowAsync(tableId);
            if (table.BranchId != branchId)
            {
                throw new NotFoundException("Table not found");
            }

            return table;
        }

        private async Task<Branch> GetBranchOrThrowAsync(Guid branchId)
        {
            return await _repository.GetBranchByIdAsync(branchId)
                ?? throw new NotFoundException("Branch not found");
        }

        private async Task EnsureCanManageBranchAsync(Guid branchId)
        {
            var branch = await GetBranchOrThrowAsync(branchId);
            var userId = GetCurrentUserId();
            var role = _currentUserService.Role;

            if (role == OwnerRole && branch.Restaurant.OwnerId == userId)
            {
                return;
            }

            if (role == BranchManagerRole && branch.ManagerId == userId)
            {
                return;
            }

            throw new ForbiddenException();
        }

        private async Task EnsureCanWorkInBranchAsync(Guid branchId)
        {
            var branch = await GetBranchOrThrowAsync(branchId);
            if (!branch.IsActive || !branch.Restaurant.IsActive)
            {
                throw new BusinessRuleException("Branch is inactive");
            }

            var userId = GetCurrentUserId();
            var role = _currentUserService.Role;
            if ((role == StaffRole || role == KitchenRole) && await _repository.UserBelongsToBranchAsync(userId, branchId))
            {
                return;
            }

            throw new ForbiddenException();
        }

        private async Task<QrSession> GetActiveSessionByCodeOrThrowAsync(string sessionCode)
        {
            return await _repository.GetActiveSessionByCodeAsync(sessionCode)
                ?? throw new NotFoundException("Session not found or expired");
        }

        private static void CloseExpiredActiveSessions(RestaurantTable table)
        {
            var now = DateTime.UtcNow;
            var expiredSessions = table.QrSessions
                .Where(x => x.IsActive && x.ExpiresAt <= now)
                .ToList();

            if (expiredSessions.Count == 0)
            {
                return;
            }

            foreach (var session in expiredSessions)
            {
                session.IsActive = false;
                session.UpdatedAt = now;
            }

            if (table.Status == TableStatus.OCCUPIED && !table.QrSessions.Any(x => x.IsActive && x.ExpiresAt > now))
            {
                table.Status = TableStatus.AVAILABLE;
                table.UpdatedAt = now;
            }

        }

        private Guid GetCurrentUserId()
        {
            return _currentUserService.UserId ?? throw new UnauthorizedException();
        }

        private async Task<string> GenerateUniqueQrCodeTokenAsync()
        {
            for (var attempt = 0; attempt < 10; attempt++)
            {
                var token = Guid.NewGuid().ToString("N");
                if (!await _repository.QrCodeTokenExistsAsync(token))
                {
                    return token;
                }
            }

            throw new ConflictException("Unable to generate QR token");
        }

        private async Task<string> GenerateUniqueSessionCodeAsync()
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var code = new string(Enumerable.Range(0, SessionCodeLength)
                    .Select(_ => SessionCodeChars[Random.Shared.Next(SessionCodeChars.Length)])
                    .ToArray());

                if (!await _repository.ActiveSessionCodeExistsAsync(code))
                {
                    return code;
                }
            }

            throw new ConflictException("Unable to generate session code");
        }

        private string BuildQrCodeUrl(string qrCodeToken)
        {
            var frontendBaseUrl = _configuration["App:FrontendBaseUrl"] ?? _configuration["App:ClientUrl"];
            var tablePath = _configuration["App:QrTablePath"] ?? "/tables";

            if (string.IsNullOrWhiteSpace(frontendBaseUrl))
            {
                return $"{tablePath.TrimEnd('/')}/{qrCodeToken}";
            }

            return $"{frontendBaseUrl.TrimEnd('/')}/{tablePath.Trim('/')}/{qrCodeToken}";
        }

        private static TableResponse MapTable(RestaurantTable table)
        {
            return new TableResponse
            {
                TableId = table.Id,
                BranchId = table.BranchId,
                BranchName = table.Branch?.Name ?? string.Empty,
                TableNumber = table.TableNumber,
                Capacity = table.Capacity,
                QrCodeToken = table.QrCodeToken,
                QrCodeUrl = table.QrCodeUrl,
                QrCodeImageUrl = table.QrCodeImageUrl,
                Status = table.Status,
                IsActive = table.IsActive,
                CurrentSession = table.QrSessions
                    .Where(x => x.IsActive)
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault() is { } session ? MapSession(session) : null,
                CreatedAt = table.CreatedAt,
                UpdatedAt = table.UpdatedAt
            };
        }

        private static QrSessionResponse MapSession(QrSession session)
        {
            return new QrSessionResponse
            {
                SessionId = session.Id,
                TableId = session.TableId,
                BranchId = session.BranchId,
                SessionCode = session.SessionToken,
                IsActive = session.IsActive,
                ExpiresAt = session.ExpiresAt,
                CreatedAt = session.CreatedAt,
                UpdatedAt = session.UpdatedAt
            };
        }

        private static JoinSessionResponse MapJoinSession(QrSession session)
        {
            return new JoinSessionResponse
            {
                SessionId = session.Id,
                TableId = session.TableId,
                BranchId = session.BranchId,
                TableNumber = session.Table.TableNumber,
                BranchName = session.Branch.Name,
                ExpiresAt = session.ExpiresAt
            };
        }

        private static ScanNow.Application.Features.MenuManagement.DTOs.PagedResult<MenuCategoryResponse> BuildMenu(Guid branchId, IEnumerable<Category> categories, IEnumerable<MenuItem> items, MenuQuery query)
        {
            var filteredCategories = categories.Where(x => x.IsActive).ToList();
            IEnumerable<MenuItem> filteredItems = items.Where(x => x.BranchId == branchId && x.IsActive && x.IsAvailable && x.Category.IsActive);

            if (query.CategoryId.HasValue)
            {
                filteredItems = filteredItems.Where(x => x.CategoryId == query.CategoryId.Value);
            }

            if (query.IsFeatured.HasValue)
            {
                filteredItems = filteredItems.Where(x => x.IsFeatured == query.IsFeatured.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                filteredItems = filteredItems.Where(x =>
                    x.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (x.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) == true)
                    || x.Category.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            filteredItems = ApplyMenuSort(filteredItems, query);
            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var allItems = filteredItems.ToList();
            var pagedItems = allItems.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            return new ScanNow.Application.Features.MenuManagement.DTOs.PagedResult<MenuCategoryResponse>
            {
                Items = filteredCategories
                    .OrderBy(x => x.DisplayOrder)
                    .ThenBy(x => x.Name)
                    .Select(category => new MenuCategoryResponse
                    {
                        CategoryId = category.Id,
                        CategoryName = category.Name,
                        DisplayOrder = category.DisplayOrder,
                        Items = pagedItems
                            .Where(item => item.CategoryId == category.Id)
                            .Select(MapMenuItem)
                            .ToList()
                    })
                    .Where(x => x.Items.Count > 0)
                    .ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = allItems.Count
            };
        }

        private static MenuItemResponse MapMenuItem(MenuItem item)
        {
            return new MenuItemResponse
            {
                MenuItemId = item.Id,
                BranchId = item.BranchId,
                CategoryId = item.CategoryId,
                CategoryName = item.Category.Name,
                Name = item.Name,
                Description = item.Description,
                ImageUrl = item.ImageUrl,
                Price = item.Price,
                CostPrice = item.CostPrice,
                PreparationTime = item.PreparationTime,
                DisplayOrder = item.DisplayOrder,
                IsAvailable = item.IsAvailable,
                IsFeatured = item.IsFeatured,
                IsActive = item.IsActive,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            };
        }

        private static IEnumerable<RestaurantTable> ApplyTableSort(IEnumerable<RestaurantTable> tables, TableQuery query)
        {
            var desc = IsDesc(query.SortDirection);
            return query.SortBy?.ToLowerInvariant() switch
            {
                "tablenumber" => desc ? tables.OrderByDescending(x => x.TableNumber) : tables.OrderBy(x => x.TableNumber),
                "capacity" => desc ? tables.OrderByDescending(x => x.Capacity) : tables.OrderBy(x => x.Capacity),
                "status" => desc ? tables.OrderByDescending(x => x.Status) : tables.OrderBy(x => x.Status),
                "isactive" => desc ? tables.OrderByDescending(x => x.IsActive) : tables.OrderBy(x => x.IsActive),
                _ => desc ? tables.OrderByDescending(x => x.CreatedAt) : tables.OrderBy(x => x.CreatedAt)
            };
        }

        private static IEnumerable<MenuItem> ApplyMenuSort(IEnumerable<MenuItem> items, MenuQuery query)
        {
            var desc = IsDesc(query.SortDirection);
            return query.SortBy?.ToLowerInvariant() switch
            {
                "name" => desc ? items.OrderByDescending(x => x.Name) : items.OrderBy(x => x.Name),
                "price" => desc ? items.OrderByDescending(x => x.Price) : items.OrderBy(x => x.Price),
                "createdat" => desc ? items.OrderByDescending(x => x.CreatedAt) : items.OrderBy(x => x.CreatedAt),
                _ => desc ? items.OrderByDescending(x => x.DisplayOrder) : items.OrderBy(x => x.DisplayOrder)
            };
        }

        private static ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<T> ToPagedResult<T>(IEnumerable<T> source, int pageNumber, int pageSize)
        {
            var normalizedPageNumber = pageNumber <= 0 ? 1 : pageNumber;
            var normalizedPageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
            var items = source.ToList();
            return new ScanNow.Application.Features.RestaurantManagement.DTOs.PagedResult<T>
            {
                Items = items.Skip((normalizedPageNumber - 1) * normalizedPageSize).Take(normalizedPageSize).ToList(),
                PageNumber = normalizedPageNumber,
                PageSize = normalizedPageSize,
                TotalItems = items.Count
            };
        }

        private static void ValidateTableSort(string? sortBy)
        {
            ValidateSort(sortBy, ["tableNumber", "capacity", "status", "createdAt", "isActive"]);
        }

        private static void ValidateMenuSort(string? sortBy)
        {
            ValidateSort(sortBy, ["name", "price", "displayOrder", "createdAt"]);
        }

        private static void ValidateSort(string? sortBy, IReadOnlyCollection<string> allowed)
        {
            if (!string.IsNullOrWhiteSpace(sortBy) && !allowed.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            {
                throw new ScanNow.Domain.Exceptions.ValidationException("sortBy", "Invalid sort field");
            }
        }

        private static bool IsDesc(string? direction) => direction?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true;
    }
}
