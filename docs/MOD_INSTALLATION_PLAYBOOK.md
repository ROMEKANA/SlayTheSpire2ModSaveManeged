# Reusable MOD installation workflow

This guide records the installation experience the owner explicitly liked in UnifiedVanillaSave. Use it for future changes here and adapt it when building other MOD repositories. Copy this file and add a link from the destination repository's AGENTS.md; this repository cannot automatically configure unrelated repositories.

## Confirmed preference and scope

The owner reported that the latest one-command installation instructions were clear and should be retained. They also reported that the MOD still did not unify saves despite loading without errors. Preserve the installation experience; do not interpret that feedback as functional acceptance of save routing.

The preferred user experience is:

- One short action sentence, then one copyable, single-line command.
- One setup entry point that builds, prepares configuration, checks backups and installs.
- Three machine-specific directories in a separate three-line text file.
- No repeated JSON edits, hash copying, temporary shell variables or manual old-MOD moves.
- Preserve existing configuration, first backups, prior installations and user data.

Keep explanations and engineering details out of the normal installation steps. When a step fails, ask for its output and stop that sequence rather than giving commands that assume success.

## Files and responsibilities

| File | Responsibility |
| --- | --- |
| `scripts/Setup.ps1` | Own the complete setup/update sequence and report failures. |
| `local/paths.txt` | Store machine-specific paths; automatically create on first use. Git-ignore this file. |
| `scripts/paths.example.txt` | Ship a three-line template without personal data. |
| `scripts/Build.ps1` | Validate SDK/references, test, build and stage declared payloads. |
| `scripts/Install.ps1` | Validate the destination and install only declared payloads. |
| A setup/config helper | Generate or migrate config without manual JSON editing. |
| Setup tests | Exercise repeat runs, preservation and rollback with temporary fixtures. |
| `README.md` | Give the human concise, exact commands in their preferred language. |

Adapt names and tooling to the target game. Reuse the sequence and preservation guarantees, not this MOD's IDs, method names, SDK version, game hashes or audit approvals.

## Three-line path contract

The file contains exactly three non-empty lines, in this order:

1. Game installation root.
2. Game user-data/save root.
3. This MOD's local configuration directory.

No headings, JSON, comments, key names or quotes are required. The paths file is the location source for setup; derive DLL directories, config files and installation targets from it. If a conflicting environment override exists, report the conflict instead of silently writing a different config.

Create the initial file from platform defaults with environment variables resolved to absolute paths. Retain it unchanged on later runs. Accept an explicit alternate paths-file argument. If paths are invalid, show the exact missing path and the paths-file location. Let the owner edit this file only when their installation locations differ from the defaults.

The distributable STS2 template uses placeholders rather than a real username:

```text
C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2
%APPDATA%\SlayTheSpire2
%LOCALAPPDATA%\UnifiedVanillaSave
```

The generated local file holds resolved paths. Never commit personal paths, generated configs, save contents, game DLLs or backup payloads. Prefer editing the existing ignored file over asking the owner to enter the same directories on every update.

## Setup sequence

1. Require the game to be closed. Resolve and validate paths before moving installed files.
2. Lock setup against concurrent runs.
3. Check the required SDK/tool versions and external references. A newer SDK major version is not automatically compatible with global.json or a tool-package format.
4. Run relevant tests, build and stage a complete release containing only declared payloads. Fail before installation if compilation fails.
5. Generate or migrate configuration. Preserve user-selected backup destinations and other persistent settings. Back up old config before replacement and write the new file atomically.
6. Create or verify the first backup when the MOD touches persistent data. Do not refresh an initial snapshot with later progress. Keep backup roots outside active saves.
7. Preserve the old MOD directory outside the loader's scanned mods tree. Renaming a folder under mods may still load its manifest.
8. Install the staged release. If copying fails, preserve partial output outside mods and restore the previous MOD.
9. Report completion, relevant paths and the next manual acceptance check. Do not report feature success from a successful installation.

The current STS2 implementation coordinates backup verification with config generation before replacing the installation. Equivalent ordering is acceptable if it preserves these invariants. If privileged directory access is actually denied, report that error specifically; do not disable security settings or discard the old installation.

Automatic config generation does not authorize arbitrary game builds. Carry forward a matching existing audit or use a source-reviewed fingerprint where required by the MOD. Otherwise stop for source investigation. Do not turn an audit flag on merely because a method with the same name exists. The installation pattern does not require other MODs to adopt STS2-specific fingerprint logic.

## Instruction format

Use the following structure in Japanese for this owner. Each action is one line and each code block contains one command. Do not combine commands with separators or depend on variables established in previous chat turns.

リポジトリのフォルダで最新版を取得する。
```powershell
git pull --ff-only origin main
```

ゲームを終了し、一括セットアップする。
```powershell
.\scripts\Setup.ps1
```

必要な場合だけ、保存した3つの場所を確認する。
```powershell
Get-Content .\local\paths.txt
```

Make the initial SDK installation a separate prerequisite when missing. Pin development tools to versions compatible with the selected SDK when necessary. Do not repeatedly tell the owner to copy hashes into JSON or assemble installation steps by hand.

## Verification and reporting

Keep these outcomes separate:

| Outcome | Evidence |
| --- | --- |
| Source checks passed | Tests, builds, syntax checks and their actual results. |
| Setup completed | Configuration/backup/install sequence and preservation checks. |
| MOD loaded | Loader and initializer logs for the current build. |
| Feature worked | The owner's real-game acceptance test and actual file/game behavior. |

For setup changes, use temporary fixture directories to check initial path-file creation, malformed input, repeat runs, config/backup preservation, unknown-version rejection where applicable, and rollback after partial installation. Fixture payloads are not game builds. Check relevant Windows/PowerShell compatibility explicitly and state when only Linux execution was tested.

If setup is accepted but the feature fails, retain the accepted setup flow. Investigate the feature using the current logs and actual callers/results; avoid speculative patches or a more complicated installation process. For this save MOD, initialization and path-return logs do not establish real save I/O or bidirectional progress sharing.

Before publishing, follow the destination repository's Git policy and explicit task instructions. This repository currently authorizes verified fast-forward updates to main. Do not copy that publication policy into another project without checking its instructions.
