# MenoScreen by Mandi: running it on a laptop

MenoScreen is an ASP.NET Core 10 Razor Pages app with a PostgreSQL database (Entity Framework Core)
and ASP.NET Identity for sign-in. This guide gets it running locally. For the live site, see `DEPLOY.md`.

---

## 1. What you need

- The .NET 10 SDK (`dotnet --version` should start with 10)
- Git
- Visual Studio or VS Code
- A PostgreSQL database. The team uses a free Neon database; any PostgreSQL server works.

## 2. Get the code

```bash
git clone https://github.com/tharwa246/MenoScreen_Final.git
cd MenoScreen_Final
git checkout Sam-updates
dotnet restore
```

## 3. Add your secrets (never in git)

Secrets live in .NET user-secrets on each laptop, outside the project folder, so they can never be committed.

**Database (required).** Paste your database address. Either the `postgres://user:password@host/dbname` form
(as Neon and Heroku give it) or the `Host=...;Username=...;Password=...` form works. `Data/DatabaseUrl.cs`
converts it and always turns on SSL.

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your database address>"
```

**Azure Translator (optional).** Only needed to create new translation drafts on the Translations page.
Without it, everything else works and existing translations still show.

```bash
dotnet user-secrets set "Translator:Key" "<Azure Translator key>"
dotnet user-secrets set "Translator:Region" "global"
```

## 4. Run

```bash
dotnet run
```

Open http://localhost:5018. On every start the app:

1. applies any database migrations it hasn't had yet (the tables come from the `Migrations` folder);
2. creates the roles (Admin, Clinician, Patient);
3. fills empty tables: the 23 screening questions, the band wording, the portal tips and the interface wording;
4. on a laptop only, adds the demo clinics, patients and test accounts below.

### Test accounts (laptop only)

| Sign in as | Password | Role and clinic |
|---|---|---|
| admin@menoscreen.test | Admin@1234 | Admin, all clinics |
| clinician@menoscreen.test | Clinician@1234 | Clinician, Gugulethu Community Clinic |
| clinician2@menoscreen.test | Clinician@1234 | Clinician, Sea Point Women's Health |
| clinician3@menoscreen.test | Clinician@1234 | Clinician, Soweto Family Practice |

These accounts are never created on the live site, because their passwords are written in the code.

## 5. Changing the database structure

After changing a class in `Models` or `Data/ApplicationDbContext.cs`:

```bash
dotnet ef migrations add <ShortName>
```

Commit the new files in `Migrations`. Teammates only `git pull` and `dotnet run`: the migration applies itself on start-up.
Only one person should create a migration at a time.

## 6. Testing QR self-screening on a phone

A phone can't open `localhost`. Either use the live site, or run

```bash
dotnet run --urls "http://0.0.0.0:5018"
```

and open the app on your laptop at `http://<your-laptop-IP>:5018`, with the phone on the same Wi-Fi.
The QR code uses whichever address you opened the app with.

## 7. Where things are

| Folder | What it holds |
|---|---|
| `Program.cs` | Start-up: database connection, sign-in, access rules per folder, services, seeding |
| `Models` | The tables: patients, screenings, answers, questions and versions, translations, check-ins |
| `Data` | The database context, the address converter and the starting data |
| `Services` | The rules: scoring, screening, clinic scoping, portal, analytics, PDFs, translations, audit log |
| `Pages` | The screens. Each folder has its own access rule in `Program.cs` |
| `Migrations` | The history of the database structure |
| `wwwroot/css/site.css` | All styling. Mandi's colours are at the top in `:root` |
| `Fonts` | Montserrat and Sour Gummy, used in the PDFs |
