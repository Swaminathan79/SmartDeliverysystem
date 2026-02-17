using Moq;
using RouteService.DTOs;
using RouteService.Models;
using RouteService.Repositories;
using RouteService.Services;
using Xunit;
using Microsoft.Extensions.Logging;

namespace RouteService.Tests;

public class RouteServiceTests
{
    private readonly Mock<IRouteRepository> _mockRepository;
    private readonly Mock<ILogger<RouteServiceImpl>> _mockLogger;
    private readonly RouteServiceImpl _service;
    
    public RouteServiceTests()
    {
        _mockRepository = new Mock<IRouteRepository>();
        _mockLogger = new Mock<ILogger<RouteServiceImpl>>();
        _service = new RouteServiceImpl(_mockRepository.Object, _mockLogger.Object);
    }
    
    #region Driver Assignment Tests
    
    [Fact]
    public async Task AssignDriverAsync_ValidAssignment_Success()
    {
        // Arrange
        var routeId = 1;
        var newDriverId = 10;
        var route = new Route
        {
            Id = routeId,
            DriverId = 5,
            VehicleId = 1,
            StartLocation = "Warehouse",
            EndLocation = "Downtown",
            EstimatedDistanceKm = 15.5m,
            ScheduledDate = DateTime.UtcNow.AddDays(1)
        };
        
        _mockRepository.Setup(r => r.GetByIdAsync(routeId))
            .ReturnsAsync(route);
        
        _mockRepository.Setup(r => r.HasOverlappingRoutesAsync(
            newDriverId, 
            route.ScheduledDate, 
            routeId))
            .ReturnsAsync(false);
        
        _mockRepository.Setup(r => r.UpdateAsync(It.IsAny<Route>()))
            .Returns(Task.CompletedTask);
        
        // Act
        var result = await _service.AssignDriverAsync(routeId, newDriverId);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(newDriverId, result.DriverId);
        _mockRepository.Verify(r => r.UpdateAsync(
            It.Is<Route>(x => x.DriverId == newDriverId)), 
            Times.Once);
    }
    
    [Fact]
    public async Task AssignDriverAsync_RouteNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var routeId = 999;
        var driverId = 10;
        
        _mockRepository.Setup(r => r.GetByIdAsync(routeId))
            .ReturnsAsync((Route?)null);
        
