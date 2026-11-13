# Publishing The Employees Know 0.2.0

## Prepared metadata

- Display name: `The Employees Know`
- Thunderstore team: `EmployeeOfTheMonth`
- Thunderstore package: `The_Employees_Know`
- Version: `0.2.0`
- GitHub: https://github.com/wolfred24/The-Employees-Know
- License: PolyForm Noncommercial 1.0.0
- Copyright holder: Cesar Alfredo Martinez Gallegos
- Game branch: IL2CPP only
- MelonLoader dependency: `LavaGang-MelonLoader-0.7.3`
- Ko-fi: https://ko-fi.com/employeeofthemonth

## 1. Push the repository first

The README uses raw GitHub image URLs, so the repository must have the `main` branch before Thunderstore/Nexus show the images correctly.

From the project folder:

```bash
git init -b main
git add .
git commit -m "Initial release 0.2.0"
git remote add origin https://github.com/wolfred24/The-Employees-Know.git
git push -u origin main
```

`dist/` is intentionally ignored because the ZIP is a release artifact. Upload it to a GitHub Release named `v0.2.0` if desired.

## 2. Verify the release ZIP

The prepared file is:

```text
dist/The_Employees_Know-0.2.0.zip
```

It contains:

```text
manifest.json
icon.png
README.md
CHANGELOG.md
LICENSE
SmartStorage.dll
```

Test it in a clean profile:

1. Install MelonLoader 0.7.3 for the IL2CPP branch.
2. Import the ZIP as a local mod.
3. Launch Schedule I.
4. Confirm the mod appears as **The Employees Know**.
5. Confirm the storage permission toggle works.
6. Confirm a botanist/chemist/handler can use an enabled storage.
7. Check `Latest.log` for errors.

## 3. Thunderstore upload

1. Log in and select the `EmployeeOfTheMonth` team.
2. Open https://thunderstore.io/package/create/.
3. Upload `dist/The_Employees_Know-0.2.0.zip`.
4. Community: **Schedule I**.
5. Categories: **IL2CPP** and **Mods**.
6. NSFW: **No**.
7. Declare AI-generated content if the upload form offers an AI category.
8. Confirm the package page, README images, icon and dependency.

Important: the team name and package name cannot be changed later without creating a new package.

## 4. Nexus upload

1. Open https://www.nexusmods.com/games/schedule1.
2. Create a new mod page titled **The Employees Know**.
3. Upload `dist/The_Employees_Know-0.2.0.zip`.
4. Summary: use the manifest description.
5. Description: paste `README.md`.
6. Category: choose the closest gameplay/utility category offered by Schedule I.
7. Requirements:
   - Schedule I 0.4.7f12 tested.
   - MelonLoader 0.7.3 or newer.
   - IL2CPP branch only.
   - No S1API dependency.
8. Add the icon, cover and gameplay screenshots/GIFs.
9. Apply the PolyForm Noncommercial 1.0.0 license and the required copyright notice.
10. AI disclosure:
    - `AI-Generated Content` for the code.
    - `AI Media` for the icon/cover artwork, which were generated as SVG artwork with AI assistance.
    - Gameplay screenshots are not AI-generated.
11. Set NSFW: No.
12. Set permissions and AI-training/forks preferences as desired.

## 5. Support link

Ko-fi: https://ko-fi.com/employeeofthemonth

It is already linked in the README. Keep the link visible but do not gate the mod behind donations.

## 6. Screenshots and GIFs

Place final captures in:

```text
assets/screenshots/
assets/gifs/
```

Suggested files:

```text
01-storage-toggle.png
02-botanist-storage.png
03-chemist-workflow.png
04-handler-workflow.png
chemist-workflow.gif
```

After adding them, update the README media section and re-create the release ZIP if the images are referenced by relative path. The current README uses the cover from GitHub and can be expanded with raw GitHub URLs for screenshots.

## 7. AI disclosure text

This mod's code was developed with substantial AI assistance using **opencode** and **DeepSeek**. The author designed, integrated, verified and tested the features in-game. Promotional artwork and the icon were generated as SVG artwork with AI assistance; gameplay screenshots are captured from the game.

## 8. Final checklist

- [ ] GitHub main branch pushed
- [ ] Cover and icon render on GitHub
- [ ] Clean-profile test passed
- [ ] Thunderstore package uploaded
- [ ] Thunderstore README and dependency verified
- [ ] Nexus page created
- [ ] Nexus AI tags and license set
- [ ] Ko-fi link verified
- [ ] Both pages link to each other if desired
