using System.ComponentModel.DataAnnotations;

namespace AuthService.Validation
{
    public static class Guard
    {
        public static T AgainstNull<T>(this T value, string name)
        {
            if (value == null)
                throw new ValidationException($"{name} is required.");

            return value;
        }
    }

}
