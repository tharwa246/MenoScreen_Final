using MenoScreen.Data;
using MenoScreen.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Patients;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public List<Patient> Patients { get; set; } = new();

    public async Task OnGetAsync()
    {
        Patients = await _db.Patients.OrderBy(p => p.FullName).ToListAsync();
    }
}