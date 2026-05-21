using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScanNow.Application.Abstractions;
using ScanNow.Application.DTOs;
using ScanNow.Application.Features.RestaurantManagement.DTOs;
using ScanNow.Domain.Enums;

namespace ScanNow.Web.Controllers
{
    [ApiController]
    [Route("api/owner/restaurant")]
    [Authorize(Roles = nameof(UserRole.OWNER))]
    public class OwnerRestaurantController : ControllerBase
    {
        private readonly IRestaurantManagementService _restaurantManagementService;

        public OwnerRestaurantController(IRestaurantManagementService restaurantManagementService)
        {
            _restaurantManagementService = restaurantManagementService;
        }

        [HttpGet("me")]
        public async Task<ActionResult<ApiResponse<RestaurantResponse?>>> GetMyRestaurant()
        {
            return new ApiResponse<RestaurantResponse?>
            {
                Result = await _restaurantManagementService.GetCurrentOwnerRestaurantAsync(),
                Message = "Get restaurant successfully"
            };
        }

        [HttpPut("me")]
        public async Task<ActionResult<ApiResponse<RestaurantResponse>>> UpdateMyRestaurant([FromBody] UpdateRestaurantRequest request)
        {
            return new ApiResponse<RestaurantResponse>
            {
                Result = await _restaurantManagementService.UpdateCurrentOwnerRestaurantAsync(request),
                Message = "Update restaurant successfully"
            };
        }
    }
}
