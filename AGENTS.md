# AGENTS.md — Personal AI-First Development Rules

## Purpose and ownership

- This repository is a small personal project, not a team project.
- The AI agent owns implementation, debugging, refactoring, automated checks, and technical documentation.
- The human owner provides requirements, feedback, and real-device/manual acceptance testing.
- Optimize the codebase for reliable AI comprehension and modification. Human-oriented explanations belong primarily in `README.md`.
- These are default rules across personal repositories. Explicit task instructions and more specific repository constraints take precedence.

## Autonomy and decisions

- Implement requested changes end-to-end without waiting for routine approvals.
- Make reasonable assumptions when requirements are underspecified; state consequential assumptions in the final report.
- Choose architecture, naming, internal APIs, file organization, and implementation details autonomously.
- Refactor beyond the immediate edit area when it materially improves simplicity, maintainability, correctness, or testability. Preserve intended behavior and existing user data.
- Ask a question only when a decision would materially change user-visible behavior, introduce irreversible data loss, require unavailable secrets/permissions, or commit the owner to a significant external cost.
- Prefer a simple working design over speculative abstractions, premature optimization, or formal team processes.

## Code and documentation

- Prefer clear modules, explicit contracts, consistent naming, and straightforward control flow that future coding agents can navigate easily.
- Follow existing project conventions where sensible; improve inconsistent conventions when the benefit outweighs migration risk.
- Write identifiers, code comments, docstrings, and AI-facing technical documentation in English unless required by an external interface.
- Comment non-obvious intent, invariants, tradeoffs, and platform constraints; avoid narrating obvious code or producing redundant comments.
- Keep `README.md` in Japanese and understandable to a human who wants to use or run the project. It should contain the project overview, prerequisites, setup, installation/build steps, exact copy-pasteable shell commands, run instructions, configuration, and basic usage.
- Update `README.md` whenever the setup, commands, configuration, or user-facing behavior changes. Distinguish PowerShell from Bash commands when relevant.
- Match the application's existing user-facing language unless the task requests otherwise.
- Do not create elaborate planning documents, architecture layers, or process scaffolding for routine tasks. Add focused technical notes only when they materially help later maintenance.

## MOD setup and user instructions

- For MOD installation/setup work, follow [the reusable installation playbook](docs/MOD_INSTALLATION_PLAYBOOK.md).
- Retain one-command setup, automatic config generation/migration, and the ignored three-line machine-path file. Present each action on one line followed by one single-line command; avoid state carried in shell variables across chat turns.
- The owner accepted the setup instructions, but reported that save unification still failed. Treat setup/loader success and real feature acceptance as separate outcomes.
- When adapting the pattern to another repository, carry over the playbook and link it from that repository's instructions; check its game contracts and publication policy independently.

## Dependencies and changes

- You may add, remove, or update dependencies without routine approval when justified by the task.
- Favor maintained, compatible dependencies with appropriate licenses; avoid large libraries for trivial functions.
- Update manifests, lockfiles, and setup documentation consistently.
- Preserve relevant compatibility, migration paths, and existing persisted data unless the user explicitly requests breaking changes.
- Never commit secrets, tokens, private keys, credentials, or real personal data. Use placeholders and documented environment variables.
- Do not deploy, change external production resources, spend money, or run destructive operations against real data without explicit authorization. Routine Git commit/push to `main` is authorized under the Git workflow below.

## Verification before delivery

- Independently run relevant automated tests, builds, lint checks, and type checks available in the environment.
- For bug fixes, reproduce the cause when feasible and add or update a focused regression test where useful.
- For new or changed behavior, add proportional automated coverage; do not create an excessive test framework for a tiny project.
- Inspect the final diff for unrelated edits, secrets, accidental generated files, and obvious regressions. Run `git diff --check` before committing.
- For documentation-only changes, validate formatting and any commands or links that can reasonably be checked.
- If tests do not exist, perform meaningful build or smoke checks where possible. Do not report unperformed checks as passed.
- Human acceptance testing is separate. Never imply that physical-device, graphical, or other human tests were completed unless actually performed.

## Git workflow: direct main, no PRs

- This is a single-developer workflow. Do not create feature branches, open pull requests, or require code review for routine tasks.
- After relevant checks pass, commit the task's verified changes and push directly to `origin/main` without requesting routine confirmation, if the environment grants permission.
- Stage only intended task files. Use concise, descriptive English commit messages.
- Before pushing, fetch `origin/main` and verify that the push is a fast-forward. If working on an environment-managed task branch, pushing the verified `HEAD` to `main` is acceptable only when it is a fast-forward and the environment allows it.
- Never force-push, rewrite shared history, discard someone else's changes, or bypass branch protections. Do not create or switch branches just to work around platform restrictions.
- If validation fails, do not push. Fix and rerun checks; if blocked, leave the changes available and report the blocker.
- If push access is unavailable, or the remote has diverged, do not invent a successful push or work around access controls. Report the situation and the exact remaining action.
- Do not commit or push during read-only, analysis-only, or planning-only requests.

## Completion report

- Report in Japanese, briefly and concretely:
  1. What changed and any important design decisions.
  2. Tests/builds/checks actually run and their outcomes.
  3. Commit ID and whether the push to `main` succeeded, failed, or was not attempted.
  4. Any known limitations and what the human should manually test next.
- Do not require the owner to inspect a pull request or code diff as a routine completion step.
