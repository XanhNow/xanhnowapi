# XanhNow.Auth (Visual Studio-ready)

## Open in Visual Studio

1. Extract the zip.
2. Open `XanhNow.Auth.sln` in Visual Studio 2022.
3. Right-click the solution → **Restore NuGet Packages**.
4. Set `AuthService.Api` as Startup Project.
5. Update `appsettings.json` if needed (JWT secret, Redis/Kafka endpoints).
6. Press **F5** to run. Swagger at: `http://localhost:5000/swagger`.

## EF Core Migrations (first time)

Open **Package Manager Console** (Tools → NuGet Package Manager → PMC) and run:

```powershell
Add-Migration InitAuth -Project AuthService.Infrastructure -StartupProject AuthService.Api
Update-Database -Project AuthService.Infrastructure -StartupProject AuthService.Api
```

## Dev Helpers (optional)

If you have Docker Desktop, you can bring up Redis + Kafka quickly with:

```powershell
docker compose -f docker-compose.dev.yml up -d
```

## Notes

- Login by phone number + password (JWT + refresh).
- Forgot password flow stores a 6-digit code in Redis (DEV: written to console).
- Passkey (WebAuthn) endpoints are present; integrate with your frontend to use.
- Kafka events published: `user.registered`, `user.loggedin`, `user.password.changed`.
