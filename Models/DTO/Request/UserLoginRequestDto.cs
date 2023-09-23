using System.ComponentModel.DataAnnotations;

namespace TodoApp.Models.DTO.Request
{
    public class UserLoginRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }

    }
}
