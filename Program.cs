using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MenoScreen.Data;
using MenoScreen.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// DATABASE
// Which database do we use? We read one setting, "Database:Provider":
//   - "Sqlite"   (the default) = a local file called app.db in this project folder.
//                 Good for development: nothing to install, nothing online.
//   - "Postgres" = a database hosted online (e.g. Supabase or Neon), so the whole
//                 team and a deployed site can share the same data.
// The connection string says WHERE the database is. For Postgres it contains a
// password, so it lives in "user-secrets" on each laptop, never in git (SETUP.md).
// The rest of the app doesn't care which one is used: Entity Framework talks to both.
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
var dbProvider = builder.Configuration["Database:Provider"] ?? "Sqlite";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (dbProvider.Equals("Postgres", StringComparison.OrdinalIgnoreCase))
        options.UseNpgsql(connectionString);
    else
        options.UseSqlite(connectionString);
});
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 8;
    })
    .AddRoles<IdentityRole>()   // Here is what we added to support roles
    .AddEntityFrameworkStores<ApplicationDbContext>();

// Security: if nobody clicks anything for 30 minutes, the clinician is signed out.
// Consulting-room computers are shared, so we don't want a result left open.
// Each click resets the 30 minutes ("sliding expiration").
builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// ROLES AND ACCESS RULES ("policies")
// A policy is a named rule. "AdminOnly" lets in only users with the Admin role, etc.
// Below, we attach these rules to whole folders of pages, so every page in
// /Patients automatically requires a Clinician. No page has to remember to check.
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ClinicianOnly", policy => policy.RequireRole("Clinician"));
    // Analytics is useful to both the clinic admin and clinicians
    options.AddPolicy("StaffOnly", policy => policy.RequireRole("Admin", "Clinician"));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Questions", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Patients", "ClinicianOnly");
    options.Conventions.AuthorizeFolder("/Screenings", "ClinicianOnly");
    options.Conventions.AuthorizeFolder("/Analytics", "StaffOnly");
    // Clinician accounts are created by an admin, not by anyone off the internet
    options.Conventions.AuthorizeAreaPage("Identity", "/Account/Register", "AdminOnly");
    // /SelfScreen stays public on purpose: patients open it from a QR code,
    // and access is controlled by the one-time token instead of a login.
});

builder.Services.AddHttpContextAccessor();

// SERVICES: the classes that hold our business logic.
// Registering them here means any page can just ask for one in its constructor
// ("dependency injection"). "Scoped" = one fresh copy per web request.
builder.Services.AddScoped<ScoringService>();
builder.Services.AddScoped<ScreeningService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<InviteService>();
builder.Services.AddScoped<AnalyticsService>();
// AI clinician assistant: the provider's address, model and key all come from
// user-secrets (see SETUP.md), so none of them are stored in the code or in git.
builder.Services.AddHttpClient<ClinicianAssistantService>(client =>
{
    var baseUrl = builder.Configuration["Assistant:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(baseUrl))
        client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(60);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

//SEEDING BLOCK 0: build or update the database tables before anything else runs.
// "Migrations" are step-by-step instructions for creating tables (see the Migrations folder).
// MigrateAsync runs any steps this database hasn't had yet. On a brand-new empty
// database it builds every table; on an existing one it only adds what's new.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

//SEEDING BLOCK 1: roles and test users
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

    string[] roles = { "Admin", "Clinician", "Doctor", "Patient" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }

    // FIRST ADMIN ON A DEPLOYED SITE. A live site has no test accounts (see below), so
    // nobody could sign in. If the hosting settings contain Seed:AdminEmail and
    // Seed:AdminPassword, we create that one admin account on start-up (only if it doesn't
    // exist yet). After the first sign-in, remove Seed:AdminPassword from the settings.
    var seedEmail = app.Configuration["Seed:AdminEmail"];
    var seedPassword = app.Configuration["Seed:AdminPassword"];
    if (!string.IsNullOrWhiteSpace(seedEmail) && !string.IsNullOrWhiteSpace(seedPassword)
        && await userManager.FindByEmailAsync(seedEmail) == null)
    {
        var firstAdmin = new IdentityUser { UserName = seedEmail, Email = seedEmail, EmailConfirmed = true };
        var created = await userManager.CreateAsync(firstAdmin, seedPassword);
        if (created.Succeeded)
            await userManager.AddToRoleAsync(firstAdmin, "Admin");
        else
            app.Logger.LogError("Could not create the first admin: {Errors}",
                string.Join("; ", created.Errors.Select(e => e.Description)));
    }

    // Test accounts with known passwords are only created when running on a laptop
    // ("Development"). A real deployed site never gets them, because anyone could
    // log in with a password that's written in this file.
    if (app.Environment.IsDevelopment())
    {
        // Admin account (development only)
        var adminEmail = "admin@menoscreen.test";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            await userManager.CreateAsync(admin, "Admin@1234");
            await userManager.AddToRoleAsync(admin, "Admin");
        }

        // Clinician account (development only)
        var clinicianEmail = "clinician@menoscreen.test";
        if (await userManager.FindByEmailAsync(clinicianEmail) == null)
        {
            var clinician = new IdentityUser { UserName = clinicianEmail, Email = clinicianEmail, EmailConfirmed = true };
            await userManager.CreateAsync(clinician, "Clinician@1234");
            await userManager.AddToRoleAsync(clinician, "Clinician");
        }
    }
}

//SEEDING BLOCK 2: the 23 questions
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await SeedData.SeedQuestionsAsync(db);
}
// PDF FONTS: load the website's fonts from the Fonts folder so the PDFs match the site
DocumentService.RegisterFonts(Path.Combine(app.Environment.ContentRootPath, "Fonts"));
app.Run();
