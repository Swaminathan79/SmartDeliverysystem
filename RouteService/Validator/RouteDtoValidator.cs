using FluentValidation;
using RouteService.DTOs;

namespace RouteService.Validator
{
    public class RouteDtoValidator : AbstractValidator<CreateRouteDto>
    {
        public RouteDtoValidator()
        {
            RuleFor(x => x.DriverId)
                .GreaterThan(0);

            RuleFor(x => x.VehicleId)
                .GreaterThan(0);

            RuleFor(x => x.StartLocation)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.EndLocation)
                .NotEmpty()
                .MaximumLength(100);

            RuleFor(x => x.EstimatedDistanceKm)
                .GreaterThan(0);

            RuleFor(x => x.ScheduledDate)
                .Must(date => date.Date >= DateTime.UtcNow.Date)
                .WithMessage("Scheduled date cannot be in the past");
        }
    }

    public class UpdateRouteDtoValidator : AbstractValidator<UpdateRouteDto>
    {
        public UpdateRouteDtoValidator()
        {
            RuleFor(x => x.StartLocation)
                .MaximumLength(100);

            RuleFor(x => x.EndLocation)
                .MaximumLength(100);

            RuleFor(x => x.EstimatedDistanceKm)
                .GreaterThan(0)
                .When(x => x.EstimatedDistanceKm.HasValue);

            RuleFor(x => x.ScheduledDate)
                .Must(date => date.Value.Date >= DateTime.UtcNow.Date)
                .When(x => x.ScheduledDate.HasValue)
                .WithMessage("Scheduled date cannot be in the past");
        }
    }


    public class AssignDriverDtoValidator : AbstractValidator<AssignDriverDto>
    {
        public AssignDriverDtoValidator()
        {
            RuleFor(x => x.RouteId)
                .GreaterThan(0)
                .WithMessage("RouteId is required");

            RuleFor(x => x.DriverId)
                .GreaterThan(0)
                .WithMessage("DriverId is required");
        }
    }

}
