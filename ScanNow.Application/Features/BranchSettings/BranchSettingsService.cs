using FluentValidation;
using ScanNow.Application.Abstractions;
using ScanNow.Application.Exceptions;
using ScanNow.Application.Features.BranchSettings.DTOs;
using ScanNow.Domain.Abstractions.Persistence;
using ScanNow.Domain.Entities;
using ScanNow.Domain.Enums;
using ScanNow.Domain.Exceptions;

namespace ScanNow.Application.Features.BranchSettings
{
    public class BranchSettingsService : IBranchSettingsService
    {
        private static readonly string OwnerRole = UserRole.OWNER.ToString();
        private static readonly string BranchManagerRole = UserRole.BRANCH_MANAGER.ToString();

        private readonly IBranchSettingsRepository _repository;
        private readonly ITableQrRepository _tableRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IValidator<UpsertBranchPaymentConfigRequest> _paymentConfigValidator;
        private readonly IValidator<CreatePaperVoucherRequest> _createVoucherValidator;
        private readonly IValidator<UpdatePaperVoucherRequest> _updateVoucherValidator;

        public BranchSettingsService(
            IBranchSettingsRepository repository,
            ITableQrRepository tableRepository,
            ICurrentUserService currentUserService,
            IUnitOfWork unitOfWork,
            IValidator<UpsertBranchPaymentConfigRequest> paymentConfigValidator,
            IValidator<CreatePaperVoucherRequest> createVoucherValidator,
            IValidator<UpdatePaperVoucherRequest> updateVoucherValidator)
        {
            _repository = repository;
            _tableRepository = tableRepository;
            _currentUserService = currentUserService;
            _unitOfWork = unitOfWork;
            _paymentConfigValidator = paymentConfigValidator;
            _createVoucherValidator = createVoucherValidator;
            _updateVoucherValidator = updateVoucherValidator;
        }

        public async Task<BranchPaymentConfigResponse> GetPaymentConfigAsync(Guid branchId)
        {
            await EnsureCanManageBranchAsync(branchId);
            var config = await _repository.GetPaymentConfigAsync(branchId);
            return MapPaymentConfig(branchId, config);
        }

        public async Task<BranchPaymentConfigResponse> UpsertPaymentConfigAsync(Guid branchId, UpsertBranchPaymentConfigRequest request)
        {
            await _paymentConfigValidator.ValidateAndThrowAsync(request);
            await EnsureCanManageBranchAsync(branchId);

            var config = await _repository.GetPaymentConfigAsync(branchId);
            var now = DateTime.UtcNow;

            if (config is null)
            {
                config = new BranchPaymentConfig
                {
                    Id = Guid.NewGuid(),
                    BranchId = branchId,
                    CreatedAt = now
                };
                await _repository.AddPaymentConfigAsync(config);
            }

            var payOsClientId = KeepExistingWhenBlank(request.PayOsClientId, config.PayOsClientId);
            var payOsApiKey = KeepExistingWhenBlank(request.PayOsApiKey, config.PayOsApiKey);
            var payOsChecksumKey = KeepExistingWhenBlank(request.PayOsChecksumKey, config.PayOsChecksumKey);

            if (request.PayOsEnabled
                && (string.IsNullOrWhiteSpace(payOsClientId)
                    || string.IsNullOrWhiteSpace(payOsApiKey)
                    || string.IsNullOrWhiteSpace(payOsChecksumKey)))
            {
                throw new BusinessRuleException("PayOS credentials are incomplete. Please enter Client ID, API Key and Checksum Key.");
            }

            config.CashEnabled = true;
            config.PayOsEnabled = request.PayOsEnabled;
            config.PayOsClientId = payOsClientId;
            config.PayOsApiKey = payOsApiKey;
            config.PayOsChecksumKey = payOsChecksumKey;
            config.DefaultMethod = request.PayOsEnabled ? request.DefaultMethod : PaymentMethod.CASH;
            config.UpdatedAt = now;

            await _unitOfWork.SaveChangesAsync();
            return MapPaymentConfig(branchId, config);
        }

        public async Task<List<PaperVoucherResponse>> GetPaperVouchersAsync(Guid branchId)
        {
            await EnsureCanManageBranchAsync(branchId);
            var vouchers = await _repository.GetPaperVouchersAsync(branchId);
            return vouchers.Select(MapVoucher).ToList();
        }

        public async Task<PaperVoucherResponse> CreatePaperVoucherAsync(Guid branchId, CreatePaperVoucherRequest request)
        {
            await _createVoucherValidator.ValidateAndThrowAsync(request);
            await EnsureCanManageBranchAsync(branchId);

            var code = NormalizeCode(request.Code);
            if (await _repository.PaperVoucherCodeExistsAsync(branchId, code))
            {
                throw new ConflictException("Voucher code already exists in this branch.");
            }

            var now = DateTime.UtcNow;
            var voucher = new PaperVoucher
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                Code = code,
                Name = request.Name.Trim(),
                Description = NormalizeNullable(request.Description),
                DiscountType = request.DiscountType,
                DiscountValue = request.DiscountValue,
                MinOrderAmount = request.MinOrderAmount,
                MaxDiscountAmount = request.MaxDiscountAmount,
                Quantity = request.Quantity,
                IsActive = request.IsActive,
                ValidFrom = request.ValidFrom,
                ValidUntil = request.ValidUntil,
                CreatedAt = now,
                UpdatedAt = now
            };

