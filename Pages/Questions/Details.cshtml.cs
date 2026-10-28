using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using MenoScreen.Data;
using MenoScreen.Models;

namespace MenoScreen.Pages_Questions
{
    public class DetailsModel : PageModel
    {
        private readonly MenoScreen.Data.ApplicationDbContext _context;

        public DetailsModel(MenoScreen.Data.ApplicationDbContext context)
        {
            _context = context;
        }

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
    }
}
