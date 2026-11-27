# Dugout Deck — name packs

All clubs, leagues and cups in Dugout Deck are fictional ("Red Sofia", "Sky Blue Manchester"…). A **name pack** is a small
JSON file that renames them — in every save, careers in progress included. Packs are made and shared by players; the game
itself only comes with its fictional names and an empty template.

## Using a pack

1. Main menu → **Settings** → **Name packs** → **Import a name pack…** and choose the `.json` file.
2. The names change straight away. Switch packs on and off with the checkbox, or delete them.
3. **Reset to original names** switches every pack off.

Packs are stored in `%LOCALAPPDATA%\DugoutDeck\name-packs` (paste that into the Explorer address bar). Game updates do not
touch this folder. You can also drop files there yourself: files ending in `.json` are active, `.json.off` are switched off.
When several packs are active, the one whose file name comes later in the alphabet wins for a club both of them rename.

Clubs you changed in the in-game **Team & league editor** keep their editor names.

## Making a pack

Start from **Export current names (template)** in Settings (it also downloads the file and keeps a switched-off copy in the
name-packs folder), or from `name-pack-template.json` that comes with the game. Change only what you want and delete the rest:

```json
{
  "name": "My name pack",
  "author": "your name",
  "version": "1",
  "teams": [
    { "code": "BG1-02", "name": "New Name", "short": "NEW", "city": "Sofia", "stadium": "New Stadium", "colors": ["#c8102e", "#ffffff"] }
  ],
  "leagues": [ { "code": "BG1", "name": "New League Name" } ],
  "competitions": [ { "code": "UCL", "name": "New Cup Name" } ]
}
```

- **`code` is how the game finds the club** — never change it. Every other field is optional.
- `short`: 2–4 letters. `colors`: two colours as `#rrggbb` (they also draw the badge).
- Names are cut at 40 characters (leagues and cups 60). Two clubs cannot share a name — a name that is already taken is skipped.
- Ratings cannot be changed by a pack.

## Please note

Real club names, league names and badges usually belong to their owners. Dugout Deck does not ship real names; if you share a
pack, you do so yourself. Badges are always drawn from the club colours — the game never loads pictures from a pack.
