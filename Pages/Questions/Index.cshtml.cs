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
    public class IndexModel : PageModel
    {
        private readonly MenoScreen.Data.ApplicationDbContext _context;

        public IndexModel(MenoScreen.Data.ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<Question> Question { get;set; } = default!;

        public async Task OnGetAsync()
        {
            Question = await _context.Questions
                .Include(q => q.QuestionSetVersion).ToListAsync();
        }
    }
}
