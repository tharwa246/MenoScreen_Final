using System.ComponentModel.DataAnnotations;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace MenoScreen.Pages.Admin;

// MANAGE CLINICS (admins only: the whole /Admin folder needs the AdminOnly policy)
// Add a clinic, rename it, or delete it. A clinic that still has patients can't be
// deleted, because her records would have nowhere to belong (the database refuses it too).
public class ClinicsModel : PageModel
{
    private readonly ApplicationDbContext _db;
    private readonly AuditService _audit;

    public ClinicsModel(ApplicationDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public record ClinicRow(int Id, string Name, string? Area, int Patients, int Staff);
    public List<ClinicRow> Clinics { get; set; } = new();

    [BindProperty]
    public ClinicInput Input { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public class ClinicInput
    {
        [Required, MaxLength(100), Display(Name = "Clinic name")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100), Display(Name = "Area (suburb or town)")]
        public string? Area { get; set; }
    }

    public async Task OnGetAsync() => await LoadAsync();

    // "Add clinic" button
    public async Task<IActionResult> OnPostAddAsync()
    {
        if (ModelState.IsValid && await _db.Clinics.AnyAsync(c => c.Name == Input.Name.Trim()))
            ModelState.AddModelError("Input.Name", "There's already a clinic with this name.");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var clinic = new Clinic { Name = Input.Name.Trim(), Area = Input.Area?.Trim() };
        _db.Clinics.Add(clinic);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CreateClinic", "Clinic", clinic.Id, clinic.Name);

        StatusMessage = $"{clinic.Name} added.";
        return RedirectToPage();
    }

    // "Save" button on one row (rename or change the area)
    public async Task<IActionResult> OnPostSaveAsync(int id, string? name, string? area)
    {
        var clinic = await _db.Clinics.FindAsync(id);
        if (clinic == null) return NotFound();

        name = name?.Trim();
        area = string.IsNullOrWhiteSpace(area) ? null : area.Trim();

        if (string.IsNullOrEmpty(name) || name.Length > 100 || area?.Length > 100)
        {
            StatusMessage = "A clinic needs a name, and the name and area can be up to 100 characters.";
            return RedirectToPage();
        }
        if (await _db.Clinics.AnyAsync(c => c.Id != id && c.Name == name))
        {
            StatusMessage = $"There's already a clinic called {name}.";
            return RedirectToPage();
        }

        var oldName = clinic.Name;
        clinic.Name = name;
        clinic.Area = area;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("UpdateClinic", "Clinic", id, oldName == name ? name : $"{oldName} renamed to {name}");

        StatusMessage = $"{name} saved.";
        return RedirectToPage();
    }

    // "Delete" button (only shown for clinics with no patients)
    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var clinic = await _db.Clinics.FindAsync(id);
        if (clinic == null) return NotFound();

        if (await _db.Patients.AnyAsync(p => p.ClinicId == id))
        {
            StatusMessage = $"{clinic.Name} still has patients, so it can't be deleted. Move or delete its patients first.";
            return RedirectToPage();
        }

        _db.Clinics.Remove(clinic);
        await _db.SaveChangesAsync();   // its staff are kept, with no clinic until reassigned
        await _audit.LogAsync("DeleteClinic", "Clinic", id, clinic.Name);

        StatusMessage = $"{clinic.Name} deleted. Its staff have no clinic until you assign them a new one.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        Clinics = await _db.Clinics
            .OrderBy(c => c.Name)
            .Select(c => new ClinicRow(
                c.Id,
                c.Name,
                c.Area,
                c.Patients.Count,
                _db.StaffProfiles.Count(s => s.ClinicId == c.Id)))
            .ToListAsync();
    }
}