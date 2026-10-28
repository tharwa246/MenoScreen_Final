using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using MenoScreen.Data;
using MenoScreen.Models;

namespace MenoScreen.Pages_Questions
{
    public class CreateModel : PageModel
    {
        private readonly MenoScreen.Data.ApplicationDbContext _context;

        public CreateModel(MenoScreen.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
        ViewData["QuestionSetVersionId"] = new SelectList(_context.QuestionSetVersions, "Id", "VersionNumber");
            return Page();
        }

        [BindProperty]
        public Question Question { get; set; } = default!;

        // For more information, see https://aka.ms/RazorPagesCRUD.
        public async Task<IActionResult> OnPostAsync()
        {
            // The form posts QuestionSetVersionId, not the whole QuestionSetVersion object.
            // Without this line the (non-nullable) navigation property always fails validation.
            ModelState.Remove("Question.QuestionSetVersion");

            if (!ModelState.IsValid)
            {
                // Refill the version dropdown, or the page crashes when it redisplays
                ViewData["QuestionSetVersionId"] = new SelectList(_context.QuestionSetVersions, "Id", "VersionNumber");
                return Page();
            }

            _context.Questions.Add(Question);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
