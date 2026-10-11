# QuasiRobotics localisation

One file per language the game supports. File names match the game's language list:

| File | Language |
| --- | --- |
| EnglishUS.json | English |
| Russian.json | Russian |
| German.json | German |
| French.json | French |
| Spanish.json | Spanish |
| Polish.json | Polish |
| Turkish.json | Turkish |
| BrazilianPortugal.json | Portuguese (Brazil) |
| Korean.json | Korean |
| Japanese.json | Japanese |
| ChineseSimp.json | Chinese (Simplified) |

Every file holds the same keys. The other languages start as copies of English, so translate the
text on the right and leave the keys on the left alone. If a key is missing or its text is empty,
the mod shows the English text.

- Keep `{0}`, `{1}` and so on: the mod puts names, numbers and times there.
- Keep the file valid JSON (UTF-8, quotes escaped as `\"`).
- `mgperk.*` keys are tech tree names (`.name`) and subtitles (`.subName`).

## Testing a translation without building

The build ships this folder next to the DLL (`QuasiRobotics/Localization/`). A file there overrides
the copy built into the DLL, so you can edit it in your installed mod folder and restart the game.
When you are happy, send the file back for this folder.
