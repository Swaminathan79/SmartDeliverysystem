using PackageService.DTOs;
using PackageService.Models;
using PackageService.Services;
using RouteService.Repositories;
using System.ComponentModel.DataAnnotations;
using ValidationException = PackageService.Services.ValidationException;



namespace PackageService.BuildingBlocks.Validators
{
    public static class PackageServiceValidator
    {
        private const int MAX_PAGE_SIZE = 50;

        public static void ValidateId(int id)
        {
            if (id <= 0)
                throw new ValidationException("Id must be greater than zero.");
        }

        public static void ValidateTrackingNumber(string trackingNumber)
        {
            if (string.IsNullOrWhiteSpace(trackingNumber))
                throw new ValidationException("TrackingNumber is required.");
        }

        public static void ValidateCreateDto(CreatePackageDto dto)
        {
            if (dto == null)
                throw new ValidationException("CreatePackageDto is required.");

            if (dto.CustomerId <= 0)
                throw new ValidationException("CustomerId must be greater than zero.");

            if (dto.RouteId <= 0)
                throw new ValidationException("RouteId must be greater than zero.");

            if (dto.WeightKg <= 0)
                throw new ValidationException("Weight must be greater than zero.");
        }

        public static void ValidateUpdateDto(UpdatePackageDto dto)
        {
            if (dto == null)
                throw new ValidationException("UpdatePackageDto is required.");

            if (dto.RouteId.HasValue && dto.RouteId.Value <= 0)
                throw new ValidationException("RouteId must be greater than zero.");
        }

        public static void ValidateStatusUpdate(int id)
        {
            if (id <= 0)
                throw new ValidationException("PackageId must be greater than zero.");
        }

        public static void ValidatePagination(int pageNumber, int pageSize)
        {
            if (pageNumber <= 0)
                throw new ValidationException("PageNumber must be greater than zero.");

            if (pageSize <= 0)
                throw new ValidationException("PageSize must be greater than zero.");

            // Clamp max page size (VERY IMPORTANT)
            if (pageSize > MAX_PAGE_SIZE)
                pageSize = MAX_PAGE_SIZE;

            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = MAX_PAGE_SIZE;

        }
    }

}
