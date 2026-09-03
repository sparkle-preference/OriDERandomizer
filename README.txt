This repository contains the client code (distributed as a modified Assembly-CSharp.dll) for the OriDE randomizer.

The website, seed generator, bingo and other web tools are hosted at https://github.com/turntekGodhead/ori_rando_server

Built on top of sigmasin's incredible work.

Everything a player needs is on the website:

https://orirando.com/faq            Installing, generating seeds, trackers, bingo, Archipelago, practice mode
https://orirando.com/patchnotes     What changed, and when
https://orirando.com/discord        Ask a person

----------------------------

Setup:

Install with the Rando App from https://orirando.com/app, or by hand: put Assembly-CSharp.dll in Steam/steamapps/common/Ori DE/oriDE_Data/Managed.
Generate a seed at https://orirando.com and put the downloaded randomizer.bfr next to OriDE.exe.

The randomizer only works with Ori and the Blind Forest: Definitive Edition on PC. The Windows Store version cannot be modded.

----------------------------

COMMANDS

Binds are in RandomizerRebinding.txt, which the game writes on first launch and reads on every launch. Every entry below can be rebound there; the file lists the ones with no default too.

Alt+R               Warp: the world map, to travel to a spirit well
Alt+T               Replay the last pickup message
Alt+L               Reload the seed file, to change seeds without restarting
Alt+P               Skill tree and shard progress
Alt+K               Keysanity door progress
Alt+B               Bonus item inventory
Alt+Q               Cycle the active bonus item
Alt+1 - Alt+5       Trees, map altars, teleporters, relics, run stats
Alt+C               Toggle color shifting
Alt+S               Save the current game as a practice segment
Alt+V / Alt+F       Chaos mode: toggle messages / force an effect
PgUp / PgDn         Save select: jump three slots (with Shift, ten)
Bash (held)         On the world map, warp to the spirit well under the cursor
Grenade             Double bash

The double bash bind exists to create parity between playing randomizer on controller and keyboard+mouse. If any of the binds specified are held when a bash ends, a double bash will automatically occur. To also make any of the binds specified end a bash on their own, add "Tap" as a bind for the double bash function.

RandomizerSettings.txt holds the rest, including the bash deadzone on controller (0 for none through 1 for full).

If you get stuck, Warp somewhere else. Wall jump, double jump and the post-Ginso escape are all sticking points. Don't warp out of a room with a temporary lock (the Sein fronkey fight, the Ginso miniboss) with the door still closed, or you will probably be softlocked.

----------------------------

CHAOS MODE

Unbound by default; give "Toggle Chaos" a bind in RandomizerRebinding.txt. Just try it once before reading any further. Toggling it off removes every active effect immediately.

Chaos mode spawns random effects at intervals of 5-15 seconds, each lasting 5-60 seconds, so several run at once. Movement speed, ice physics, acceleration, gravity strength and direction, gravity wells, short and long range teleports, camera zoom, poison, invisibility, damage vulnerability and random velocity vectors are all on the table.

Its randomness comes from the system clock rather than the seed, so it differs between players on the same seed and between runs of one. This mode is intended for fun and to feel the joy of getting teleported out of bounds while you're stuck in a cutscene. I recommend saving a lot.

----------------------------

OTHER FILES HERE

Practice Mode.txt   Practice segments, boxes, variants and ghosts, in full
SH Format.txt       The seed file format

----------------------------

DEVELOPMENT

Preparation:
To use the included project/solution and enable building the rando, copy the `Managed` folder of a clean game install (found in `Ori DE/oriDE_Data/Managed`) into the root of this repo, so that the `Managed` folder sits next to the `dnspy-modfile.json` file.
    - Important: It needs to have a vanilla `Assembly-CSharp.dll`. If you have the rando installed, revert the file to its original, or repair the game via steam / do a clean reinstall.

Developing:
When adding new class or resource files, they need to be added to the corresponding list inside `dnspy-modfile.json`.
Classes from the source assembly that are now modified also need to be added to the "replaceClasses" section.

Formatting:
The code can be automatically formatted using `dotnet-format.bat`.
Note however, that this can't enforce everything. For example, it enforces braces being on the same line as an `if`, but doesn't force the braces to be present; that has to be done manually.

Building:
Building the rando uses a fork of dnSpy found at https://github.com/AsmPrgmC3/dnSpy/releases/latest

Build steps:
- Download dnSpy
- Run `path\to\dnSpy.exe --modfile:dnspy-modfile.json --runModfile --closeAfterModfile`

`Assembly-CSharp.dll` will contain the updated rando.

If you're compiling often, it might be more convenient to avoid dnSpy loading times by keeping it open.
To do that, open dnSpy and select "File -> Load Modfile...". Select the `dnspy-modfile.json` next to this README.
Once that's done, you can run it anytime via "File -> Run Modfile" (or the shortcut Ctrl+Shift+M).

To avoid copying the `Assembly-CSharp.dll` after every recompile, it's also possible to change the "outputFile" path in `dnspy-modfile.json` to point to your game's `Assembly-CSharp.dll`,
or to symlink the game file to the one here.
