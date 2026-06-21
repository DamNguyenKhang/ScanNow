using AutoMapper;
using FluentValidation;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.MenuManagement.DTOs;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;

namespace ScanNow.Application.Features.MenuManagement
{
    public class MenuManagementService : IMenuManagementService
    {
        private static readonly string OwnerRole = UserRole.OWNER.ToString();
        private static readonly string BranchManagerRole = UserRole.BRANCH_MANAGER.ToString();
        private static readonly string StaffRole = UserRole.STAFF.ToString();
        private static readonly string CashierRole = UserRole.CASHIER.ToString();
        private static readonly string KitchenRole = UserRole.KITCHEN.ToString();

        private readonly IMenuManagementRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly IMapper _mapper;
        private readonly IValidator<CategoryQuery> _categoryQueryValidator;
        private readonly IValidator<MenuQuery> _menuQueryValidator;
        private readonly IValidator<CreateCategoryRequest> _createCategoryValidator;
        private readonly IValidator<UpdateCategoryRequest> _updateCategoryValidator;
        private readonly IValidator<CreateMenuItemRequest> _createMenuItemValidator;
        private readonly IValidator<UpdateMenuItemRequest> _updateMenuItemValidator;
        private readonly IValidator<ReorderCategoryRequest> _reorderCategoryValidator;
        private readonly IValidator<ReorderMenuItemRequest> _reorderMenuItemValidator;
        private readonly IValidator<UpdateMenuItemPriceRequest> _priceValidator;
        private readonly IValidator<BulkAvailabilityRequest> _bulkAvailabilityValidator;

        public MenuManagementService(
            IMenuManagementRepository repository,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            IMapper mapper,
            IValidator<CategoryQuery> categoryQueryValidator,
            IValidator<MenuQuery> menuQueryValidator,
            IValidator<CreateCategoryRequest> createCategoryValidator,
            IValidator<UpdateCategoryRequest> updateCategoryValidator,
            IValidator<CreateMenuItemRequest> createMenuItemValidator,
            IValidator<UpdateMenuItemRequest> updateMenuItemValidator,
            IValidator<ReorderCategoryRequest> reorderCategoryValidator,
            IValidator<ReorderMenuItemRequest> reorderMenuItemValidator,
            IValidator<UpdateMenuItemPriceRequest> priceValidator,
            IValidator<BulkAvailabilityRequest> bulkAvailabilityValidator)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _mapper = mapper;
            _categoryQueryValidator = categoryQueryValidator;
            _menuQueryValidator = menuQueryValidator;
            _createCategoryValidator = createCategoryValidator;
            _updateCategoryValidator = updateCategoryValidator;
            _createMenuItemValidator = createMenuItemValidator;
            _updateMenuItemValidator = updateMenuItemValidator;
            _reorderCategoryValidator = reorderCategoryValidator;
            _reorderMenuItemValidator = reorderMenuItemValidator;
            _priceValidator = priceValidator;
            _bulkAvailabilityValidator = bulkAvailabilityValidator;
        }

        public async Task<PagedResult<CategoryResponse>> GetAdminCategoriesAsync(Guid branchId, CategoryQuery query)
        {
            await ValidateCategoryQueryAsync(query);
            await GetBranchOrThrowAsync(branchId);
            return ToPagedResult(ApplyCategorySort(ApplyCategoryFilters(await _repository.GetCategoriesByBranchIdAsync(branchId), query), query)
                .Select(_mapper.Map<CategoryResponse>), query.PageNumber, query.PageSize);
        }

        public async Task<CategoryResponse> GetAdminCategoryAsync(Guid branchId, Guid id)
        {
            await GetBranchOrThrowAsync(branchId);
            var category = await GetCategoryInBranchAsync(branchId, id);
            return _mapper.Map<CategoryResponse>(category);
        }

        public async Task<PagedResult<MenuItemResponse>> GetAdminMenuItemsAsync(Guid branchId, MenuQuery query)
        {
            await ValidateMenuQueryAsync(query);
            await GetBranchOrThrowAsync(branchId);
            return ToPagedResult(ApplyMenuItemSort(ApplyMenuItemFilters(await _repository.GetMenuItemsByBranchIdAsync(branchId), query), query)
                .Select(_mapper.Map<MenuItemResponse>), query.PageNumber, query.PageSize);
        }

