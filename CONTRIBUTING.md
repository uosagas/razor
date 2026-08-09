# Contributing to UOSagas Razor

Thanks for your interest in improving UOSagas Razor! This document explains how to get a working
dev setup, what we expect from contributions, and — importantly — how testing against the live
shard works.

## ⚠ The one thing to know first: test server vs. production

- **Your own builds only work on the UOSagas test server.** The production server runs a version
  and signature gate and only accepts the latest officially deployed release.
- Changes you contribute become playable on production **after they are merged into `main` and
  deployed by the UOSagas team**. There is no way around this — it keeps the game fair and every
  live change reviewed.
- Ask in the community Discord for access to the test server if you want to develop against a
  live game.

## Development setup

Requirements:

- **.NET SDK 9.0.200 or newer** — the solution file uses the `.slnx` format. The projects
  themselves target `net8.0`.

```
git clone https://github.com/uosagas/razor.git
cd razor
dotnet build
dotnet test
```

The full test suite (300 tests, including headless Avalonia UI tests) runs without a display and
must stay green. Please add tests for new core behavior — the suite is the reason this port stays
compatible with Razor CE file formats.

## Architecture in five sentences

1. `Razor.Core` is **UI-free**: world model, agents, macros, the three script engines
   (Razor script, Lua, VScript) and the packet mirror. It must never reference Avalonia.
2. `Razor.Avalonia` is the UI. It never touches core state directly from the UI thread —
   reads come from `UiSnapshot`s, writes go through `GameThread.Post(...)`.
3. `Razor.Plugin` is the entry point the UOSagas client bootstrap loads; the ABI boundary is
   defined in `external/AssistantApi` (a synced copy — API changes happen client-side first).
4. Much of `Razor.Core` is ported from Razor CE; those files keep their original license headers.
   When porting CE behavior, stay faithful — deliberate deviations must be documented in a comment.
5. Everything the assistant does goes through the client. There is no direct file or socket access
   to the shard: the client validates every action.

## Making changes

1. Fork the repository and create a feature branch from `main`.
2. Keep changes focused — one topic per pull request.
3. Match the existing code style of the file you are editing (yes, including the comment style).
4. Run `dotnet test` — 300 green tests are the baseline; new core logic needs new tests.
5. If your change affects user-visible scripting behavior, mention it so the docs on
   [share.uosagas.com](https://share.uosagas.com) can be updated.
6. Open a pull request against `main` and describe **what** changed and **why**.

A maintainer will review your PR. After the merge, the change ships with the next official
release through the launcher.

## Reporting bugs

- Use the bug report issue template.
- Crash logs live in `Plugins/SagasRazor/Data/CrashLogs/` next to your client installation —
  please attach the relevant `crash-*.txt`.
- For script problems, include the smallest script that reproduces the issue.

## License

UOSagas Razor is licensed under the **GPL-3.0** (see [LICENSE](LICENSE)). By contributing, you
agree that your contributions are licensed under the same terms. Files ported from
[Razor CE](https://github.com/markdwags/Razor) keep their original copyright notices — do not
remove them.
