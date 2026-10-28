# Deploying MenoScreen as a live web app (Azure App Service)

**Recommended host:** Microsoft Azure App Service, in the **South Africa North** (Johannesburg) region.
It runs .NET natively, gives you an `https://<name>.azurewebsites.net` address with HTTPS
included, and keeps patient data in South Africa (a good POPIA point for the presentation).

**Cost:** Sign up for **Azure for Students** with your UCT email. You get $100 of credit for
12 months, with no credit card needed. App Service has an always-free tier (F1), but it sleeps
when idle and only gets about an hour of compute a day. For presentation day, use the **Basic B1**
tier paid from the student credit, so the app is awake and fast.

## Before you start

- The update and `SETUP.md` steps are done, and the app runs locally with `dotnet run`.
- The code is committed and pushed to GitHub.
- Use **test data only** on the live site: no real patients.

## Step 1: create the web app (Azure portal, about 5 minutes)

1. Go to portal.azure.com, sign in with your student account, and choose **Create a resource → Web App**.
2. Fill in:
   - **Name:** e.g. `menoscreen-team5`. This becomes `https://menoscreen-team5.azurewebsites.net`.
   - **Publish:** Code
   - **Runtime stack:** .NET 10
   - **Operating system:** Linux
   - **Region:** South Africa North
   - **Pricing plan:** Basic B1 (or Free F1 to try it out)
3. Click **Review + create → Create**.

## Step 2: choose where the live database lives

Pick one.

**Option A: SQLite file on the server (simplest, fine for the demo).**
App Service keeps everything under `/home` when the app restarts or redeploys,
so the database file can live there. Nothing else to create.

**Option B: Azure Database for PostgreSQL (the "proper" production setup).**
Free for 12 months on the student offer (Flexible Server, Burstable B1MS). Create it in
**South Africa North**, and allow access from Azure services. Then follow "Optional: hosted
Postgres database" in `SETUP.md` once on your laptop, to create the Postgres migrations
and commit them.

## Step 3: add the settings (instead of user-secrets)

On the live site, settings go in the web app, not in files. In the portal, open your web app
→ **Settings → Environment variables → App settings**, and add each one. The double underscore
`__` stands for a `:` in the setting name.

| Name | Value |
|---|---|
| `ConnectionStrings__DefaultConnection` | Option A: `Data Source=/home/menoscreen.db` · Option B: your Postgres connection string |
| `Database__Provider` | Option A: `Sqlite` · Option B: `Postgres` |
| `Seed__AdminEmail` | the email for the first admin account |
| `Seed__AdminPassword` | a strong password (8+ characters, upper-case, number, symbol) |
| `Assistant__...` | only if you're switching on the assistant: the same five values as in `SETUP.md` |

Click **Apply**. These values are encrypted by Azure and never appear in your code or on GitHub.

## Step 4: deploy

Pick one.

**From VS Code (quickest).**
1. Install the **Azure App Service** extension, then sign in to Azure from the Azure icon in the sidebar.
2. In the terminal: `dotnet publish -c Release -o ./publish`
3. In the Azure sidebar, right-click your web app → **Deploy to Web App…** → choose the `publish` folder.

**Automatic from GitHub (best for a team).**
In the portal: web app → **Deployment Center** → Source: **GitHub** → pick your repo and
branch → **Save**. Azure adds a GitHub Actions file to your repo, and from then on every push
to that branch deploys automatically. Pull first (`git pull`) to get that new file.

## Step 5: first sign-in and checks

1. Open `https://<your-app-name>.azurewebsites.net`. The first load can take 20–30 seconds while
   it starts and builds the database tables.
2. Sign in with the `Seed__AdminEmail` account.
3. **Admin → Add a staff account** to create a Clinician account for each team member.
4. Delete the `Seed__AdminPassword` app setting (the account stays; this just removes the password from the settings).
5. Run the same checklist as in `SETUP.md`. The **QR self-screen** now works on any phone
   with no extra setup, because the QR code contains the public `https://` address.

## If something goes wrong

- **"Application Error" page:** web app → **Log stream** in the portal shows the real error.
- **Login works but every form says "bad request" after a redeploy:** sign out and in again.
- **PDFs fail on Linux:** check the Log stream and send me the error text.
- **Database changes:** after adding a migration locally (`dotnet ef migrations add ...`),
  just commit and deploy. The app applies new migrations itself on start-up.

## What to say in the presentation

"MenoScreen is a server-rendered ASP.NET Core web app, hosted on Azure App Service in
South Africa North over HTTPS. Secrets live in encrypted app settings, not in code.
The database schema is versioned with Entity Framework migrations that apply automatically
on deployment, and every push to GitHub deploys through a CI/CD pipeline."