        public async Task<MenuItemResponse> GetAdminMenuItemAsync(Guid id)
        {
            return _mapper.Map<MenuItemResponse>(await GetMenuItemOrThrowAsync(id));
        }

        public async Task<IReadOnlyList<PriceHistoryResponse>> GetAdminPriceHistoryAsync(Guid id)
        {
            await GetMenuItemOrThrowAsync(id);
            return (await _repository.GetPriceHistoriesByMenuItemIdAsync(id))
                .Select(_mapper.Map<PriceHistoryResponse>)
                .ToList();
        }

        public async Task<PagedResult<CategoryResponse>> GetManageCategoriesAsync(Guid branchId, CategoryQuery query)
        {
            await EnsureCanManageBranchAsync(branchId);
            return await GetAdminCategoriesAsync(branchId, query);
        }

        public async Task<CategoryResponse> GetManageCategoryAsync(Guid branchId, Guid id)
        {
            await EnsureCanManageBranchAsync(branchId);
            return await GetAdminCategoryAsync(branchId, id);
        }

        public async Task<CategoryResponse> CreateCategoryAsync(Guid branchId, CreateCategoryRequest request)
        {
            await _createCategoryValidator.ValidateAndThrowAsync(request);
            await EnsureCanManageBranchAsync(branchId);

            var category = new Category
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                Name = request.Name.Trim(),
                Description = TrimToNull(request.Description),
                ImageUrl = TrimToNull(request.ImageUrl),
                DisplayOrder = request.DisplayOrder,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.AddCategoryAsync(category);
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<CategoryResponse>(await GetCategoryInBranchAsync(branchId, category.Id));
        }

        public async Task<CategoryResponse> UpdateCategoryAsync(Guid branchId, Guid id, UpdateCategoryRequest request)
        {
            await _updateCategoryValidator.ValidateAndThrowAsync(request);
            await EnsureCanManageBranchAsync(branchId);

            var category = await GetCategoryInBranchAsync(branchId, id);
            category.Name = request.Name.Trim();
            category.Description = TrimToNull(request.Description);
            category.ImageUrl = TrimToNull(request.ImageUrl);
            category.DisplayOrder = request.DisplayOrder;
            category.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<CategoryResponse>(category);
        }

        public async Task<IReadOnlyList<CategoryResponse>> ReorderCategoriesAsync(Guid branchId, ReorderCategoryRequest request)
        {
            await _reorderCategoryValidator.ValidateAndThrowAsync(request);
            await EnsureCanManageBranchAsync(branchId);

            var categories = await _repository.GetCategoriesByBranchIdAsync(branchId);
            var byId = categories.ToDictionary(x => x.Id);
            foreach (var item in request.Items)
            {
                if (!byId.TryGetValue(item.Id, out var category))
                {
                    throw new NotFoundException("Category not found");
                }

                category.DisplayOrder = item.DisplayOrder;
                category.UpdatedAt = DateTime.UtcNow;
            }

            await _unitOfWork.SaveChangesAsync();
            return categories.OrderBy(x => x.DisplayOrder).Select(_mapper.Map<CategoryResponse>).ToList();
        }

        public async Task<CategoryResponse> SetCategoryActiveAsync(Guid branchId, Guid id, bool isActive)
        {
            await EnsureCanManageBranchAsync(branchId);
            var category = await GetCategoryInBranchAsync(branchId, id);
            category.IsActive = isActive;
            category.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<CategoryResponse>(category);
        }

        public async Task<PagedResult<MenuItemResponse>> GetManageMenuItemsAsync(Guid branchId, MenuQuery query)
        {
            await EnsureCanManageBranchAsync(branchId);
            return await GetAdminMenuItemsAsync(branchId, query);
        }

        public async Task<MenuItemResponse> GetManageMenuItemAsync(Guid id)
        {
            var item = await GetMenuItemOrThrowAsync(id);
            await EnsureCanManageBranchAsync(item.BranchId);
            return _mapper.Map<MenuItemResponse>(item);
        }

