# StandingStonesOfValheim

A Valheim mod that adds Skyrim-style standing stones of the Norse gods to the world: find them, choose one, and it guides you. See [package/README.md](package/README.md) for what it does.

## Building

`build.ps1` checks the translation files, compiles `src/*.cs` with the .NET Framework C# compiler (C# 5) against the game's assemblies, BepInEx and Jötunn, then packs `package/` and the DLL into `dist/StandingStonesOfValheim-<version>.zip` for Thunderstore. Adjust the paths at the top of the script to your install.

## How the stones are made

No asset bundle: each stone is a Jötunn location built when the game starts, from vanilla prefabs copied as plain meshes and colliders (scripts removed): a scaled vegvisir as the pillar with its emission tinted by the stone's kind, a ring of rocks, the god's items, a `TerrainModifier` that levels the ground, and the god's rune drawn in code as a sprite that settles onto the pillar's face when the location loads. A stone's effect is a status effect (`SE_Stone`, a `SE_Stats` subclass) using the game's own hooks where they exist, with Harmony patches for the rest. A character's stones are kept in `Player.m_customData`. Hints ask the server for the location list over a routed RPC. The rune altar is a cloned vegvisir made a building piece, with a Jötunn GUI window.

## Made with AI

aetuss decided what this mod does and how it should work, and tested it in game. The code, the icon, this description and the Turkish translation were written by Claude, an AI model by Anthropic (through Claude Code); aetuss checked the Turkish texts in game.
