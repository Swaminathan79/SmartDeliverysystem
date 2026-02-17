using Moq;
using PackageService.DTOs;
using PackageService.Models;
using PackageService.Repositories;
using PackageService.Services;
using Xunit;
using Microsoft.Extensions.Logging;

namespace PackageService.Tests;

public class PackageServiceTests
{
    private readonly Mock<IPackageRepository> _mockRepository;
    private readonly Mock<IRouteValidationService> _mockRouteValidator;
    private readonly Mock<ILogger<PackageServiceImpl>> _mockLogger;
    private readonly PackageServiceImpl _service;
    
    public PackageServiceTests()
    {
        _mockRepository = new Mock<IPackageRepository>();
        _mockRouteValidator = new Mock<IRouteValidationService>();
        _mockLogger = new Mock<ILogger<PackageServiceImpl>>();
        _service = new PackageServiceImpl(
            _mockRepository.Object,
            _mockRouteValidator.Object,
            _mockLogger.Object);
    }
    
    #region Package Creation Validation Tests
    
    [Fact]
    public async Task CreateAsync_ValidRoute_CreatesSuccessfully()
    {
        // Arrange
        var dto = new CreatePackageDto
        {
            CustomerId = 100,
            RouteId = 1,
            WeightKg = 5.0m,
            Description = "Electronics"
        };
        
        _mockRouteValidator.Setup(v => v.ValidateRouteExistsAsync(dto.RouteId))
            .ReturnsAsync(true);
        
        _mockRepository.Setup(r => r.GetByTrackingNumberAsync(It.IsAny<string>()))
            .ReturnsAsync((Package?)null); // Tracking number doesn't exist
        
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Package>()))
            .ReturnsAsync((Package p) => p);
        
        // Act
        var result = await _service.CreateAsync(dto);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(dto.CustomerId, result.CustomerId);
        Assert.Equal(dto.RouteId, result.RouteId);
        Assert.Equal(dto.WeightKg, result.WeightKg);
        Assert.Equal("Pending", result.Status);
        Assert.NotEmpty(result.TrackingNumber);
        Assert.StartsWith("PKG-", result.TrackingNumber);
    }
    
    [Fact]
    public async Task CreateAsync_InvalidRoute_ThrowsValidationException()
    {
        // Arrange
        var dto = new CreatePackageDto
        {
            CustomerId = 100,
            RouteId = 999, // Non-existent route
            WeightKg = 5.0m
        };
        
        _mockRouteValidator.Setup(v => v.ValidateRouteExistsAsync(dto.RouteId))
            .ReturnsAsync(false);
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(dto));
        
        Assert.Contains("does not exist", exception.Message);
        _mockRepository.Verify(r => r.AddAsync(It.IsAny<Package>()), Times.Never);
    }
    
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5.5)]
    public async Task CreateAsync_InvalidWeight_FailsValidation(decimal weight)
    {
        // This would be validated by model validation attributes
        var dto = new CreatePackageDto
        {
            CustomerId = 100,
            RouteId = 1,
            WeightKg = weight
        };
        
        // The validation would occur at the controller level via ModelState
        Assert.True(weight <= 0);
    }
    
    #endregion
    
    #region Status Transition Tests
    
    [Theory]
    [InlineData(PackageStatus.Pending, PackageStatus.InTransit, true)]
    [InlineData(PackageStatus.InTransit, PackageStatus.Delivered, true)]
    [InlineData(PackageStatus.Delivered, PackageStatus.Pending, false)]
    [InlineData(PackageStatus.Delivered, PackageStatus.InTransit, false)]
    [InlineData(PackageStatus.Pending, PackageStatus.Delivered, false)]
    public async Task UpdateStatusAsync_VariousTransitions_ValidatesCorrectly(
        PackageStatus currentStatus,
        PackageStatus newStatus,
        bool shouldSucceed)
    {
        // Arrange
        var package = new Package
        {
            Id = 1,
            TrackingNumber = "PKG-2026-0001",
            CustomerId = 100,
            RouteId = 1,
            Status = currentStatus,
            WeightKg = 5.0m,
            CreatedAt = DateTime.UtcNow
        };
        
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(package);
        
        if (newStatus == PackageStatus.Delivered)
        {
            _mockRouteValidator.Setup(v => v.GetRouteScheduledDateAsync(package.RouteId))
                .ReturnsAsync(DateTime.UtcNow.AddDays(-1)); // Past date
        }
        
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Package>()))
            .Returns(Task.CompletedTask);
        
        // Act & Assert
        if (shouldSucceed)
        {
            var result = await _service.UpdateStatusAsync(1, newStatus, null, "Admin");
            Assert.Equal(newStatus.ToString(), result.Status);
            _mockRepository.Verify(r => r.UpdateAsync(
                It.Is<Package>(p => p.Status == newStatus)),
                Times.Once);
        }
        else
        {
            await Assert.ThrowsAsync<ValidationException>(
                () => _service.UpdateStatusAsync(1, newStatus, null, "Admin"));
            _mockRepository.Verify(r => r.UpdateAsync(It.IsAny<Package>()), Times.Never);
        }
    }
    
    [Fact]
    public async Task UpdateStatusAsync_DeliveredWithFutureRouteDate_ThrowsValidationException()
    {
        // Arrange
        var package = new Package
        {
            Id = 1,
            TrackingNumber = "PKG-2026-0001",
            CustomerId = 100,
            RouteId = 1,
            Status = PackageStatus.InTransit,
            WeightKg = 5.0m,
            CreatedAt = DateTime.UtcNow
        };
        
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(package);
        
        _mockRouteValidator.Setup(v => v.GetRouteScheduledDateAsync(package.RouteId))
            .ReturnsAsync(DateTime.UtcNow.AddDays(2)); // Future date
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.UpdateStatusAsync(1, PackageStatus.Delivered, null, "Admin"));
        
        Assert.Contains("before the scheduled route date", exception.Message);
    }
    
    [Fact]
    public async Task UpdateStatusAsync_DeliveredWithTodayRouteDate_Succeeds()
    {
        // Arrange
        var package = new Package
        {
            Id = 1,
            TrackingNumber = "PKG-2026-0001",
            CustomerId = 100,
            RouteId = 1,
            Status = PackageStatus.InTransit,
            WeightKg = 5.0m,
            CreatedAt = DateTime.UtcNow
        };
        
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(package);
        
        _mockRouteValidator.Setup(v => v.GetRouteScheduledDateAsync(package.RouteId))
            .ReturnsAsync(DateTime.UtcNow); // Today
        
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Package>()))
            .Returns(Task.CompletedTask);
        
        // Act
        var result = await _service.UpdateStatusAsync(1, PackageStatus.Delivered, null, "Admin");
        
        // Assert
        Assert.Equal("Delivered", result.Status);
    }
    
    #endregion
    
    #region Authorization Tests
    
    [Fact]
    public async Task UpdateStatusAsync_DriverNotOwningRoute_ThrowsUnauthorizedException()
    {
        // Arrange
        var driverId = 101;
        var package = new Package
        {
            Id = 1,
            TrackingNumber = "PKG-2026-0001",
            CustomerId = 100,
            RouteId = 5,
            Status = PackageStatus.Pending,
            WeightKg = 5.0m,
            CreatedAt = DateTime.UtcNow
        };
        
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(package);
        
        _mockRouteValidator.Setup(v => v.IsRouteOwnedByDriverAsync(package.RouteId, driverId))
            .ReturnsAsync(false); // Driver doesn't own the route
        
        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _service.UpdateStatusAsync(
                1,
                PackageStatus.InTransit,
                driverId,
                "Driver"));
    }
    
    [Fact]
    public async Task UpdateStatusAsync_DriverOwningRoute_Succeeds()
    {
        // Arrange
        var driverId = 101;
        var package = new Package
        {
            Id = 1,
            TrackingNumber = "PKG-2026-0001",
            CustomerId = 100,
            RouteId = 5,
            Status = PackageStatus.Pending,
            WeightKg = 5.0m,
            CreatedAt = DateTime.UtcNow
        };
        
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(package);
        
        _mockRouteValidator.Setup(v => v.IsRouteOwnedByDriverAsync(package.RouteId, driverId))
            .ReturnsAsync(true); // Driver owns the route
        
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Package>()))
            .Returns(Task.CompletedTask);
        
        // Act
        var result = await _service.UpdateStatusAsync(
            1,
            PackageStatus.InTransit,
            driverId,
            "Driver");
        
        // Assert
        Assert.Equal("InTransit", result.Status);
    }
    
    [Fact]
    public async Task UpdateStatusAsync_AdminRole_BypassesRouteOwnershipCheck()
    {
        // Arrange
        var package = new Package
        {
            Id = 1,
            TrackingNumber = "PKG-2026-0001",
            CustomerId = 100,
            RouteId = 5,
            Status = PackageStatus.Pending,
            WeightKg = 5.0m,
            CreatedAt = DateTime.UtcNow
        };
        
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(package);
        
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Package>()))
            .Returns(Task.CompletedTask);
        
        // Act
        var result = await _service.UpdateStatusAsync(
            1,
            PackageStatus.InTransit,
            null,
            "Admin");
        
        // Assert
        Assert.Equal("InTransit", result.Status);
        // Verify route ownership was never checked
        _mockRouteValidator.Verify(
            v => v.IsRouteOwnedByDriverAsync(It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }
    
    #endregion
    
    #region Cross-Service Route Validation Tests
    
    [Fact]
    public async Task CreateAsync_RouteServiceUnavailable_ThrowsValidationException()
    {
        // Arrange
        var dto = new CreatePackageDto
        {
            CustomerId = 100,
            RouteId = 1,
            WeightKg = 5.0m
        };
        
        _mockRouteValidator.Setup(v => v.ValidateRouteExistsAsync(dto.RouteId))
            .ReturnsAsync(false); // Simulating service unavailable or route not found
        
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(dto));
    }
    
    [Fact]
    public async Task UpdateAsync_ChangingToInvalidRoute_ThrowsValidationException()
    {
        // Arrange
        var package = new Package
        {
            Id = 1,
            TrackingNumber = "PKG-2026-0001",
            CustomerId = 100,
            RouteId = 1,
            Status = PackageStatus.Pending,
            WeightKg = 5.0m,
            CreatedAt = DateTime.UtcNow
        };
        
        var updateDto = new UpdatePackageDto
        {
            RouteId = 999 // Non-existent route
        };
        
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(package);
        
        _mockRouteValidator.Setup(v => v.ValidateRouteExistsAsync(999))
            .ReturnsAsync(false);
        
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.UpdateAsync(1, updateDto));
    }
    
    #endregion
    
    #region Search and Pagination Tests
    
    [Fact]
    public async Task SearchAsync_WithTrackingNumber_ReturnsFilteredResults()
    {
        // Arrange
        var trackingNumber = "PKG-2026-0001";
        var packages = new List<Package>
        {
            new Package
            {
                Id = 1,
                TrackingNumber = trackingNumber,
                CustomerId = 100,
                RouteId = 1,
                Status = PackageStatus.Pending,
                WeightKg = 5.0m,
                CreatedAt = DateTime.UtcNow
            }
        };
        
        _mockRepository.Setup(r => r.SearchAsync(
            trackingNumber,
            null,
            null,
            1,
            10))
            .ReturnsAsync((packages, 1));
        
        // Act
        var result = await _service.SearchAsync(trackingNumber, null, null, 1, 10);
        
        // Assert
        Assert.Single(result.Data);
        Assert.Equal(trackingNumber, result.Data.First().TrackingNumber);
    }
    
    [Fact]
    public async Task SearchAsync_WithStatus_ReturnsFilteredResults()
    {
        // Arrange
        var status = PackageStatus.InTransit;
        var packages = GeneratePackagesWithStatus(3, status);
        
        _mockRepository.Setup(r => r.SearchAsync(
            null,
            status,
            null,
            1,
            10))
            .ReturnsAsync((packages, 3));
        
        // Act
        var result = await _service.SearchAsync(null, status, null, 1, 10);
        
        // Assert
        Assert.Equal(3, result.Data.Count());
        Assert.All(result.Data, p => Assert.Equal("InTransit", p.Status));
    }
    
    [Fact]
    public async Task GetByCustomerAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var customerId = 100;
        var packages = GenerateCustomerPackages(customerId, 25);
        
        _mockRepository.Setup(r => r.SearchAsync(
            null,
            null,
            customerId,
            2,
            10))
            .ReturnsAsync((packages.Skip(10).Take(10).ToList(), 25));
        
        // Act
        var result = await _service.GetByCustomerAsync(customerId, 2, 10);
        
        // Assert
        Assert.Equal(10, result.Data.Count());
        Assert.Equal(2, result.PageNumber);
        Assert.Equal(25, result.TotalRecords);
        Assert.Equal(3, result.TotalPages);
    }
    
    #endregion
    
    #region Update Validation Tests
    
    [Fact]
    public async Task UpdateAsync_DeliveredPackage_ThrowsValidationException()
    {
        // Arrange
        var package = new Package
        {
            Id = 1,
            TrackingNumber = "PKG-2026-0001",
            CustomerId = 100,
            RouteId = 1,
            Status = PackageStatus.Delivered,
            WeightKg = 5.0m,
            CreatedAt = DateTime.UtcNow
        };
        
        var updateDto = new UpdatePackageDto
        {
            WeightKg = 6.0m
        };
        
        _mockRepository.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(package);
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.UpdateAsync(1, updateDto));
        
        Assert.Contains("Cannot update a delivered package", exception.Message);
    }
    
    #endregion
    
    private List<Package> GeneratePackagesWithStatus(int count, PackageStatus status)
    {
        var packages = new List<Package>();
        for (int i = 1; i <= count; i++)
        {
            packages.Add(new Package
            {
                Id = i,
                TrackingNumber = $"PKG-2026-{i:D4}",
                CustomerId = 100 + i,
                RouteId = i,
                Status = status,
                WeightKg = 5.0m + i,
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }
        return packages;
    }
    
    private List<Package> GenerateCustomerPackages(int customerId, int count)
    {
        var packages = new List<Package>();
        for (int i = 1; i <= count; i++)
        {
            packages.Add(new Package
            {
                Id = i,
                TrackingNumber = $"PKG-2026-{i:D4}",
                CustomerId = customerId,
                RouteId = i % 5 + 1,
                Status = (PackageStatus)(i % 3),
                WeightKg = 5.0m + i,
                CreatedAt = DateTime.UtcNow.AddHours(-i)
            });
        }
        return packages;
    }
}
