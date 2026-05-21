using FluentValidation;
using Microsoft.AspNetCore.Identity;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.RestaurantManagement.DTOs;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;

namespace ScanNow.Application.Features.RestaurantManagement
{
    public class RestaurantManagementService : IRestaurantManagementService
    {
        private static readonly string OwnerRole = UserRole.OWNER.ToString();
        private readonly IRestaurantManagementRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IValidator<CreateRestaurantRequest> _createRestaurantValidator;
        private readonly IValidator<UpdateRestaurantRequest> _updateRestaurantValidator;
        private readonly IValidator<CreateBranchRequest> _createBranchValidator;
        private readonly IValidator<UpdateBranchRequest> _updateBranchValidator;
        private readonly IValidator<RestaurantQuery> _restaurantQueryValidator;
        private readonly IValidator<BranchQuery> _branchQueryValidator;

        public RestaurantManagementService(
            IRestaurantManagementRepository repository,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            UserManager<ApplicationUser> userManager,
            IValidator<CreateRestaurantRequest> createRestaurantValidator,
            IValidator<UpdateRestaurantRequest> updateRestaurantValidator,
            IValidator<CreateBranchRequest> createBranchValidator,
            IValidator<UpdateBranchRequest> updateBranchValidator,
            IValidator<RestaurantQuery> restaurantQueryValidator,
            IValidator<BranchQuery> branchQueryValidator)
        {
            _repository = repository;
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _userManager = userManager;
            _createRestaurantValidator = createRestaurantValidator;
            _updateRestaurantValidator = updateRestaurantValidator;
            _createBranchValidator = createBranchValidator;
            _updateBranchValidator = updateBranchValidator;
            _restaurantQueryValidator = restaurantQueryValidator;
            _branchQueryValidator = branchQueryValidator;
        }

        public async Task<PagedResult<RestaurantResponse>> GetRestaurantsAsync(RestaurantQuery query)
        {
            await _restaurantQueryValidator.ValidateAndThrowAsync(query);
            ValidateRestaurantSort(query.SortBy);

            IEnumerable<Restaurant> restaurants = await _repository.GetRestaurantsAsync();
            restaurants = ApplyRestaurantFilters(restaurants, query);
            restaurants = ApplyRestaurantSort(restaurants, query);

            return ToPagedResult(restaurants.Select(MapRestaurant), query.PageNumber, query.PageSize);
        }

        public async Task<RestaurantResponse> GetRestaurantByIdAsync(Guid id)
        {
            var restaurant = await _repository.GetRestaurantByIdAsync(id)
                ?? throw new NotFoundException("Restaurant not found");

            return MapRestaurant(restaurant);
        }

        public async Task<PagedResult<BranchResponse>> GetRestaurantBranchesAsync(Guid restaurantId, BranchQuery query)
        {
            await _branchQueryValidator.ValidateAndThrowAsync(query);
            ValidateBranchSort(query.SortBy);

            if (await _repository.GetRestaurantByIdAsync(restaurantId) is null)
            {
                throw new NotFoundException("Restaurant not found");
            }

            IEnumerable<Branch> branches = await _repository.GetBranchesByRestaurantIdAsync(restaurantId);
            branches = ApplyBranchFilters(branches, query);
            branches = ApplyBranchSort(branches, query);

            return ToPagedResult(branches.Select(MapBranch), query.PageNumber, query.PageSize);
        }

        public async Task<BranchResponse> GetRestaurantBranchByIdAsync(Guid restaurantId, Guid branchId)
        {
            if (await _repository.GetRestaurantByIdAsync(restaurantId) is null)
            {
                throw new NotFoundException("Restaurant not found");
            }

            var branch = await _repository.GetBranchByIdAsync(branchId)
                ?? throw new NotFoundException("Branch not found");

            if (branch.RestaurantId != restaurantId)
            {
                throw new NotFoundException("Branch not found");
            }

            return MapBranch(branch);
        }

