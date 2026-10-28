using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MenoScreen.Data;
using MenoScreen.Models;

namespace MenoScreen.Pages_Questions
{
    public class EditModel : PageModel
    {
        private readonly MenoScreen.Data.ApplicationDbContext _context;

        public EditModel(MenoScreen.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Question Question { get; set; } = default!;

        public async Task<IActionResult> OnGetAsync(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var question =  await _context.Questions.FirstOrDefaultAsync(m => m.Id == id);
            if (question == null)
            {
                return NotFound();
            }
            Question = question;
           ViewData["QuestionSetVersionId"] = new SelectList(_context.QuestionSetVersions, "Id", "VersionNumber");
            return Page();
        }

        // To protect from overposting attacks, enable the specific properties you want to bind to.
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

            _context.Attach(Question).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!QuestionExists(Question.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return RedirectToPage("./Index");
        }

        private bool QuestionExists(int id)
        {
            return _context.Questions.Any(e => e.Id == id);
        }
    }
}
