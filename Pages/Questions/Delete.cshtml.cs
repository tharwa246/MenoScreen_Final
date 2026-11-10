using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MenoScreen.Data;
using MenoScreen.Models;
using MenoScreen.Services;

namespace MenoScreen.Pages_Questions
{
    public class DeleteModel : PageModel
    {
        private readonly MenoScreen.Data.ApplicationDbContext _context;
        private readonly VersionService _versions;

        public DeleteModel(MenoScreen.Data.ApplicationDbContext context, VersionService versions)
        {
            _context = context;
            _versions = versions;
        }

        [BindProperty]
        public Question Question { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var question = await _context.Questions.FirstOrDefaultAsync(m => m.Id == id);

            if (question is not null)
            {
                Question = question;

                return Page();
            }

            return NotFound();
        }

        public async Task<IActionResult> OnPostAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var question = await _context.Questions.FindAsync(id);

            // A question in a version that has been used can never be deleted:
            // that would also delete the answers in old screenings
            if (question != null && await _versions.IsLockedAsync(question.QuestionSetVersionId))
                return RedirectToPage("/Versions/Details", new { id = question.QuestionSetVersionId });

            if (question != null)
            {
                Question = question;
                _context.Questions.Remove(Question);
                await _context.SaveChangesAsync();
            }

            return RedirectToPage("./Index");
        }
    }
}