        public async Task<RestaurantResponse> CreateRestaurantAsync(CreateRestaurantRequest request)
        {
            await _createRestaurantValidator.ValidateAndThrowAsync(request);

            var owner = await _userManager.FindByIdAsync(request.OwnerId.ToString())
                ?? throw new NotFoundException("Owner not found");

            if (!await _userManager.IsInRoleAsync(owner, OwnerRole))
            {
                throw new ScanNow.Domain.Exceptions.ValidationException("ownerId", "User must have OWNER role");
            }

            if (await _repository.GetRestaurantByOwnerIdAsync(request.OwnerId) is not null)
            {
                throw new ConflictException("Owner already has a restaurant");
            }

            var slug = Normalize(request.Slug);
            if (await _repository.RestaurantSlugExistsAsync(slug))
            {
                throw new ConflictException("Restaurant slug already exists");
            }

            var restaurant = new Restaurant
            {
                Id = Guid.NewGuid(),
                OwnerId = request.OwnerId,
                Name = request.Name.Trim(),
                Slug = slug,
                LogoUrl = TrimToNull(request.LogoUrl),
                Description = TrimToNull(request.Description),
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.AddRestaurantAsync(restaurant);
            await _unitOfWork.SaveChangesAsync();

            return MapRestaurant(restaurant);
        }

        public async Task<RestaurantResponse> UpdateRestaurantAsync(Guid id, UpdateRestaurantRequest request)
        {
            await _updateRestaurantValidator.ValidateAndThrowAsync(request);

            var restaurant = await _repository.GetRestaurantByIdAsync(id)
                ?? throw new NotFoundException("Restaurant not found");

            await UpdateRestaurantCoreAsync(restaurant, request);
            await _unitOfWork.SaveChangesAsync();
            return MapRestaurant(restaurant);
        }

        public async Task<RestaurantResponse> BanRestaurantAsync(Guid id)
        {
            var restaurant = await _repository.GetRestaurantByIdAsync(id)
                ?? throw new NotFoundException("Restaurant not found");

            restaurant.IsActive = false;
            restaurant.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return MapRestaurant(restaurant);
        }

        public async Task<RestaurantResponse> UnbanRestaurantAsync(Guid id)
        {
            var restaurant = await _repository.GetRestaurantByIdAsync(id)
                ?? throw new NotFoundException("Restaurant not found");

            restaurant.IsActive = true;
            restaurant.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return MapRestaurant(restaurant);
        }

        public async Task<RestaurantResponse?> GetCurrentOwnerRestaurantAsync()
        {
            var ownerId = GetCurrentUserId();
            var restaurant = await _repository.GetRestaurantByOwnerIdAsync(ownerId);
            return restaurant is null ? null : MapRestaurant(restaurant);
        }

        public async Task<RestaurantResponse> UpdateCurrentOwnerRestaurantAsync(UpdateRestaurantRequest request)
        {
            await _updateRestaurantValidator.ValidateAndThrowAsync(request);

            var ownerId = GetCurrentUserId();
            var restaurant = await _repository.GetRestaurantByOwnerIdAsync(ownerId)
                ?? throw new NotFoundException("Restaurant not found");

            EnsureRestaurantActive(restaurant);
            await UpdateRestaurantCoreAsync(restaurant, request);

            await _unitOfWork.SaveChangesAsync();
            return MapRestaurant(restaurant);
        }

        public async Task<BranchResponse> CreateOwnerBranchAsync(CreateBranchRequest request)
        {
            await _createBranchValidator.ValidateAndThrowAsync(request);

            var restaurant = await GetRequiredCurrentOwnerRestaurantAsync();
            var slug = Normalize(request.Slug);
            if (await _repository.BranchSlugExistsAsync(restaurant.Id, slug))
            {
                throw new ConflictException("Branch slug already exists in this restaurant");
            }

            var branch = new Branch
            {
                Id = Guid.NewGuid(),
                RestaurantId = restaurant.Id,
                Name = request.Name.Trim(),
                Slug = slug,
                Address = TrimToNull(request.Address),
                Phone = TrimToNull(request.Phone),
                Email = TrimToNull(request.Email),
                OpenTime = request.OpenTime,
                CloseTime = request.CloseTime,
                IsActive = true,
                VatPercent = request.VatPercent,
                ServiceChargePercent = request.ServiceChargePercent,
                ServiceChargeFixed = request.ServiceChargeFixed,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _repository.AddBranchAsync(branch);
            await _unitOfWork.SaveChangesAsync();
            return MapBranch(branch);
        }

        public async Task<PagedResult<BranchResponse>> GetOwnerBranchesAsync(BranchQuery query)
        {
            await _branchQueryValidator.ValidateAndThrowAsync(query);
            ValidateBranchSort(query.SortBy);

            var restaurant = await GetRequiredCurrentOwnerRestaurantAsync();
            IEnumerable<Branch> branches = await _repository.GetBranchesByRestaurantIdAsync(restaurant.Id);

            branches = ApplyBranchFilters(branches, query);
            branches = ApplyBranchSort(branches, query);

            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var items = branches.Select(MapBranch).ToList();

            return new PagedResult<BranchResponse>
            {
                Items = items.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = items.Count
            };
        }

        public async Task<BranchResponse> GetOwnerBranchByIdAsync(Guid id)
        {
            var branch = await GetOwnedBranchAsync(id);
            return MapBranch(branch);
        }

        public async Task<BranchResponse> UpdateOwnerBranchAsync(Guid id, UpdateBranchRequest request)
        {
            await _updateBranchValidator.ValidateAndThrowAsync(request);

            var branch = await GetOwnedBranchAsync(id);
            var slug = Normalize(request.Slug);
            if (await _repository.BranchSlugExistsAsync(branch.RestaurantId, slug, branch.Id))
            {
                throw new ConflictException("Branch slug already exists in this restaurant");
            }

            branch.Name = request.Name.Trim();
            branch.Slug = slug;
            branch.Address = TrimToNull(request.Address);
            branch.Phone = TrimToNull(request.Phone);
            branch.Email = TrimToNull(request.Email);
            branch.OpenTime = request.OpenTime;
            branch.CloseTime = request.CloseTime;
            branch.VatPercent = request.VatPercent;
            branch.ServiceChargePercent = request.ServiceChargePercent;
            branch.ServiceChargeFixed = request.ServiceChargeFixed;
            branch.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return MapBranch(branch);
        }

        public async Task<BranchResponse> InactiveOwnerBranchAsync(Guid id)
        {
            var branch = await GetOwnedBranchAsync(id);
            branch.IsActive = false;
            branch.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return MapBranch(branch);
        }

        public async Task<BranchResponse> ActiveOwnerBranchAsync(Guid id)
        {
            var branch = await GetOwnedBranchAsync(id);
            branch.IsActive = true;
            branch.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return MapBranch(branch);
        }

        public async Task<IReadOnlyList<BranchResponse>> GetMyBranchesAsync()
        {
            var userId = GetCurrentUserId();
            var branches = await _repository.GetBranchesByUserIdAsync(userId);
            return branches
                .OrderBy(x => x.Name)
                .Select(MapBranch)
                .ToList();
        }

        public async Task<BranchResponse> GetMyBranchByIdAsync(Guid id)
        {
            var userId = GetCurrentUserId();
            var branches = await _repository.GetBranchesByUserIdAsync(userId);
            var branch = branches.FirstOrDefault(x => x.Id == id)
                ?? throw new ForbiddenException("Branch is outside your scope.");

            return MapBranch(branch);
        }

        private async Task UpdateRestaurantCoreAsync(Restaurant restaurant, UpdateRestaurantRequest request)
        {
            var slug = Normalize(request.Slug);
            if (await _repository.RestaurantSlugExistsAsync(slug, restaurant.Id))
            {
                throw new ConflictException("Restaurant slug already exists");
            }

            restaurant.Name = request.Name.Trim();
            restaurant.Slug = slug;
            restaurant.LogoUrl = TrimToNull(request.LogoUrl);
            restaurant.Description = TrimToNull(request.Description);
            restaurant.UpdatedAt = DateTime.UtcNow;
        }

        private async Task<Branch> GetOwnedBranchAsync(Guid id)
        {
            var restaurant = await GetRequiredCurrentOwnerRestaurantAsync();
            var branch = await _repository.GetBranchByIdAsync(id)
                ?? throw new NotFoundException("Branch not found");

            if (branch.RestaurantId != restaurant.Id)
            {
                throw new ForbiddenException("Branch is outside your restaurant scope.");
            }

            return branch;
        }

        private async Task<Restaurant> GetRequiredCurrentOwnerRestaurantAsync()
        {
            var ownerId = GetCurrentUserId();
            var restaurant = await _repository.GetRestaurantByOwnerIdAsync(ownerId)
                ?? throw new BusinessRuleException("Owner has no restaurant");

            EnsureRestaurantActive(restaurant);
            return restaurant;
        }

        private Guid GetCurrentUserId()
        {
            return _currentUserService.UserId ?? throw new UnauthorizedException();
        }

        private static IEnumerable<Branch> ApplyBranchFilters(IEnumerable<Branch> branches, BranchQuery query)
        {
            if (query.IsActive.HasValue)
            {
                branches = branches.Where(x => x.IsActive == query.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                branches = branches.Where(x =>
                    Contains(x.Name, search)
                    || Contains(x.Slug, search)
                    || Contains(x.Address, search)
                    || Contains(x.Phone, search)
                    || Contains(x.Email, search));
            }

            return branches;
        }

        private static IEnumerable<Restaurant> ApplyRestaurantFilters(IEnumerable<Restaurant> restaurants, RestaurantQuery query)
        {
            if (query.IsActive.HasValue)
            {
                restaurants = restaurants.Where(x => x.IsActive == query.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                restaurants = restaurants.Where(x =>
                    Contains(x.Name, search)
                    || Contains(x.Slug, search)
                    || Contains(x.Description, search));
            }

            return restaurants;
        }

        private static IEnumerable<Restaurant> ApplyRestaurantSort(IEnumerable<Restaurant> restaurants, RestaurantQuery query)
        {
            var desc = query.SortDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true;
            return query.SortBy?.ToLowerInvariant() switch
            {
                "name" => desc ? restaurants.OrderByDescending(x => x.Name) : restaurants.OrderBy(x => x.Name),
                "slug" => desc ? restaurants.OrderByDescending(x => x.Slug) : restaurants.OrderBy(x => x.Slug),
                "isactive" => desc ? restaurants.OrderByDescending(x => x.IsActive) : restaurants.OrderBy(x => x.IsActive),
                _ => desc ? restaurants.OrderByDescending(x => x.CreatedAt) : restaurants.OrderBy(x => x.CreatedAt)
            };
        }

        private static IEnumerable<Branch> ApplyBranchSort(IEnumerable<Branch> branches, BranchQuery query)
        {
            var desc = query.SortDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true;
            return query.SortBy?.ToLowerInvariant() switch
            {
                "name" => desc ? branches.OrderByDescending(x => x.Name) : branches.OrderBy(x => x.Name),
                "slug" => desc ? branches.OrderByDescending(x => x.Slug) : branches.OrderBy(x => x.Slug),
                "isactive" => desc ? branches.OrderByDescending(x => x.IsActive) : branches.OrderBy(x => x.IsActive),
                _ => desc ? branches.OrderByDescending(x => x.CreatedAt) : branches.OrderBy(x => x.CreatedAt)
            };
        }

        private static void ValidateBranchSort(string? sortBy)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                return;
            }

            var allowed = new[] { "name", "slug", "createdAt", "isActive" };
            if (!allowed.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            {
                throw new ScanNow.Domain.Exceptions.ValidationException("sortBy", "Invalid sort field");
            }
        }

        private static void ValidateRestaurantSort(string? sortBy)
        {
            if (string.IsNullOrWhiteSpace(sortBy))
            {
                return;
            }

            var allowed = new[] { "name", "slug", "createdAt", "isActive" };
            if (!allowed.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            {
                throw new ScanNow.Domain.Exceptions.ValidationException("sortBy", "Invalid sort field");
            }
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

        private static void EnsureRestaurantActive(Restaurant restaurant)
        {
            if (!restaurant.IsActive)
            {
                throw new BusinessRuleException("Restaurant is inactive");
            }
        }

        private static RestaurantResponse MapRestaurant(Restaurant restaurant)
        {
            return new RestaurantResponse
            {
                RestaurantId = restaurant.Id,
                OwnerId = restaurant.OwnerId,
                Name = restaurant.Name,
                Slug = restaurant.Slug,
                LogoUrl = restaurant.LogoUrl,
                Description = restaurant.Description,
                IsActive = restaurant.IsActive,
                CreatedAt = restaurant.CreatedAt,
                UpdatedAt = restaurant.UpdatedAt
            };
        }

        private static BranchResponse MapBranch(Branch branch)
        {
            return new BranchResponse
            {
                BranchId = branch.Id,
                RestaurantId = branch.RestaurantId,
                ManagerId = branch.ManagerId,
                Name = branch.Name,
                Slug = branch.Slug,
                Address = branch.Address,
                Phone = branch.Phone,
                Email = branch.Email,
                OpenTime = branch.OpenTime,
                CloseTime = branch.CloseTime,
                IsActive = branch.IsActive,
                VatPercent = branch.VatPercent,
                ServiceChargePercent = branch.ServiceChargePercent,
                ServiceChargeFixed = branch.ServiceChargeFixed,
                CreatedAt = branch.CreatedAt,
                UpdatedAt = branch.UpdatedAt
            };
        }

        private static string Normalize(string value)
        {
            return value.Trim().ToLowerInvariant();
        }

        private static string? TrimToNull(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static bool Contains(string? value, string search)
        {
            return value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