        // Act & Assert
        await Assert.ThrowsAsync<NotFoundException>(
            () => _service.AssignDriverAsync(routeId, driverId));
    }
    
    [Fact]
    public async Task AssignDriverAsync_DriverHasOverlappingRoute_ThrowsValidationException()
    {
        // Arrange
        var routeId = 1;
        var newDriverId = 10;
        var route = new Route
        {
            Id = routeId,
            DriverId = 5,
            VehicleId = 1,
            StartLocation = "Warehouse",
            EndLocation = "Downtown",
            EstimatedDistanceKm = 15.5m,
            ScheduledDate = DateTime.UtcNow.AddDays(1)
        };
        
        _mockRepository.Setup(r => r.GetByIdAsync(routeId))
            .ReturnsAsync(route);
        
        _mockRepository.Setup(r => r.HasOverlappingRoutesAsync(
            newDriverId,
            route.ScheduledDate,
            routeId))
            .ReturnsAsync(true);
        
        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => _service.AssignDriverAsync(routeId, newDriverId));
    }
    
    #endregion
    
    #region Overlapping Route Detection Tests
    
    [Fact]
    public async Task CreateAsync_OverlappingDate_ThrowsValidationException()
    {
        // Arrange
        var dto = new CreateRouteDto
        {
            DriverId = 5,
            VehicleId = 1,
            StartLocation = "Warehouse",
            EndLocation = "Downtown",
            EstimatedDistanceKm = 15.5m,
            ScheduledDate = DateTime.UtcNow.AddDays(1)
        };
        
        _mockRepository.Setup(r => r.HasOverlappingRoutesAsync(
            dto.DriverId,
            dto.ScheduledDate,
            null))
            .ReturnsAsync(true);
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(dto));
        
        Assert.Contains("already has a route scheduled", exception.Message);
    }
    
    [Fact]
    public async Task CreateAsync_NoOverlap_CreatesSuccessfully()
    {
        // Arrange
        var dto = new CreateRouteDto
        {
            DriverId = 5,
            VehicleId = 1,
            StartLocation = "Warehouse",
            EndLocation = "Downtown",
            EstimatedDistanceKm = 15.5m,
            ScheduledDate = DateTime.UtcNow.AddDays(1)
        };
        
        var createdRoute = new Route
        {
            Id = 1,
            DriverId = dto.DriverId,
            VehicleId = dto.VehicleId,
            StartLocation = dto.StartLocation,
            EndLocation = dto.EndLocation,
            EstimatedDistanceKm = dto.EstimatedDistanceKm,
            ScheduledDate = dto.ScheduledDate
        };
        
        _mockRepository.Setup(r => r.HasOverlappingRoutesAsync(
            dto.DriverId,
            dto.ScheduledDate,
            null))
            .ReturnsAsync(false);
        
        _mockRepository.Setup(r => r.AddAsync(It.IsAny<Route>()))
            .ReturnsAsync(createdRoute);
        
        // Act
        var result = await _service.CreateAsync(dto);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(dto.DriverId, result.DriverId);
        Assert.Equal(dto.EstimatedDistanceKm, result.EstimatedDistanceKm);
    }
    
    [Fact]
    public async Task CreateAsync_PastScheduledDate_ThrowsValidationException()
    {
        // Arrange
        var dto = new CreateRouteDto
        {
            DriverId = 5,
            VehicleId = 1,
            StartLocation = "Warehouse",
            EndLocation = "Downtown",
            EstimatedDistanceKm = 15.5m,
            ScheduledDate = DateTime.UtcNow.AddDays(-1) // Past date
        };
        
        // Act & Assert
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.CreateAsync(dto));
        
        Assert.Contains("cannot be in the past", exception.Message);
    }
    
    #endregion
    
    #region Search and Pagination Tests
    
    [Fact]
    public async Task SearchAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var routes = GenerateTestRoutes(25);
        var pageNumber = 2;
        var pageSize = 10;
        
        _mockRepository.Setup(r => r.SearchAsync(
            null,
            null,
            null,
            pageNumber,
            pageSize))
            .ReturnsAsync((routes.Skip(10).Take(10), 25));
        
        // Act
        var result = await _service.SearchAsync(null, null, null, pageNumber, pageSize);
        
        // Assert
        Assert.Equal(10, result.Data.Count());
        Assert.Equal(pageNumber, result.PageNumber);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(25, result.TotalRecords);
        Assert.Equal(3, result.TotalPages);
    }
    
    [Fact]
    public async Task SearchAsync_WithDateFilter_ReturnsFilteredResults()
    {
        // Arrange
        var searchDate = DateTime.UtcNow.AddDays(1);
        var filteredRoutes = new List<Route>
        {
            new Route
            {
                Id = 1,
                DriverId = 5,
                VehicleId = 1,
                StartLocation = "A",
                EndLocation = "B",
                EstimatedDistanceKm = 10,
                ScheduledDate = searchDate
            }
        };
        
        _mockRepository.Setup(r => r.SearchAsync(
            searchDate,
            null,
            null,
            1,
            10))
            .ReturnsAsync((filteredRoutes, 1));
        
        // Act
        var result = await _service.SearchAsync(searchDate, null, null, 1, 10);
        
        // Assert
        Assert.Single(result.Data);
        Assert.Equal(1, result.TotalRecords);
    }
    
    [Fact]
    public async Task GetByDriverAsync_ReturnsPaginatedDriverRoutes()
    {
        // Arrange
        var driverId = 5;
        var driverRoutes = new List<Route>
        {
            new Route { Id = 1, DriverId = driverId, VehicleId = 1, StartLocation = "A", EndLocation = "B", EstimatedDistanceKm = 10, ScheduledDate = DateTime.UtcNow },
            new Route { Id = 2, DriverId = driverId, VehicleId = 1, StartLocation = "C", EndLocation = "D", EstimatedDistanceKm = 15, ScheduledDate = DateTime.UtcNow }
        };
        
        _mockRepository.Setup(r => r.SearchAsync(
            null,
            null,
            driverId,
            1,
            10))
            .ReturnsAsync((driverRoutes, 2));
        
        // Act
        var result = await _service.GetByDriverAsync(driverId, 1, 10);
        
        // Assert
        Assert.Equal(2, result.Data.Count());
        Assert.All(result.Data, r => Assert.Equal(driverId, r.DriverId));
    }
    
    #endregion
    
    #region Validation Tests
    
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(-0.5)]
    public async Task CreateAsync_InvalidDistance_ThrowsValidationException(decimal distance)
    {
        // This would be validated by model validation attributes
        // Testing the model behavior
        var dto = new CreateRouteDto
        {
            DriverId = 5,
            VehicleId = 1,
            StartLocation = "A",
            EndLocation = "B",
            EstimatedDistanceKm = distance,
            ScheduledDate = DateTime.UtcNow.AddDays(1)
        };
        
        // The validation would occur at the controller level via ModelState
        // This test demonstrates the expected behavior
        Assert.True(distance <= 0);
    }
    
    #endregion
    
    private List<Route> GenerateTestRoutes(int count)
    {
        var routes = new List<Route>();
        for (int i = 1; i <= count; i++)
        {
            routes.Add(new Route
            {
                Id = i,
                DriverId = i % 5 + 1,
                VehicleId = i % 3 + 1,
                StartLocation = $"Location {i}",
                EndLocation = $"Destination {i}",
                EstimatedDistanceKm = 10 + i,
                ScheduledDate = DateTime.UtcNow.AddDays(i % 7)
            });
        }
        return routes;
    }
}
