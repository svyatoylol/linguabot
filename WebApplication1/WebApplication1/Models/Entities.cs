using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApplication1.Models
{
    [Table("Users")]
    public class User
    {
        [Key] public int Id { get; set; }
        [Required, MaxLength(150)] public string Email { get; set; } = string.Empty;
        [Required] public string PasswordHash { get; set; } = string.Empty;
        [Required, MaxLength(50)] public string Role { get; set; } = "Student";
    }

    [Table("DialogSessions")]
    public class DialogSession
    {
        [Key] public int Id { get; set; }
        public int? UserId { get; set; }
        public string GuestSessionGuid { get; set; } = string.Empty;
        public string ScenarioCode { get; set; } = "cafe";
        public string Lang { get; set; } = "EN";
        public string Level { get; set; } = "A1-A2";
        public bool IsFinished { get; set; } = false;
    }

    [Table("DialogMessages")]
    public class DialogMessage
    {
        [Key] public int Id { get; set; }
        public int DialogSessionId { get; set; }
        public string Sender { get; set; } = "User";
        public string ContentText { get; set; } = string.Empty;
        public bool UsedHint { get; set; } = false;
    }
}