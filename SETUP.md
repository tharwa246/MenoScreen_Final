# MenoScreen by Mandi: adding the October 2026 update in VS Code

Do steps 1–6 once, on one team member's machine. Everyone else just pulls (step 7).

## 1. Save a safe point first

Open the project folder in VS Code, open the terminal (**Ctrl+`**), and run:

```bash
git add -A
git commit -m "Before Mandi UI and features update"
git checkout -b mandi-update
```

If anything goes wrong you can return with `git checkout main` (or `master`).

## 2. Copy the files in

1. Stop the app if it's running (**Ctrl+C** in the terminal, or the red square if you used F5).
2. In VS Code's Explorer, right-click the project folder (the one with `MenoScreen.csproj`)
   and choose **Reveal in File Explorer** (Windows) / **Reveal in Finder** (Mac).
3. Extract `MenoScreen-update.zip` into that folder. When asked, choose **Replace the files**.
4. Check in VS Code that these new items now exist:
   `Guidelines/`, `Pages/Analytics/`, `Pages/SelfScreen.cshtml`, `Pages/Patients/Details.cshtml`,
   `Services/InviteService.cs`, `Models/AuditLog.cs`.

The **Source Control** panel (Ctrl+Shift+G) now shows every changed file. Click one to see
a before/after comparison.

## 3. Restore and build

```bash
dotnet restore
dotnet build
```

`restore` downloads the one new package (the Postgres driver). If `build` shows errors,
copy the full error text and send it over.

## 4. Update the database (required, once)

The update adds consent fields, QR invites and the audit log, so the database needs a migration:

```bash
dotnet ef migrations add PopiaSelfScreenAudit
```

If you get "dotnet-ef not found", install the tool first: `dotnet tool install --global dotnet-ef`.

You don't need `dotnet ef database update`: the app now applies migrations by itself at start-up.

## 5. Run and test

```bash
dotnet run
```

(or press **F5**). Open the address shown in the terminal and work through this checklist:

| Sign in as | Test |
|---|---|
| clinician@menoscreen.test / Clinician@1234 | Patients → **Add patient** with the consent box ticked → **Save and start screening** → answer all 23 → result page shows the score, band and blanket |
| same | On the result, download both PDFs |
| same | Patient page → **QR self-screen** → **Create QR code**; open the link in a private window, answer, submit; the new result appears in her history marked "Self-screen (QR)" |
| same | **Analytics** shows totals and charts |
| admin@menoscreen.test / Admin@1234 | **Admin** shows the audit log of everything above; **Add a staff account** works; **Questions** → Edit saves |

Existing patients show "Consent not recorded". Open each one and press **She agrees: record consent**,
or delete `app.db` to start with a clean database.

## 6. Commit and share

```bash
git add -A
git commit -m "Mandi UI, QR self-screening, analytics, assistant, POPIA"
git push -u origin mandi-update
```

Merge into `main` once the team has checked it (pull request on GitHub, or `git checkout main` then `git merge mandi-update`).

## 7. Teammates

```bash
git pull
dotnet restore
dotnet run
```

Teammates do **not** run `migrations add`; the migration from step 4 arrives with the pull.
Only one person should ever create a migration at a time, then commit it.

---

## Optional: hosted Postgres database

Firestore (the Firebase database) can't be used by Entity Framework or ASP.NET Identity,
so the app supports hosted **Postgres** instead (for example Supabase or Neon, both free).

1. Keep the password out of git by storing it in user-secrets:
   ```bash
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=...;Database=...;Username=...;Password=...;SSL Mode=Require"
   dotnet user-secrets set "Database:Provider" "Postgres"
   ```
2. The existing migrations are SQLite-specific. Make a fresh set:
   ```bash
   mv Migrations Migrations-sqlite-old      # Windows: ren Migrations Migrations-sqlite-old
   dotnet ef migrations add InitialPostgres
   dotnet run
   ```
   Delete `Migrations-sqlite-old` once it works, and commit.
3. Each teammate runs the two `user-secrets` commands on their own machine (secrets are never pushed).

To go back to the local file: `dotnet user-secrets remove "Database:Provider"`.

## Optional: switching on the clinician assistant

All provider settings live in user-secrets, so nothing about the AI provider is stored in the code or in git.
Set these five values from your AI provider's account and API documentation:

```bash
dotnet user-secrets set "Assistant:BaseUrl" "<provider API base address>"
dotnet user-secrets set "Assistant:Model" "<model name>"
dotnet user-secrets set "Assistant:VersionHeader" "<API version header name>"
dotnet user-secrets set "Assistant:ApiVersion" "<API version value>"
dotnet user-secrets set "Assistant:ApiKey" "<your API key>"
```

Until all five are set, the result page says the assistant isn't switched on, and everything else works normally.
It answers only from `Guidelines/assistant-guidelines.md` plus the result on screen, and never receives the
patient's name. Add sections to that file once your clinical advisor approves them.

## Optional: testing QR self-screening on a phone

A phone can't open `localhost`. Run `dotnet run --urls "http://0.0.0.0:5018"`, open the app on your laptop at
`http://<your-laptop-IP>:5018` (same Wi-Fi as the phone), and create the QR code from there. Or use VS Code's
**Ports** panel to forward port 5018 with public visibility. The QR code uses whichever address you opened the app with.

## Brand colours

All colours are at the top of `wwwroot/css/site.css` (`:root { ... }`); the PDF colours are the two constants
at the top of `Services/DocumentService.cs`. Change them there to match JoinMandi's exact hex codes.
