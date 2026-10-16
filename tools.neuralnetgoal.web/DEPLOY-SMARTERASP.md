# Deploy to SmarterASP.NET - checklist

Everything is uploaded from your PC with FileZilla (FTP details are in the SmarterASP.NET control panel).
Nothing is run on the VPS for this.

## 0. Build the folder (PC)

```bash
dotnet publish tools.neuralnetgoal.web/tools.neuralnetgoal.service -c Release -o publish/web
```

Then put the secrets next to the published files (they are never in git):
`publish/web/appsettings.Production.json` must contain `ConnectionStrings:Default`, `Parser:ApiKey` and `Export:GemBoxLicense`
(same values as the service's user-secrets). The `publish/web/appsettings.json` must stay without secrets.
A ready folder was created by Claude at `C:\github\neuralnetgoal.net\publish\web`.

## 1. Control panel (browser)

- [ ] Website settings: ASP.NET / .NET Core version = **latest (.NET 10)**.
- [ ] HTTPS is enabled for the domain (free certificate in the panel if not).
- [ ] Note the FTP host, user and the site root folder.

## 2. Upload (FileZilla, FTP of the hosting)

- [ ] Upload `app_offline.htm` first (shows "back soon" while files are copied).
      Content: `<html><body><h3>Updating, back in a minute.</h3></body></html>`
- [ ] Upload the **contents** of `publish\web` (not the folder itself) into the site root, overwrite all.
- [ ] Check that `appsettings.Production.json`, `web.config`, `templates\Training_data.xlsx` and `wwwroot\_framework` are there.
- [ ] Delete `app_offline.htm` on the server.

## 3. First visit

- [ ] Open `https://<your site>/`. The login page offers **"First start: create the Admin password"**.
- [ ] Create it **immediately**, before sharing the address (until then anyone who opens the site could).

## 4. Check

- [ ] Dashboard: Database = OK.
- [ ] Dashboard: Parser = OK. If not, the host cannot reach `https://parser.neuralnetgoal.cloud:5234`
      (shared hosts often block odd outbound ports). Fix: also serve the parser on port 443 through nginx on the VPS,
      then change `Parser:ApiUrl` to `https://parser.neuralnetgoal.cloud` in `appsettings.Production.json`.
- [ ] Run: Results for one country, "Newest N matches" = 10. Watch the live log.
- [ ] Run: League tables, then Fixtures & Excel. Download the Excel and compare with the console's file.

## 5. Later updates

1. Upload `app_offline.htm`.
2. Rebuild (step 0) and upload only what changed (usually `*.dll` and `wwwroot\_framework`). **Do not overwrite `appsettings.Production.json`.**
3. Delete `app_offline.htm`.

## Troubleshooting

| Symptom | Likely cause |
|---|---|
| HTTP 500.30 / 500.19 | wrong .NET version in the panel, or an incomplete upload |
| Login page loads but "server answered 500" | database connection string wrong or not reachable; check `appsettings.Production.json` |
| Excel download fails | cannot write to `App_Data\exports`; set `Export:Directory` to a writable folder |
| A long job stops by itself | the host recycled the application (no visitors); keep the Run page open |
