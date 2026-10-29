using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MenoScreen.Data;
using MenoScreen.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole>()   // Here is what we added to support roles
    .AddEntityFrameworkStores<ApplicationDbContext>();

// a rule called "AdminOnly" that only lets users in the Admin role through
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ClinicianOnly", policy => policy.RequireRole("Clinician"));
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Questions", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Patients", "ClinicianOnly");
    options.Conventions.AuthorizeFolder("/Screenings", "ClinicianOnly");
    options.Conventions.AuthorizeFolder("/Versions", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Dashboard", "AdminOnly");
});


// lets pages ask for a ScoringService
builder.Services.AddScoped<ScoringService>();
builder.Services.AddScoped<ScreeningService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<VersionService>();

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

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

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

//SEEDING BLOCK 2: the 23 questions
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await SeedData.SeedQuestionsAsync(db);
}

app.Run();