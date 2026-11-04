# Standing Stones of Valheim

Skyrim-style standing stones of the Norse gods, hidden around the world. Find them, choose one, and it guides you: faster skills, blessings, bargains with a cost, or a power once a day.

## What changes?

Sixteen standing stones, one for each of sixteen gods, stand in the wilds of every new world. Each is a tall rune stone with the god's rune glowing on it, a ring of small stones around it and a few things that belong to the god, with a faint light at night. A world has two or three of each god's stone, in the land that fits it, so the stones come in the order you explore: the Meadows' stones on your first days, the Deep North's at the very end.

- **Read a stone** (E): the god's lore, the kind of stone, and what it does. The first touch marks it as found.
- **Choose it** (Shift+E): it guides you from now on. Only one stone guides you at a time; choosing another is free, and you can change as often as you like. The chosen stone shows as a rune icon with your other effects; its tooltip (or the compendium's active effects) says what it does.
- **Hints:** after you read a stone, its runes point towards the nearest stone you haven't found yet, in the same land if there's one left there (a direction and a rough distance, no map pin).
- **Rune altar:** once you've found a stone, you can build a rune altar at home (Hammer, Misc: 20 stone, 10 wood). Bind each stone you've found to it once, with an offering from that stone's land, and then choose any bound stone at the altar instead of travelling back to it.

What you've found, bound and chosen belongs to your character, like your skills, and goes with you to other worlds.

## The stones

The colour of a stone's runes tells its kind: **blue** path stones make a group of skills rise faster, **green** blessing stones simply help, **red** bargain stones help more but take something, **gold** power stones give a power once a day.

| Land | Stone | Kind | What it does |
|---|---|---|---|
| Meadows | Tyr | path | melee weapon skills and Blocking rise 25% faster |
| Meadows | Bragi | blessing | Rested lasts 50% longer |
| Meadows | Frigg | bargain | food lasts 50% longer; you run 10% slower |
| Black Forest | Freyr | path | Wood Cutting, Pickaxes, Fishing and Farming rise 25% faster |
| Black Forest | Ullr | path | Bows, Crossbows and Sneak rise 25% faster |
| Swamp | Hel | bargain | 40% more damage while your health is below 30%; Hel's mark (below) |
| Swamp | Eir | blessing | health regenerates 50% faster |
| Mountain | Skadi | blessing | the cold and freezing hold you 50% less, and you are resistant to frost |
| Mountain | Heimdall | blessing | enemies coming for you within 40 m show as red dots on your minimap |
| Plains | Thor | blessing | +100 max carry weight |
| Plains | Baldr | power | once a day, a blow that would kill you leaves you at 20% health, and nothing can hurt you for 3 seconds |
| Mistlands | Odin | path | Elemental Magic and Blood Magic rise 25% faster |
| Mistlands | Freyja | power | once a day, press **H**: for 60 seconds you deal 15% more damage and each kill gives back 10% of your health |
| Ashlands | Loki | bargain | sneak attacks hit 50% harder, 50% quieter while sneaking; 20% less max health |
| Deep North | Víðarr | bargain | melee attacks deal 20% more damage; your blocks are 50% weaker |
| Any shore | Njord | blessing | a ship you steer sails 10% faster; swimming costs 50% less stamina |

**Hel's mark:** if you die with Hel's stone chosen, her mark takes 25% of your max health and stamina. It lifts when you take your things back from your grave (the moment the game gives you Corpse Run), or after one in-game day. If no grave was made, there's no mark. It stays when you switch stones and over a logout.

**Daily powers** come back on the next in-game day. The tooltip of a power stone says whether it's ready today.

**The daily power key** is **H** by default. It does nothing while Alt is held (so Alt+H stays free for other mods) or while you type in chat or the console, or have a menu or your inventory open. Change it in the config or with Configuration Manager (`DailyPowerKey`).

Every number above can be changed in the config (see below).

## The rune altar's offerings

| Stone's land | Offering to bind it |
|---|---|
| Meadows | 10 flint, 5 deer hide |
| Black Forest | 10 copper, 2 troll hide |
| Swamp | 10 iron, 5 guck |
| Mountain | 10 silver, 3 wolf pelt |
| Plains | 10 black metal, 2 lox pelt |
| Mistlands | 5 refined eitr, 1 black core |
| Ashlands | 10 flametal |
| Deep North | 3 frostcore, 10 timberwood |
| Any shore (Njord) | 5 serpent scale |

Stones you haven't found never show at the altar.

## New worlds and existing worlds

Valheim decides where locations go when a world is created, and only builds them when someone first comes near.

- **New world:** the stones are there from the start.
- **Existing world:** run `genloc` once in the console (needs `devcommands`, as the host or in single player). Stones then appear in parts of the world nobody has visited yet. Land you've already explored doesn't get any.

Stones stand on fairly flat, open ground: trees and rocks keep clear of them, and no stone stands near lava.

## Configuration

- `CopiesPerStone` (default 3): how many times the game tries to place each god's stone. Only used when locations are generated (new world or `genloc`).
- `StoneSpacing` (default 400): the least distance in metres between any two stones.
- `DailyPowerKey` (default H): the key that calls a power stone's power.
- `Stone - <god>`: every number of every stone (skill gain, bonuses, costs, durations).
- `DebugLog`: writes how the stones were built and what happens with them to the BepInEx log.

The server's settings (everything except the key and `DebugLog`) are sent to every player.

## Console commands

- `ssov_info`: which stones you've found, bound and chosen; and, as the host or in single player, how many of each stone this world has and how far the closest one is.
- `ssov_goto [stone]` (needs `devcommands`): teleports you to the closest stone of that god (for example `ssov_goto thor`), or without a name to the closest stone you haven't found.
- `ssov_findall` / `ssov_reset` (need `devcommands`, for testing): your character finds every stone / forgets every stone.

## Translations

The mod comes in English and Turkish, and follows the game's language.

To change a text, or to add a language, put a file of your own in `BepInEx/config/StandingStonesOfValheim/Translations/<Language>/` (for example `BepInEx/config/StandingStonesOfValheim/Translations/Turkish/MyTexts.json`) with only the keys you want, in the same format as the mod's own files (`Translations/<Language>/StandingStonesOfValheim.json`). Files there win over the mod's own, and an update doesn't touch them. The language folder is named like the game's languages (English, Turkish, German, French...).

## Multiplayer

**Note:** Multiplayer and dedicated servers haven't been tested yet. It's built to work there, but if something seems off, please report it on GitHub Issues.

Install it on the server and on every player's game: the stones are part of the world, and everyone needs the mod to see them. Jötunn checks that everyone has the same version. Each player finds, binds and chooses their own stones.

## Feedback and bug reports

Found a bug or have an idea? Open an issue on [GitHub](https://github.com/aetuss/StandingStonesOfValheim/issues). Turn on `DebugLog` and include the matching lines from `BepInEx/LogOutput.log`. You can also reach me on Discord: **aethuswulf**.

## Made with AI

aetuss decided what this mod does and how it should work, and tested it in game. The code, the icon, this description and the Turkish translation were written by Claude, an AI model by Anthropic (through Claude Code); aetuss checked the Turkish texts in game.
