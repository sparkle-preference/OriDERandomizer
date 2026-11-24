# Client property tests

Hypothesis properties over the client's C#. Python drives the C# through a small bridge exe, so a test
can put the server's writer next to the client's reader: seed lines, ticks, bingo frames, logic.

## What you need

- A checkout of `ori_rando_server` next to this repo (or `ORI_SERVER_REPO`), with its venv set up and
  `requirements-test.txt` installed. The suite uses the server's Hypothesis profiles and strategies.
- A .NET SDK (8 or newer) for its Roslyn, and the .NET Framework 4.8 reference assemblies (the
  Developer Pack). `ORI_CSC` points at a different `csc.dll`.
- `Managed\` copied from the game install, as for `typecheck.ps1`, and a built `Assembly-CSharp.dll`.

## Running

From the repo root:

```
..\ori_rando_server\.venv\Scripts\python.exe client_props\run.py [slice] [-v]
```

`slice` filters by file name (`logic`, `seedfile`, ...); none runs all of them. `HYPOTHESIS_PROFILE`
picks `dev` (the default: random inputs), `ci` (fixed inputs, about 3 minutes) or `deep` (slow and
thorough). A slice's first run builds its bridge, which is cached under `%TEMP%\client_props`.

Tests that need seeds served the way the website serves them skip unless a datastore emulator is up
on 127.0.0.1:8032. Use one of your own, never a shared one:

```
docker run -d --name client_props_emu -p 127.0.0.1:8032:8000 gcr.io/google.com/cloudsdktool/google-cloud-cli:emulators gcloud beta emulators datastore start --host-port=0.0.0.0:8000 --no-store-on-disk --project=orirandov3
```

`ingame_props.py` loads served seeds into the running game and skips unless `ORI_HARNESS=1`. It
needs a debug harness build at the title screen; the harness isn't part of this repo.

## Reading results

The bridge runs .NET Framework 4.8, not the game's Mono, so anything that depends on culture, number
formatting or `Uri` gets confirmed in game before it counts as a bug.

An open bug is two things: a named predicate that keeps the bug's inputs out of the general property,
and a test named for the rule it breaks, holding the smallest repro under `@unittest.expectedFailure`.
A fix deletes both. An "unexpected success" means a bug got fixed and its marker can go.

## Layout

- `<slice>_props.py`: one slice's properties.
- `cs/<slice>/`: the slice's C# side: stubs for engine-bound code, glue that turns game objects into
  plain values.
- `strategies/<slice>.py`: Hypothesis strategies.
- `bridge/` and `cs_bridge.py`: the bridge. `bridge_props.py` tests the bridge itself.
- `tools/bare_markers.py <module>`: runs a module's markers bare and shows where each one fails.
