using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.UserManagement.DTOs;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;

namespace ScanNow.Application.Features.UserManagement
{
    public class UserManagementService : IUserManagementService
    {
        private const string LocalProvider = "Local";
        private static readonly string AdminRole = UserRole.ADMIN.ToString();
        private static readonly string OwnerRole = UserRole.OWNER.ToString();
        private static readonly string BranchManagerRole = UserRole.BRANCH_MANAGER.ToString();
        private static readonly string StaffRole = UserRole.STAFF.ToString();
        private static readonly string KitchenRole = UserRole.KITCHEN.ToString();
        private static readonly string[] OwnerManagedRoles = [nameof(UserRole.BRANCH_MANAGER), nameof(UserRole.STAFF), nameof(UserRole.KITCHEN)];
        private static readonly string[] ManagerManagedRoles = [nameof(UserRole.STAFF), nameof(UserRole.KITCHEN)];

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IUserManagementRepository _repository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUserService;
        private readonly IValidator<UserListQuery> _queryValidator;
        private readonly IValidator<CreateOwnerRequest> _createOwnerValidator;
        private readonly IValidator<UpdateOwnerRequest> _updateOwnerValidator;
        private readonly IValidator<CreateManagedUserRequest> _createManagedUserValidator;
        private readonly IValidator<UpdateManagedUserRequest> _updateManagedUserValidator;

