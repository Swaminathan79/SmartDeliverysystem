using FluentValidation;
using AuthService.DTOs;


namespace AuthService.Validators
{
    public class LoginDtoValidator : AbstractValidator<LoginDto>
    {
        public LoginDtoValidator()
        {
            RuleFor(x => x)
                .NotNull()
                .WithMessage("Login data is required.");

            RuleFor(x => x.Username)
                .NotEmpty();

            RuleFor(x => x.Password)
                .NotEmpty();
        }
    }

}
