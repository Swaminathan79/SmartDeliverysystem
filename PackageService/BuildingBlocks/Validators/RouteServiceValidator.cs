using System.ComponentModel.DataAnnotations;

namespace PackageService.BuildingBlocks.Validators
{
    public static class RouteServiceValidator
    {
        public static void ValidateRouteId(int routeId)
        {
            if (routeId <= 0)
                throw new ValidationException("RouteId must be greater than zero.");
        }

        public static void ValidateRouteAndDriver(int routeId, int driverId)
        {
            if (routeId <= 0)
                throw new ValidationException("RouteId must be greater than zero.");

            if (driverId <= 0)
                throw new ValidationException("DriverId must be greater than zero.");
        }
    }

}