        public async Task<MenuItemResponse> CreateMenuItemAsync(Guid branchId, Guid categoryId, CreateMenuItemRequest request)
        {
            await _createMenuItemValidator.ValidateAndThrowAsync(request);
            await EnsureCanManageBranchAsync(branchId);
            await GetCategoryInBranchAsync(branchId, categoryId);

            var item = new MenuItem
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                CategoryId = categoryId,
                Name = request.Name.Trim(),
                Description = TrimToNull(request.Description),
                ImageUrl = TrimToNull(request.ImageUrl),
                Price = request.Price,
                CostPrice = request.CostPrice,
                PreparationTime = request.PreparationTime,
                DisplayOrder = request.DisplayOrder,
                IsAvailable = request.IsAvailable,
                IsFeatured = request.IsFeatured,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.AddMenuItemAsync(item);
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<MenuItemResponse>(await GetMenuItemOrThrowAsync(item.Id));
        }

        public async Task<MenuItemResponse> UpdateMenuItemAsync(Guid id, UpdateMenuItemRequest request)
        {
            await _updateMenuItemValidator.ValidateAndThrowAsync(request);
            var item = await GetMenuItemOrThrowAsync(id);
            await EnsureCanManageBranchAsync(item.BranchId);
            await GetCategoryInBranchAsync(item.BranchId, request.CategoryId);

            item.CategoryId = request.CategoryId;
            item.Name = request.Name.Trim();
            item.Description = TrimToNull(request.Description);
            item.ImageUrl = TrimToNull(request.ImageUrl);
            item.CostPrice = request.CostPrice;
            item.PreparationTime = request.PreparationTime;
            item.DisplayOrder = request.DisplayOrder;
            item.IsAvailable = request.IsAvailable;
            item.IsFeatured = request.IsFeatured;
            item.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<MenuItemResponse>(await GetMenuItemOrThrowAsync(id));
        }

        public async Task<MenuItemResponse> SetMenuItemActiveAsync(Guid id, bool isActive)
        {
            var item = await GetMenuItemOrThrowAsync(id);
            await EnsureCanManageBranchAsync(item.BranchId);
            item.IsActive = isActive;
            item.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<MenuItemResponse>(item);
        }

        public async Task<IReadOnlyList<MenuItemResponse>> ReorderMenuItemsAsync(Guid branchId, ReorderMenuItemRequest request)
        {
            await _reorderMenuItemValidator.ValidateAndThrowAsync(request);
            await EnsureCanManageBranchAsync(branchId);

            var items = await _repository.GetMenuItemsByBranchIdAsync(branchId);
            var byId = items.ToDictionary(x => x.Id);
            foreach (var item in request.Items)
            {
                if (!byId.TryGetValue(item.Id, out var menuItem))
                {
                    throw new NotFoundException("Menu item not found");
                }

                menuItem.DisplayOrder = item.DisplayOrder;
                menuItem.UpdatedAt = DateTime.UtcNow;
            }

            await _unitOfWork.SaveChangesAsync();
            return items.OrderBy(x => x.DisplayOrder).Select(_mapper.Map<MenuItemResponse>).ToList();
        }

        public async Task<MenuItemResponse> ToggleMenuItemAvailableAsync(Guid id)
        {
            var item = await GetMenuItemOrThrowAsync(id);
            await EnsureCanManageBranchAsync(item.BranchId);
            return await ToggleAvailableCoreAsync(item);
        }

        public async Task<IReadOnlyList<MenuItemResponse>> BulkUpdateAvailabilityAsync(Guid branchId, BulkAvailabilityRequest request)
        {
            await _bulkAvailabilityValidator.ValidateAndThrowAsync(request);
            await EnsureCanManageBranchAsync(branchId);
            return await BulkAvailabilityCoreAsync(branchId, request);
        }

        public async Task<MenuItemResponse> ToggleMenuItemFeaturedAsync(Guid id)
        {
            var item = await GetMenuItemOrThrowAsync(id);
            await EnsureCanManageBranchAsync(item.BranchId);
            item.IsFeatured = !item.IsFeatured;
            item.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<MenuItemResponse>(item);
        }

