using FluentValidation;
using AuthService.DTOs;

namespace AuthService.BuildingBlocks.Validators
{
    public class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
    {
        public UpdateUserDtoValidator()
        {
            RuleFor(x => x)
                .NotNull()
                .WithMessage("User data is required.");

            RuleFor(x => x.Email)
                .EmailAddress()
                .When(x => !string.IsNullOrEmpty(x.Email));

            RuleFor(x => x.Role)
                .IsInEnum()
                .When(x => x.Role.HasValue);
        }
    }

}