        public UserManagementService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IUserManagementRepository repository,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUserService,
            IValidator<UserListQuery> queryValidator,
            IValidator<CreateOwnerRequest> createOwnerValidator,
            IValidator<UpdateOwnerRequest> updateOwnerValidator,
            IValidator<CreateManagedUserRequest> createManagedUserValidator,
            IValidator<UpdateManagedUserRequest> updateManagedUserValidator)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _repository = repository;
            _unitOfWork = unitOfWork;
            _currentUserService = currentUserService;
            _queryValidator = queryValidator;
            _createOwnerValidator = createOwnerValidator;
            _updateOwnerValidator = updateOwnerValidator;
            _createManagedUserValidator = createManagedUserValidator;
            _updateManagedUserValidator = updateManagedUserValidator;
        }

        public async Task<PagedResult<OwnerUserResponse>> GetOwnersAsync(UserListQuery query)
        {
            await ValidateQueryAsync(query, ["fullName", "username", "email", "createdAt", "restaurantName"]);

            var owners = (await _userManager.GetUsersInRoleAsync(OwnerRole)).ToList();
            var restaurants = await _repository.GetRestaurantsByOwnerIdsAsync(owners.Select(x => x.Id));
            var restaurantByOwner = restaurants
                .GroupBy(x => x.OwnerId)
                .ToDictionary(x => x.Key, x => x.First());

            var items = owners.Select(user =>
            {
                restaurantByOwner.TryGetValue(user.Id, out var restaurant);
                return new OwnerUserResponse
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Username = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    PhoneNumber = user.PhoneNumber,
                    IsActive = user.IsActive,
                    IsBanned = IsBanned(user),
                    RestaurantId = restaurant?.Id,
                    RestaurantName = restaurant?.Name,
                    CreatedAt = user.CreatedAt
                };
            });

            items = ApplyOwnerFilters(items, query);
            items = ApplyOwnerSort(items, query);
            return ToPagedResult(items, query);
        }

        public async Task<PagedResult<OwnerUserResponse>> GetAvailableOwnersAsync(UserListQuery query)
        {
            await ValidateQueryAsync(query, ["fullName", "username", "email", "createdAt"]);

            var owners = (await _userManager.GetUsersInRoleAsync(OwnerRole)).ToList();
            var restaurants = await _repository.GetRestaurantsByOwnerIdsAsync(owners.Select(x => x.Id));
            var assignedOwnerIds = restaurants.Select(x => x.OwnerId).ToHashSet();

            var items = owners
                .Where(user => !assignedOwnerIds.Contains(user.Id))
                .Select(user => new OwnerUserResponse
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    Username = user.UserName ?? string.Empty,
                    Email = user.Email ?? string.Empty,
                    PhoneNumber = user.PhoneNumber,
                    IsActive = user.IsActive,
                    IsBanned = IsBanned(user),
                    CreatedAt = user.CreatedAt
                });

            items = ApplyOwnerFilters(items, query);
            items = ApplyOwnerSort(items, query);
            return ToPagedResult(items, query);
        }

        public async Task<OwnerUserResponse> CreateOwnerAsync(CreateOwnerRequest request)
        {
            await _createOwnerValidator.ValidateAndThrowAsync(request);
            await EnsureEmailUniqueAsync(request.Email);
            await EnsureUsernameUniqueAsync(request.Username);

            var user = CreateUser(request.FullName, request.Username, request.Email, request.PhoneNumber);
            EnsureSucceeded(await _userManager.CreateAsync(user, request.Password));
            await AddRoleAsync(user, OwnerRole);

            return new OwnerUserResponse
            {
                UserId = user.Id,
                FullName = user.FullName,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                IsBanned = IsBanned(user),
                CreatedAt = user.CreatedAt
            };
        }

        public async Task<OwnerUserResponse> UpdateOwnerAsync(Guid id, UpdateOwnerRequest request)
        {
            await _updateOwnerValidator.ValidateAndThrowAsync(request);
            var user = await GetUserAsync(id);
            await EnsureUserInRoleAsync(user, OwnerRole);
            await EnsureEmailUniqueAsync(request.Email, id);
            await EnsureUsernameUniqueAsync(request.Username, id);

            UpdateBasicInfo(user, request.FullName, request.Username, request.Email, request.PhoneNumber);
            EnsureSucceeded(await _userManager.UpdateAsync(user));

            var restaurant = (await _repository.GetRestaurantsByOwnerIdsAsync([id])).FirstOrDefault();
            return new OwnerUserResponse
            {
                UserId = user.Id,
                FullName = user.FullName,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                IsActive = user.IsActive,
                IsBanned = IsBanned(user),
                RestaurantId = restaurant?.Id,
                RestaurantName = restaurant?.Name,
                CreatedAt = user.CreatedAt
            };
        }

        public async Task BanOwnerAsync(Guid id, BanUserRequest request)
        {
            var user = await GetUserAsync(id);
            await EnsureUserInRoleAsync(user, OwnerRole);
            await BanAsync(user);
        }

        public async Task UnbanOwnerAsync(Guid id)
        {
            var user = await GetUserAsync(id);
            await EnsureUserInRoleAsync(user, OwnerRole);
            await UnbanAsync(user);
        }

        public async Task<PagedResult<OwnerScopedUserResponse>> GetOwnerUsersAsync(UserListQuery query)
        {
            await ValidateQueryAsync(query, ["fullName", "username", "email", "role", "createdAt"], OwnerManagedRoles);
            var restaurant = await GetCurrentOwnerRestaurantAsync();
            var branches = await _repository.GetBranchesByRestaurantIdAsync(restaurant.Id);
            var branchStaff = await _repository.GetBranchStaffByBranchIdsAsync(branches.Select(x => x.Id));
            IEnumerable<OwnerScopedUserResponse> scopedUsers = await BuildOwnerScopedUsersAsync(restaurant, branches, branchStaff);

            scopedUsers = ApplyOwnerScopedFilters(scopedUsers, query);
            scopedUsers = ApplyOwnerScopedSort(scopedUsers, query);
            return ToPagedResult(scopedUsers, query);
        }

        public async Task<OwnerScopedUserResponse> CreateOwnerUserAsync(CreateManagedUserRequest request)
        {
            await _createManagedUserValidator.ValidateAndThrowAsync(request);
            var role = NormalizeRole(request.Role);
            if (!OwnerManagedRoles.Contains(role))
            {
                throw new ScanNow.Domain.Exceptions.ValidationException("role", "Invalid role");
            }

            await EnsureEmailUniqueAsync(request.Email);
            await EnsureUsernameUniqueAsync(request.Username);
            var currentUserId = GetCurrentUserId();
            var restaurant = await GetCurrentOwnerRestaurantAsync();
            var branches = await GetOwnedBranchesAsync(restaurant.Id, request.BranchIds);

            var user = CreateUser(request.FullName, request.Username, request.Email, request.PhoneNumber);
            EnsureSucceeded(await _userManager.CreateAsync(user, request.Password));
            await AddRoleAsync(user, role);

            await AssignOwnerManagedUserAsync(user.Id, role, branches, currentUserId);
            return await MapOwnerScopedUserAsync(user, role, restaurant, branches);
        }

        public async Task<OwnerScopedUserResponse> UpdateOwnerUserAsync(Guid id, UpdateManagedUserRequest request)
        {
            await _updateManagedUserValidator.ValidateAndThrowAsync(request);
            var role = NormalizeRole(request.Role);
            if (!OwnerManagedRoles.Contains(role))
            {
                throw new ScanNow.Domain.Exceptions.ValidationException("role", "Invalid role");
            }

            var restaurant = await GetCurrentOwnerRestaurantAsync();
            var branches = await GetOwnedBranchesAsync(restaurant.Id, request.BranchIds);
            var user = await GetUserAsync(id);
            var currentRole = await GetAllowedRoleAsync(user, OwnerManagedRoles);
            if (currentRole is null || !await UserBelongsToRestaurantAsync(id, restaurant.Id))
            {
                throw new ForbiddenException();
            }

            await EnsureEmailUniqueAsync(request.Email, id);
            await EnsureUsernameUniqueAsync(request.Username, id);
            UpdateBasicInfo(user, request.FullName, request.Username, request.Email, request.PhoneNumber);
            await ReplaceRolesAsync(user, OwnerManagedRoles, role);
            await ReplaceOwnerAssignmentsAsync(id, role, branches, GetCurrentUserId());
            EnsureSucceeded(await _userManager.UpdateAsync(user));
            await _unitOfWork.SaveChangesAsync();

            return await MapOwnerScopedUserAsync(user, role, restaurant, branches);
        }

        public async Task BanOwnerUserAsync(Guid id)
        {
            var restaurant = await GetCurrentOwnerRestaurantAsync();
            var user = await GetUserAsync(id);
            if (await GetAllowedRoleAsync(user, OwnerManagedRoles) is null || !await UserBelongsToRestaurantAsync(id, restaurant.Id))
            {
                throw new ForbiddenException();
            }

            await BanAsync(user);
        }

        public async Task UnbanOwnerUserAsync(Guid id)
        {
            var restaurant = await GetCurrentOwnerRestaurantAsync();
            var user = await GetUserAsync(id);
            if (await GetAllowedRoleAsync(user, OwnerManagedRoles) is null || !await UserBelongsToRestaurantAsync(id, restaurant.Id))
            {
                throw new ForbiddenException();
            }

            await UnbanAsync(user);
        }

        public async Task<PagedResult<ManagerScopedUserResponse>> GetManagerUsersAsync(UserListQuery query)
        {
            await ValidateQueryAsync(query, ["fullName", "username", "email", "role", "createdAt"], ManagerManagedRoles);
            var branches = await GetCurrentManagerBranchesAsync();
            var branchStaff = await _repository.GetBranchStaffByBranchIdsAsync(branches.Select(x => x.Id));
            IEnumerable<ManagerScopedUserResponse> scopedUsers = await BuildManagerScopedUsersAsync(branches, branchStaff);

            scopedUsers = ApplyManagerScopedFilters(scopedUsers, query);
            scopedUsers = ApplyManagerScopedSort(scopedUsers, query);
            return ToPagedResult(scopedUsers, query);
        }

        public async Task<ManagerScopedUserResponse> CreateManagerUserAsync(CreateManagedUserRequest request)
        {
            await _createManagedUserValidator.ValidateAndThrowAsync(request);
            var role = NormalizeRole(request.Role);
            if (!ManagerManagedRoles.Contains(role))
            {
                throw new ScanNow.Domain.Exceptions.ValidationException("role", "Invalid role");
            }

            await EnsureEmailUniqueAsync(request.Email);
            await EnsureUsernameUniqueAsync(request.Username);
            var branches = await GetManagedBranchesAsync(request.BranchIds);
            var user = CreateUser(request.FullName, request.Username, request.Email, request.PhoneNumber);
            EnsureSucceeded(await _userManager.CreateAsync(user, request.Password));
            await AddRoleAsync(user, role);

            await ReplaceStaffAssignmentsAsync(user.Id, branches, GetCurrentUserId());
            await _unitOfWork.SaveChangesAsync();

            return MapManagerScopedUser(user, role, branches);
        }

        public async Task<ManagerScopedUserResponse> UpdateManagerUserAsync(Guid id, UpdateManagedUserRequest request)
        {
            await _updateManagedUserValidator.ValidateAndThrowAsync(request);
            var role = NormalizeRole(request.Role);
            if (!ManagerManagedRoles.Contains(role))
            {
                throw new ScanNow.Domain.Exceptions.ValidationException("role", "Invalid role");
            }

            var branches = await GetManagedBranchesAsync(request.BranchIds);
            var user = await GetUserAsync(id);
            if (await GetAllowedRoleAsync(user, ManagerManagedRoles) is null || !await UserBelongsToManagedBranchesAsync(id))
            {
                throw new ForbiddenException();
            }

            await EnsureEmailUniqueAsync(request.Email, id);
            await EnsureUsernameUniqueAsync(request.Username, id);
            UpdateBasicInfo(user, request.FullName, request.Username, request.Email, request.PhoneNumber);
            await ReplaceRolesAsync(user, ManagerManagedRoles, role);
            await ReplaceStaffAssignmentsAsync(id, branches, GetCurrentUserId());
            EnsureSucceeded(await _userManager.UpdateAsync(user));
            await _unitOfWork.SaveChangesAsync();

            return MapManagerScopedUser(user, role, branches);
        }

        public async Task BanManagerUserAsync(Guid id)
        {
            var user = await GetUserAsync(id);
            if (await GetAllowedRoleAsync(user, ManagerManagedRoles) is null || !await UserBelongsToManagedBranchesAsync(id))
            {
                throw new ForbiddenException();
            }

            await BanAsync(user);
        }

        public async Task UnbanManagerUserAsync(Guid id)
        {
            var user = await GetUserAsync(id);
            if (await GetAllowedRoleAsync(user, ManagerManagedRoles) is null || !await UserBelongsToManagedBranchesAsync(id))
            {
                throw new ForbiddenException();
            }

            await UnbanAsync(user);
        }

        private async Task<List<OwnerScopedUserResponse>> BuildOwnerScopedUsersAsync(Restaurant restaurant, List<Branch> branches, List<BranchStaff> branchStaff)
        {
            var managerIds = branches.Where(x => x.ManagerId.HasValue).Select(x => x.ManagerId!.Value);
            var staffIds = branchStaff.Select(x => x.UserId);
            var userIds = managerIds.Concat(staffIds).Distinct().ToList();
            var users = await _userManager.Users.Where(x => userIds.Contains(x.Id)).ToListAsync();
            var responses = new List<OwnerScopedUserResponse>();

            foreach (var user in users)
            {
                var role = await GetAllowedRoleAsync(user, OwnerManagedRoles);
                if (role is null)
                {
                    continue;
                }

                var userBranches = GetUserBranches(user.Id, role, branches, branchStaff);
                responses.Add(await MapOwnerScopedUserAsync(user, role, restaurant, userBranches));
            }

            return responses;
        }

        private async Task<List<ManagerScopedUserResponse>> BuildManagerScopedUsersAsync(List<Branch> branches, List<BranchStaff> branchStaff)
        {
            var userIds = branchStaff.Select(x => x.UserId).Distinct().ToList();
            var users = await _userManager.Users.Where(x => userIds.Contains(x.Id)).ToListAsync();
            var responses = new List<ManagerScopedUserResponse>();

            foreach (var user in users)
            {
                var role = await GetAllowedRoleAsync(user, ManagerManagedRoles);
                if (role is null)
                {
                    continue;
                }

                var userBranches = branches
                    .Where(branch => branchStaff.Any(staff => staff.UserId == user.Id && staff.BranchId == branch.Id))
                    .ToList();
                responses.Add(MapManagerScopedUser(user, role, userBranches));
            }

            return responses;
        }

        private async Task AssignOwnerManagedUserAsync(Guid userId, string role, List<Branch> branches, Guid assignedById)
        {
            if (role == BranchManagerRole)
            {
                foreach (var branch in branches)
                {
                    branch.ManagerId = userId;
                    branch.UpdatedAt = DateTime.UtcNow;
                }

                await _unitOfWork.SaveChangesAsync();
                return;
            }

            await ReplaceStaffAssignmentsAsync(userId, branches, assignedById);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task ReplaceOwnerAssignmentsAsync(Guid userId, string role, List<Branch> branches, Guid assignedById)
        {
            var currentStaff = await _repository.GetBranchStaffByUserIdAsync(userId);
            _repository.RemoveBranchStaffRange(currentStaff);

            var currentManaged = await _repository.GetBranchesByManagerIdAsync(userId);
            foreach (var branch in currentManaged)
            {
                branch.ManagerId = branches.Any(x => x.Id == branch.Id) && role == BranchManagerRole ? userId : null;
                branch.UpdatedAt = DateTime.UtcNow;
            }

            if (role == BranchManagerRole)
            {
                foreach (var branch in branches.Where(branch => currentManaged.All(x => x.Id != branch.Id)))
                {
                    branch.ManagerId = userId;
                    branch.UpdatedAt = DateTime.UtcNow;
                }

                return;
            }

            foreach (var branch in branches)
            {
                await _repository.AddBranchStaffAsync(new BranchStaff
                {
                    Id = Guid.NewGuid(),
                    BranchId = branch.Id,
                    UserId = userId,
                    AssignedById = assignedById,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        private async Task ReplaceStaffAssignmentsAsync(Guid userId, List<Branch> branches, Guid assignedById)
        {
            var currentStaff = await _repository.GetBranchStaffByUserIdAsync(userId);
            _repository.RemoveBranchStaffRange(currentStaff);

            foreach (var branch in branches)
            {
                await _repository.AddBranchStaffAsync(new BranchStaff
                {
                    Id = Guid.NewGuid(),
                    BranchId = branch.Id,
                    UserId = userId,
                    AssignedById = assignedById,
                    AssignedAt = DateTime.UtcNow
                });
            }
        }

        private async Task<Restaurant> GetCurrentOwnerRestaurantAsync()
        {
            var ownerId = GetCurrentUserId();
            return await _repository.GetRestaurantByOwnerIdAsync(ownerId)
                ?? throw new BusinessRuleException("Owner has no restaurant");
        }

        private async Task<List<Branch>> GetCurrentManagerBranchesAsync()
        {
            var managerId = GetCurrentUserId();
            var branches = await _repository.GetBranchesByManagerIdAsync(managerId);
            return branches.Count == 0 ? throw new BusinessRuleException("Manager has no branch") : branches;
        }

        private async Task<List<Branch>> GetOwnedBranchesAsync(Guid restaurantId, IEnumerable<Guid> branchIds)
        {
            var distinctIds = branchIds.Distinct().ToList();
            var branches = await _repository.GetBranchesByIdsAsync(distinctIds);
            if (branches.Count != distinctIds.Count || branches.Any(x => x.RestaurantId != restaurantId))
            {
                throw new ForbiddenException("Branch is outside your restaurant scope.");
            }

            return branches;
        }

        private async Task<List<Branch>> GetManagedBranchesAsync(IEnumerable<Guid> branchIds)
        {
            var managerBranches = await GetCurrentManagerBranchesAsync();
            var managedIds = managerBranches.Select(x => x.Id).ToHashSet();
            var distinctIds = branchIds.Distinct().ToList();
            if (distinctIds.Count == 0 || distinctIds.Any(x => !managedIds.Contains(x)))
            {
                throw new ForbiddenException("Branch is outside your managed scope.");
            }

            return managerBranches.Where(x => distinctIds.Contains(x.Id)).ToList();
        }

        private async Task<bool> UserBelongsToRestaurantAsync(Guid userId, Guid restaurantId)
        {
            var branches = await _repository.GetBranchesByRestaurantIdAsync(restaurantId);
            return branches.Any(x => x.ManagerId == userId)
                || (await _repository.GetBranchStaffByBranchIdsAsync(branches.Select(x => x.Id))).Any(x => x.UserId == userId);
        }

        private async Task<bool> UserBelongsToManagedBranchesAsync(Guid userId)
        {
            var branches = await GetCurrentManagerBranchesAsync();
            var branchStaff = await _repository.GetBranchStaffByBranchIdsAsync(branches.Select(x => x.Id));
            return branchStaff.Any(x => x.UserId == userId);
        }

        private async Task<string?> GetAllowedRoleAsync(ApplicationUser user, IReadOnlyCollection<string> allowedRoles)
        {
            var roles = await _userManager.GetRolesAsync(user);
            return allowedRoles.FirstOrDefault(roles.Contains);
        }

        private async Task ReplaceRolesAsync(ApplicationUser user, string[] managedRoles, string newRole)
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            var removeRoles = currentRoles.Where(managedRoles.Contains).ToArray();
            if (removeRoles.Length > 0)
            {
                EnsureSucceeded(await _userManager.RemoveFromRolesAsync(user, removeRoles));
            }

            if (!await _userManager.IsInRoleAsync(user, newRole))
            {
                await AddRoleAsync(user, newRole);
            }
        }

        private async Task AddRoleAsync(ApplicationUser user, string role)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await _roleManager.CreateAsync(new ApplicationRole(role)));
            }

            EnsureSucceeded(await _userManager.AddToRoleAsync(user, role));
        }

        private async Task EnsureUserInRoleAsync(ApplicationUser user, string role)
        {
            if (!await _userManager.IsInRoleAsync(user, role))
            {
                throw new ForbiddenException();
            }
        }

        private async Task EnsureEmailUniqueAsync(string email, Guid? excludeUserId = null)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user is not null && user.Id != excludeUserId)
            {
                throw new ConflictException("Email already exists");
            }
        }

        private async Task EnsureUsernameUniqueAsync(string username, Guid? excludeUserId = null)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user is not null && user.Id != excludeUserId)
            {
                throw new ConflictException("Username already exists");
            }
        }

        private async Task<ApplicationUser> GetUserAsync(Guid id)
        {
            return await _userManager.FindByIdAsync(id.ToString())
                ?? throw new NotFoundException("User not found");
        }

        private Guid GetCurrentUserId()
        {
            return _currentUserService.UserId ?? throw new UnauthorizedException();
        }

        private static ApplicationUser CreateUser(string fullName, string username, string email, string? phoneNumber)
        {
            return new ApplicationUser
            {
                Email = email.Trim(),
                UserName = username.Trim(),
                FullName = fullName.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
                AuthProvider = LocalProvider,
                EmailConfirmed = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private static void UpdateBasicInfo(ApplicationUser user, string fullName, string username, string email, string? phoneNumber)
        {
            user.FullName = fullName.Trim();
            user.Email = email.Trim();
            user.UserName = username.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
            user.UpdatedAt = DateTime.UtcNow;
        }

        private async Task BanAsync(ApplicationUser user)
        {
            user.IsActive = false;
            user.LockoutEnabled = true;
            user.LockoutEnd = DateTimeOffset.MaxValue;
            user.UpdatedAt = DateTime.UtcNow;
            EnsureSucceeded(await _userManager.UpdateAsync(user));
        }

        private async Task UnbanAsync(ApplicationUser user)
        {
            user.IsActive = true;
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
            user.UpdatedAt = DateTime.UtcNow;
            EnsureSucceeded(await _userManager.UpdateAsync(user));
        }

        private async Task ValidateQueryAsync(UserListQuery query, IReadOnlyCollection<string> allowedSortFields, IReadOnlyCollection<string>? allowedFilterRoles = null)
        {
            await _queryValidator.ValidateAndThrowAsync(query);
            if (!string.IsNullOrWhiteSpace(query.SortBy) && !allowedSortFields.Contains(query.SortBy, StringComparer.OrdinalIgnoreCase))
            {
                throw new ScanNow.Domain.Exceptions.ValidationException("sortBy", "Invalid sort field");
            }

            if (!string.IsNullOrWhiteSpace(query.Role)
                && allowedFilterRoles is not null
                && !allowedFilterRoles.Contains(NormalizeRole(query.Role)))
            {
                throw new ScanNow.Domain.Exceptions.ValidationException("role", "Invalid role");
            }
        }

        private static IEnumerable<OwnerUserResponse> ApplyOwnerFilters(IEnumerable<OwnerUserResponse> items, UserListQuery query)
        {
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                items = items.Where(x =>
                    Contains(x.FullName, search)
                    || Contains(x.Username, search)
                    || Contains(x.Email, search)
                    || Contains(x.PhoneNumber, search)
                    || Contains(x.RestaurantName, search));
            }

            if (query.IsActive.HasValue)
            {
                items = items.Where(x => x.IsActive == query.IsActive.Value);
            }

            if (query.IsBanned.HasValue)
            {
                items = items.Where(x => x.IsBanned == query.IsBanned.Value);
            }

            return items;
        }

        private static IEnumerable<OwnerScopedUserResponse> ApplyOwnerScopedFilters(IEnumerable<OwnerScopedUserResponse> items, UserListQuery query)
        {
            items = ApplyScopedFilters(items, query, x => x.Role, x => x.BranchIds, x => x.BranchNames, x => x.FullName, x => x.Username, x => x.Email, x => x.PhoneNumber, x => x.IsActive, x => x.IsBanned);
            return items;
        }

        private static IEnumerable<ManagerScopedUserResponse> ApplyManagerScopedFilters(IEnumerable<ManagerScopedUserResponse> items, UserListQuery query)
        {
            items = ApplyScopedFilters(items, query, x => x.Role, x => x.BranchIds, x => x.BranchNames, x => x.FullName, x => x.Username, x => x.Email, x => x.PhoneNumber, x => x.IsActive, x => x.IsBanned);
            return items;
        }

        private static IEnumerable<T> ApplyScopedFilters<T>(
            IEnumerable<T> items,
            UserListQuery query,
            Func<T, string> roleSelector,
            Func<T, List<Guid>> branchIdsSelector,
            Func<T, List<string>> branchNamesSelector,
            Func<T, string> fullNameSelector,
            Func<T, string> usernameSelector,
            Func<T, string> emailSelector,
            Func<T, string?> phoneSelector,
            Func<T, bool> activeSelector,
            Func<T, bool> bannedSelector)
        {
            if (!string.IsNullOrWhiteSpace(query.Role))
            {
                var role = NormalizeRole(query.Role);
                items = items.Where(x => roleSelector(x) == role);
            }

            if (query.BranchId.HasValue)
            {
                items = items.Where(x => branchIdsSelector(x).Contains(query.BranchId.Value));
            }

            if (query.IsActive.HasValue)
            {
                items = items.Where(x => activeSelector(x) == query.IsActive.Value);
            }

            if (query.IsBanned.HasValue)
            {
                items = items.Where(x => bannedSelector(x) == query.IsBanned.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var search = query.Search.Trim();
                items = items.Where(x =>
                    Contains(fullNameSelector(x), search)
                    || Contains(usernameSelector(x), search)
                    || Contains(emailSelector(x), search)
                    || Contains(phoneSelector(x), search)
                    || branchNamesSelector(x).Any(name => Contains(name, search)));
            }

            return items;
        }

        private static IEnumerable<OwnerUserResponse> ApplyOwnerSort(IEnumerable<OwnerUserResponse> items, UserListQuery query)
        {
            var desc = IsDesc(query);
            return query.SortBy?.ToLowerInvariant() switch
            {
                "fullname" => desc ? items.OrderByDescending(x => x.FullName) : items.OrderBy(x => x.FullName),
                "username" => desc ? items.OrderByDescending(x => x.Username) : items.OrderBy(x => x.Username),
                "email" => desc ? items.OrderByDescending(x => x.Email) : items.OrderBy(x => x.Email),
                "restaurantname" => desc ? items.OrderByDescending(x => x.RestaurantName) : items.OrderBy(x => x.RestaurantName),
                _ => desc ? items.OrderByDescending(x => x.CreatedAt) : items.OrderBy(x => x.CreatedAt)
            };
        }

        private static IEnumerable<OwnerScopedUserResponse> ApplyOwnerScopedSort(IEnumerable<OwnerScopedUserResponse> items, UserListQuery query)
        {
            var desc = IsDesc(query);
            return query.SortBy?.ToLowerInvariant() switch
            {
                "fullname" => desc ? items.OrderByDescending(x => x.FullName) : items.OrderBy(x => x.FullName),
                "username" => desc ? items.OrderByDescending(x => x.Username) : items.OrderBy(x => x.Username),
                "email" => desc ? items.OrderByDescending(x => x.Email) : items.OrderBy(x => x.Email),
                "role" => desc ? items.OrderByDescending(x => x.Role) : items.OrderBy(x => x.Role),
                _ => desc ? items.OrderByDescending(x => x.CreatedAt) : items.OrderBy(x => x.CreatedAt)
            };
        }

        private static IEnumerable<ManagerScopedUserResponse> ApplyManagerScopedSort(IEnumerable<ManagerScopedUserResponse> items, UserListQuery query)
        {
            var desc = IsDesc(query);
            return query.SortBy?.ToLowerInvariant() switch
            {
                "fullname" => desc ? items.OrderByDescending(x => x.FullName) : items.OrderBy(x => x.FullName),
                "username" => desc ? items.OrderByDescending(x => x.Username) : items.OrderBy(x => x.Username),
                "email" => desc ? items.OrderByDescending(x => x.Email) : items.OrderBy(x => x.Email),
                "role" => desc ? items.OrderByDescending(x => x.Role) : items.OrderBy(x => x.Role),
                _ => desc ? items.OrderByDescending(x => x.CreatedAt) : items.OrderBy(x => x.CreatedAt)
            };
        }

        private static PagedResult<T> ToPagedResult<T>(IEnumerable<T> source, UserListQuery query)
        {
            var pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
            var pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
            var items = source.ToList();

            return new PagedResult<T>
            {
                Items = items.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalItems = items.Count
            };
        }

        private static List<Branch> GetUserBranches(Guid userId, string role, List<Branch> branches, List<BranchStaff> branchStaff)
        {
            if (role == BranchManagerRole)
            {
                return branches.Where(x => x.ManagerId == userId).ToList();
            }

            return branches
                .Where(branch => branchStaff.Any(staff => staff.UserId == userId && staff.BranchId == branch.Id))
                .ToList();
        }

        private static Task<OwnerScopedUserResponse> MapOwnerScopedUserAsync(ApplicationUser user, string role, Restaurant restaurant, List<Branch> branches)
        {
            return Task.FromResult(new OwnerScopedUserResponse
            {
                UserId = user.Id,
                FullName = user.FullName,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Role = role,
                RestaurantId = restaurant.Id,
                RestaurantName = restaurant.Name,
                BranchIds = branches.Select(x => x.Id).ToList(),
                BranchNames = branches.Select(x => x.Name).ToList(),
                IsActive = user.IsActive,
                IsBanned = IsBanned(user),
                CreatedAt = user.CreatedAt
            });
        }

        private static ManagerScopedUserResponse MapManagerScopedUser(ApplicationUser user, string role, List<Branch> branches)
        {
            return new ManagerScopedUserResponse
            {
                UserId = user.Id,
                FullName = user.FullName,
                Username = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                Role = role,
                BranchIds = branches.Select(x => x.Id).ToList(),
                BranchNames = branches.Select(x => x.Name).ToList(),
                IsActive = user.IsActive,
                IsBanned = IsBanned(user),
                CreatedAt = user.CreatedAt
            };
        }

        private static string NormalizeRole(string role)
        {
            return role.Trim().ToUpperInvariant();
        }

        private static bool IsBanned(ApplicationUser user)
        {
            return user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
        }

        private static bool IsDesc(UserListQuery query)
        {
            return query.SortDirection?.Equals("desc", StringComparison.OrdinalIgnoreCase) == true;
        }

        private static bool Contains(string? value, string search)
        {
            return value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;
        }

        private static void EnsureSucceeded(IdentityResult result)
        {
            if (result.Succeeded)
            {
                return;
            }

            var errors = result.Errors
                .GroupBy(error => error.Code)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.Description).ToArray());

            throw new ScanNow.Domain.Exceptions.ValidationException(errors);
        }
    }
}