        public async Task<MenuItemResponse> UpdateMenuItemPriceAsync(Guid id, UpdateMenuItemPriceRequest request)
        {
            await _priceValidator.ValidateAndThrowAsync(request);
            var item = await GetMenuItemOrThrowAsync(id);
            await EnsureCanManageBranchAsync(item.BranchId);

            var oldPrice = item.Price;
            item.Price = request.Price;
            item.UpdatedAt = DateTime.UtcNow;

            await _repository.AddPriceHistoryAsync(new MenuItemPriceHistory
            {
                Id = Guid.NewGuid(),
                MenuItemId = item.Id,
                OldPrice = oldPrice,
                NewPrice = request.Price,
                ChangedById = GetCurrentUserId(),
                ChangedAt = DateTime.UtcNow,
                Note = TrimToNull(request.Note)
            });

            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<MenuItemResponse>(item);
        }

        public async Task<IReadOnlyList<PriceHistoryResponse>> GetManagePriceHistoryAsync(Guid id)
        {
            var item = await GetMenuItemOrThrowAsync(id);
            await EnsureCanManageBranchAsync(item.BranchId);
            return await GetAdminPriceHistoryAsync(id);
        }

        public async Task<PagedResult<MenuCategoryResponse>> GetMyBranchMenuAsync(Guid branchId, MenuQuery query)
        {
            await ValidateMenuQueryAsync(query);
            await EnsureCanWorkInBranchAsync(branchId);
            var branch = await GetActiveBranchForMenuAsync(branchId);
            return await BuildMenuAsync(branch.Id, onlyActiveAndAvailable: true, query);
        }

        public async Task<MenuItemResponse> GetMyMenuItemAsync(Guid id)
        {
            var item = await GetMenuItemOrThrowAsync(id);
            await EnsureCanWorkInBranchAsync(item.BranchId);
            return _mapper.Map<MenuItemResponse>(item);
        }

        public async Task<MenuItemResponse> ToggleMyMenuItemAvailableAsync(Guid id)
        {
            var item = await GetMenuItemOrThrowAsync(id);
            await EnsureCanWorkInBranchAsync(item.BranchId);
            return await ToggleAvailableCoreAsync(item);
        }

        public async Task<IReadOnlyList<MenuItemResponse>> BulkUpdateMyAvailabilityAsync(Guid branchId, BulkAvailabilityRequest request)
        {
            await _bulkAvailabilityValidator.ValidateAndThrowAsync(request);
            await EnsureCanWorkInBranchAsync(branchId);
            return await BulkAvailabilityCoreAsync(branchId, request);
        }

        public async Task<PagedResult<MenuCategoryResponse>> GetPublicBranchMenuAsync(Guid branchId, MenuQuery query)
        {
            await ValidateMenuQueryAsync(query);
            var branch = await GetActiveBranchForMenuAsync(branchId);
            return await BuildMenuAsync(branch.Id, onlyActiveAndAvailable: true, query);
        }

        public async Task<IReadOnlyList<CategoryResponse>> GetPublicBranchCategoriesAsync(Guid branchId)
        {
            var branch = await GetActiveBranchForMenuAsync(branchId);
            return (await _repository.GetCategoriesByBranchIdAsync(branch.Id))
                .Where(x => x.IsActive)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(_mapper.Map<CategoryResponse>)
                .ToList();
        }

        public async Task<MenuItemResponse> GetPublicMenuItemAsync(Guid branchId, Guid id)
        {
            await GetActiveBranchForMenuAsync(branchId);
            var item = await GetMenuItemOrThrowAsync(id);
            if (item.BranchId != branchId || !item.IsActive || !item.IsAvailable || !item.Category.IsActive)
            {
                throw new NotFoundException("Menu item not found");
            }

            return _mapper.Map<MenuItemResponse>(item);
        }

        private async Task<Branch> GetBranchOrThrowAsync(Guid branchId)
        {
            return await _repository.GetBranchByIdAsync(branchId)
                ?? throw new NotFoundException("Branch not found");
        }

        private async Task<Branch> GetActiveBranchForMenuAsync(Guid branchId)
        {
            var branch = await GetBranchOrThrowAsync(branchId);
            if (!branch.IsActive || !branch.Restaurant.IsActive)
            {
                throw new BusinessRuleException("Nhà hàng hiện đang tạm ngưng hoạt động");
            }

            return branch;
        }