            await _repository.AddPaperVoucherAsync(voucher);
            await _unitOfWork.SaveChangesAsync();
            return MapVoucher(voucher);
        }

        public async Task<PaperVoucherResponse> UpdatePaperVoucherAsync(Guid branchId, Guid voucherId, UpdatePaperVoucherRequest request)
        {
            await _updateVoucherValidator.ValidateAndThrowAsync(request);
            await EnsureCanManageBranchAsync(branchId);

            var voucher = await _repository.GetPaperVoucherAsync(branchId, voucherId)
                ?? throw new NotFoundException("Voucher not found");
            var code = NormalizeCode(request.Code);

            if (await _repository.PaperVoucherCodeExistsAsync(branchId, code, voucherId))
            {
                throw new ConflictException("Voucher code already exists in this branch.");
            }

            if (request.Quantity < voucher.UsedCount)
            {
                throw new BusinessRuleException("Quantity cannot be lower than used count.");
            }

            voucher.Code = code;
            voucher.Name = request.Name.Trim();
            voucher.Description = NormalizeNullable(request.Description);
            voucher.DiscountType = request.DiscountType;
            voucher.DiscountValue = request.DiscountValue;
            voucher.MinOrderAmount = request.MinOrderAmount;
            voucher.MaxDiscountAmount = request.MaxDiscountAmount;
            voucher.Quantity = request.Quantity;
            voucher.ValidFrom = request.ValidFrom;
            voucher.ValidUntil = request.ValidUntil;
            voucher.IsActive = request.IsActive;
            voucher.UpdatedAt = DateTime.UtcNow;

            await _unitOfWork.SaveChangesAsync();
            return MapVoucher(voucher);
        }

        private async Task EnsureCanManageBranchAsync(Guid branchId)
        {
            var branch = await _tableRepository.GetBranchByIdAsync(branchId)
                ?? throw new NotFoundException("Branch not found");
            var userId = _currentUserService.UserId ?? throw new UnauthorizedException();
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

        private static BranchPaymentConfigResponse MapPaymentConfig(Guid branchId, BranchPaymentConfig? config)
        {
            if (config is null)
            {
                return new BranchPaymentConfigResponse { BranchId = branchId, CashEnabled = true };
            }

            return new BranchPaymentConfigResponse
            {
                PaymentConfigId = config.Id,
                BranchId = config.BranchId,
                CashEnabled = config.CashEnabled,
                PayOsEnabled = config.PayOsEnabled,
                HasPayOsClientId = !string.IsNullOrWhiteSpace(config.PayOsClientId),
                HasPayOsApiKey = !string.IsNullOrWhiteSpace(config.PayOsApiKey),
                HasPayOsChecksumKey = !string.IsNullOrWhiteSpace(config.PayOsChecksumKey),
                PayOsClientIdPreview = PreviewSecret(config.PayOsClientId),
                DefaultMethod = config.DefaultMethod,
                UpdatedAt = config.UpdatedAt
            };
        }

        private static PaperVoucherResponse MapVoucher(PaperVoucher voucher)
        {
            return new PaperVoucherResponse
            {
                VoucherId = voucher.Id,
                BranchId = voucher.BranchId,
                Code = voucher.Code,
                Name = voucher.Name,
                Description = voucher.Description,
                DiscountType = voucher.DiscountType,
                DiscountValue = voucher.DiscountValue,
                MinOrderAmount = voucher.MinOrderAmount,
                MaxDiscountAmount = voucher.MaxDiscountAmount,
                Quantity = voucher.Quantity,
                UsedCount = voucher.UsedCount,
                RemainingCount = Math.Max(0, voucher.Quantity - voucher.UsedCount),
                ValidFrom = voucher.ValidFrom,
                ValidUntil = voucher.ValidUntil,
                IsActive = voucher.IsActive,
                QrPayload = $"SCANNOW-VOUCHER:{voucher.BranchId}:{voucher.Code}",
                CreatedAt = voucher.CreatedAt,
                UpdatedAt = voucher.UpdatedAt
            };
        }

        private static string NormalizeCode(string value) => value.Trim().ToUpperInvariant();

        private static string? NormalizeNullable(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string? KeepExistingWhenBlank(string? incoming, string? existing)
        {
            return string.IsNullOrWhiteSpace(incoming) ? existing : incoming.Trim();
        }

        private static string? PreviewSecret(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Length <= 6 ? "***" : $"{value[..3]}***{value[^3..]}";
        }
    }
}
