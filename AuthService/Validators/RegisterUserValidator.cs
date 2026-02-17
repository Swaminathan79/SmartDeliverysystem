using AuthService.DTOs;
using FluentValidation;
using System.Text.RegularExpressions;
using AuthService.Models;

namespace AuthService.Validation
{
    public class RegisterDtoValidator : AbstractValidator<RegisterDto>
    {
        public RegisterDtoValidator()
        {
            RuleFor(x => x)
                .NotNull()
                .WithMessage("User data is required.");

            RuleFor(x => x.Username)
                .NotEmpty()
                .MinimumLength(3)
                .MaximumLength(50);

            RuleFor(x => x.Email)
                .NotEmpty()
                .EmailAddress();

            RuleFor(x => x.Password)
                .NotEmpty()
                .Must(IsStrongPassword)
                .WithMessage("Password must contain uppercase, lowercase, number and special character");

            RuleFor(x => x.Role)
                .IsInEnum();

           /* RuleFor(x => x.DriverId)
                .NotNull()
                .When(x => x.Role == UserRole.Driver)
                .WithMessage("DriverId is required for Driver role"); */
        }

        private bool IsStrongPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            return password.Length >= 8 &&
                   Regex.IsMatch(password, @"[A-Z]") &&
                   Regex.IsMatch(password, @"[a-z]") &&
                   Regex.IsMatch(password, @"\d") &&
                   Regex.IsMatch(password, @"[!@#$%^&*(),.?""':{}|<>]");
        }
    }

}