        private async Task<Category> GetCategoryInBranchAsync(Guid branchId, Guid categoryId)
        {
            var category = await _repository.GetCategoryByIdAsync(categoryId)
                ?? throw new NotFoundException("Category not found");

            if (category.BranchId != branchId)
            {
                throw new NotFoundException("Category not found");
            }

            return category;
        }

        private async Task<MenuItem> GetMenuItemOrThrowAsync(Guid id)
        {
            return await _repository.GetMenuItemByIdAsync(id)
                ?? throw new NotFoundException("Menu item not found");
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
            var branch = await GetActiveBranchForMenuAsync(branchId);
            var userId = GetCurrentUserId();
            var role = _currentUserService.Role;

            if ((role == StaffRole || role == CashierRole || role == KitchenRole) && await _repository.UserBelongsToBranchAsync(userId, branch.Id))
            {
                return;
            }

            throw new ForbiddenException();
        }

        private async Task<MenuItemResponse> ToggleAvailableCoreAsync(MenuItem item)
        {
            item.IsAvailable = !item.IsAvailable;
            item.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return _mapper.Map<MenuItemResponse>(item);
        }

        private async Task<IReadOnlyList<MenuItemResponse>> BulkAvailabilityCoreAsync(Guid branchId, BulkAvailabilityRequest request)
        {
            var items = await _repository.GetMenuItemsByBranchIdAsync(branchId);
            var ids = request.MenuItemIds.ToHashSet();
            var selectedItems = items.Where(x => ids.Contains(x.Id)).ToList();
            if (selectedItems.Count != ids.Count)
            {
                throw new NotFoundException("Menu item not found");
            }

            foreach (var item in selectedItems)
            {
                item.IsAvailable = request.IsAvailable;
                item.UpdatedAt = DateTime.UtcNow;
            }

            await _unitOfWork.SaveChangesAsync();
            return selectedItems.Select(_mapper.Map<MenuItemResponse>).ToList();
        }

        private async Task<PagedResult<MenuCategoryResponse>> BuildMenuAsync(Guid branchId, bool onlyActiveAndAvailable, MenuQuery query)
        {
            var categories = await _repository.GetCategoriesByBranchIdAsync(branchId);
            IEnumerable<MenuItem> items = await _repository.GetMenuItemsByBranchIdAsync(branchId);

            if (onlyActiveAndAvailable)
            {
                categories = categories.Where(x => x.IsActive).ToList();
                items = items.Where(x => x.IsActive && x.IsAvailable && x.Category.IsActive).ToList();
            }

            items = ApplyMenuItemFilters(items, query);
            items = ApplyMenuItemSort(items, query);

            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var allItems = items.ToList();
            var pagedItems = allItems
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var groupedItems = categories
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Name)
                .Select(category => new MenuCategoryResponse
                {
                    CategoryId = category.Id,
                    CategoryName = category.Name,
                    DisplayOrder = category.DisplayOrder,
                    Items = pagedItems
                        .Where(item => item.CategoryId == category.Id)
                        .Select(_mapper.Map<MenuItemResponse>)
                        .ToList()
                })
                .Where(x => x.Items.Count > 0)
                .ToList();

