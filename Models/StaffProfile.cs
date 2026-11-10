using System.ComponentModel.DataAnnotations;

namespace MenoScreen.Models;

// EXTRA DETAILS FOR A STAFF LOGIN
// Used to only stores email and password
// this table adds theperson's real name and the clinic they work at
// UserId is the login's Id
// Admins have no clinic (ClinicId stays empty) because they look after every clinic
public class StaffProfile
{
    [Key, MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    public int? ClinicId { get; set; }
    public Clinic? Clinic { get; set; }
}