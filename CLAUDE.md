# Sigilos: Claude Code workflow

## Advisor (Opus 5.5)

The main session runs Sonnet 5.5. `advisorModel` gives it a server-side advisor backed by Opus 5.5 Max Effort. The advisor recommends; the main session executes.

Consult it at three checkpoints:
1. **Before committing to a plan**: nontrivial task (several files, a new mechanism, or an unknown cause), after the first investigation, before the first edit.
2. **After the same error twice**: same failing command, test or symptom. Do not try a third time first.
3. **Before declaring done**: nontrivial task, after the last build/test run. Ask for a review of the change, tests, regression risk and open limits.

Give it: the goal in one sentence; files touched or planned (`path:line`); the evidence (commands run, exact error lines, test counts); what you already tried; your plan or conclusion; the specific question.

Use the answer: follow it, or state in the reply why not. Treat its claims as hypotheses until a read or a run confirms them. It never replaces build/test verification.

Skip it when the task is trivial (one obvious edit, a read, a question answered from code) or the user says so. If the advisor is unavailable or errors, say "advisor unavailable" in the final report and self-review with the `verify-and-stop` checklist.

### Messages starting with  `bug:|advisor:`

When the user's message starts with `bug:/advisor:`, the advisor owns the fix:
1. Investigate first (reproduce, read the code, collect evidence), then consult the advisor **before the first edit**, even if the fix looks small.
2. Consult it again **after the fix is applied and verified**, before reporting. Do not report a bug as fixed without that review.
3. If the first fix does not resolve it, consult again before the next attempt. Never try a second fix blind.
4. Follow the advisor's diagnosis or state in the reply why not. Build/test/in-game verification still comes from the main session.

## Retries

- Retry an action only with new evidence: changed input, a different error, or a changed file. Same action + same error = stop (advisor checkpoint 2).
- Transient failures (timeout, network, "file in use"): one retry, then report.
- Gate on exit codes. Never put `| tail` or `| head` before `&&`; the pipe hides a failing exit code. Read the test result before reporting success.
- Never retry a step the user denied; change approach or ask.
- Grep with a narrow `path`, never the repo root: `.godot/` and `Assets/` make it time out.

## Test save

Use the local `debugger` save whenever validating by running the game (captures, manual checks): launch with `-- --save=debugger`. It is a plain JSON in `user://debugger.json` (all homunculi 6★ max level, everything cleared, 700 unupgraded Legendary 6★ runes, slots 1-3 with percent/Speed mains only, 5+ of each set×slot×main). The game opens it directly, offline, and never uploads it. It keeps progress between launches; `user://debugger.base.json` is the pristine copy (copy it over `debugger.json` to reset). Nothing in the code builds it. Never touch the real save (`sigilos.json`) or register accounts on the server.

## Commits

Never commit. The user commits manually.

## Account server

The account server repo `F:\GameDev\sigilos-server` may be read and modified from this project. Never commit there either.