            return new PagedResult<MenuCategoryResponse>
            {
                Items = groupedItems,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = allItems.Count
            };
        }

        private async Task ValidateCategoryQueryAsync(CategoryQuery query)
        {
            await _categoryQueryValidator.ValidateAndThrowAsync(query);
            ValidateSort(query.SortBy, ["name", "displayOrder", "createdAt", "isActive"]);
        }

        private async Task ValidateMenuQueryAsync(MenuQuery query)
        {
            await _menuQueryValidator.ValidateAndThrowAsync(query);
            ValidateSort(query.SortBy, ["name", "price", "displayOrder", "createdAt", "isActive", "isAvailable", "isFeatured"]);
        }

        private static IEnumerable<Category> ApplyCategoryFilters(IEnumerable<Category> categories, CategoryQuery query)
        {
            if (query.IsActive.HasValue)
            {
                categories = categories.Where(x => x.IsActive == query.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                categories = categories.Where(x => Contains(x.Name, search) || Contains(x.Description, search));
            }

            return categories;
        }

        private static IEnumerable<MenuItem> ApplyMenuItemFilters(IEnumerable<MenuItem> items, MenuQuery query)
        {
            var categoryIds = GetCategoryIds(query);
            if (categoryIds.Count > 0)
            {
                items = items.Where(x => categoryIds.Contains(x.CategoryId));
            }

            if (query.IsActive.HasValue)
            {
                items = items.Where(x => x.IsActive == query.IsActive.Value);
            }

            if (query.IsAvailable.HasValue)
            {
                items = items.Where(x => x.IsAvailable == query.IsAvailable.Value);
            }

            if (query.IsFeatured.HasValue)
            {
                items = items.Where(x => x.IsFeatured == query.IsFeatured.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                items = items.Where(x => Contains(x.Name, search) || Contains(x.Description, search) || Contains(x.Category.Name, search));
            }

            return items;
        }

        private static IEnumerable<Category> ApplyCategorySort(IEnumerable<Category> categories, CategoryQuery query)
        {
            var desc = IsDesc(query.SortDirection);
            return query.SortBy?.ToLowerInvariant() switch
            {
                "name" => desc ? categories.OrderByDescending(x => x.Name) : categories.OrderBy(x => x.Name),
                "isactive" => desc ? categories.OrderByDescending(x => x.IsActive) : categories.OrderBy(x => x.IsActive),
                "createdat" => desc ? categories.OrderByDescending(x => x.CreatedAt) : categories.OrderBy(x => x.CreatedAt),
                _ => desc ? categories.OrderByDescending(x => x.DisplayOrder) : categories.OrderBy(x => x.DisplayOrder)
            };
        }

        private static IEnumerable<MenuItem> ApplyMenuItemSort(IEnumerable<MenuItem> items, MenuQuery query)
        {
            var desc = IsDesc(query.SortDirection);
            return query.SortBy?.ToLowerInvariant() switch
            {
                "name" => desc ? items.OrderByDescending(x => x.Name) : items.OrderBy(x => x.Name),
                "price" => desc ? items.OrderByDescending(x => x.Price) : items.OrderBy(x => x.Price),
                "isactive" => desc ? items.OrderByDescending(x => x.IsActive) : items.OrderBy(x => x.IsActive),
                "isavailable" => desc ? items.OrderByDescending(x => x.IsAvailable) : items.OrderBy(x => x.IsAvailable),
                "isfeatured" => desc ? items.OrderByDescending(x => x.IsFeatured) : items.OrderBy(x => x.IsFeatured),
                "createdat" => desc ? items.OrderByDescending(x => x.CreatedAt) : items.OrderBy(x => x.CreatedAt),
                _ => desc ? items.OrderByDescending(x => x.DisplayOrder) : items.OrderBy(x => x.DisplayOrder)
            };
        }

        private static PagedResult<T> ToPagedResult<T>(IEnumerable<T> source, int pageNumber, int pageSize)
        {
            var normalizedPageNumber = pageNumber <= 0 ? 1 : pageNumber;
            var normalizedPageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, 100);
            var items = source.ToList();
            return new PagedResult<T>
            {
                Items = items.Skip((normalizedPageNumber - 1) * normalizedPageSize).Take(normalizedPageSize).ToList(),
                PageNumber = normalizedPageNumber,
                PageSize = normalizedPageSize,
                TotalItems = items.Count
            };
        }

        private Guid GetCurrentUserId()
        {
            return _currentUserService.UserId ?? throw new UnauthorizedException();
        }

        private static void ValidateSort(string? sortBy, IReadOnlyCollection<string> allowed)
        {
            if (!string.IsNullOrWhiteSpace(sortBy) && !allowed.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            {
                throw new ScanNow.Domain.Exceptions.ValidationException("sortBy", "Invalid sort field");
            }
        }

        private static bool IsDesc(string? direction) => direction?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true;
        private static bool Contains(string? value, string search) => value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
        private static HashSet<Guid> GetCategoryIds(MenuQuery query)
        {
            var categoryIds = query.CategoryIds?.Where(x => x != Guid.Empty).ToHashSet() ?? new HashSet<Guid>();
            if (query.CategoryId.HasValue && query.CategoryId.Value != Guid.Empty)
            {
                categoryIds.Add(query.CategoryId.Value);
            }

            return categoryIds;
        }

        private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
