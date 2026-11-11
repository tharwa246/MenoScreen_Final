# MenoScreen by Mandi: the live site on Heroku

**Live site:** https://menoscreen-mandi-06720960df68.herokuapp.com

| Setting | Value |
|---|---|
| Heroku app | `menoscreen-mandi` (Europe region, Basic dyno) |
| Buildpack | `heroku/dotnet` |
| Database | Heroku Postgres, Essential-0 plan |
| Branch deployed | `Sam-updates` |

You need the Heroku CLI and access to the app. Sign in once with `heroku login`.

---

## Deploy a change

```bash
git push origin Sam-updates
git push heroku Sam-updates:main
```

Heroku builds the app and restarts it, which takes about two minutes. Database migrations apply themselves on start-up.
To watch it start:

```bash
heroku logs --tail -a menoscreen-mandi
```

## Undo a bad deploy

```bash
heroku releases -a menoscreen-mandi
heroku rollback v<number> -a menoscreen-mandi
```

A rollback returns the code only. It does not undo database changes.

## Settings (config vars)

Heroku settings replace the user-secrets used on a laptop. A double underscore `__` stands for `:`.
Set one with `heroku config:set NAME=value -a menoscreen-mandi`.

| Name | What it does |
|---|---|
| `DATABASE_URL` | The database address. Added automatically by Heroku Postgres. |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `true`. Lets the app see it is on https, so QR links are correct. |
| `Seed__AdminEmail` | The first admin account, created on start-up if it doesn't exist. |
| `Seed__AdminPassword` | Its password. Remove this setting once you have signed in. |
| `Seed__DemoData` | `true` adds the demo clinics and patients for the showcase. Set to `false` for real use. |
| `Translator__Key` | Azure Translator key, for creating translation drafts. |
| `Translator__Region` | `global` |

Never paste the output of `heroku config` into a chat or document: it shows the passwords.

## Looking at the database

- **In the browser:** Heroku dashboard → the app → Heroku Postgres → **Dataclips**. Good for read-only queries to show someone.
- **From the terminal:** `heroku pg:psql -a menoscreen-mandi`

## Setting it up on a new Heroku account (handover)

```bash
heroku create <app-name> --region eu
heroku buildpacks:set heroku/dotnet -a <app-name>
heroku addons:create heroku-postgresql:essential-0 -a <app-name>
heroku config:set ASPNETCORE_FORWARDEDHEADERS_ENABLED=true Seed__AdminEmail=<email> Seed__AdminPassword=<strong password> -a <app-name>
git remote add heroku https://git.heroku.com/<app-name>.git
git push heroku Sam-updates:main
```

Then sign in with that admin email and password, add clinics and staff from Home, and remove `Seed__AdminPassword`.

## Before real patients use it

1. Set `Seed__DemoData` to `false`, and delete the demo patients and clinics from Home (Patients and screenings, then Clinics).
2. Make sure `Seed__AdminPassword` is removed.
3. Rotate the database password: `heroku pg:credentials:rotate DATABASE -a <app-name>`.
4. Have a fluent speaker review every translation (Translations page).
5. Have the clinical advisor sign off the band wording (Clinical content page).
6. Confirm POPIA requirements with Mandi: where the data is hosted, the operator agreement, and the privacy notice.
