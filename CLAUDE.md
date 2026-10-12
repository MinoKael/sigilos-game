# Sigilos: Claude Code workflow

## Advisor (Opus 5.5)

The main session runs Sonnet 5.5. `advisorModel` gives it a server-side advisor backed by Opus 5.5. The advisor recommends; the main session executes.

Consult it at three checkpoints:
1. **Before committing to a plan**: nontrivial task (several files, a new mechanism, or an unknown cause), after the first investigation, before the first edit.
2. **After the same error twice**: same failing command, test or symptom. Do not try a third time first.
3. **Before declaring done**: nontrivial task, after the last build/test run. Ask for a review of the change, tests, regression risk and open limits.

Give it: the goal in one sentence; files touched or planned (`path:line`); the evidence (commands run, exact error lines, test counts); what you already tried; your plan or conclusion; the specific question.

Use the answer: follow it, or state in the reply why not. Treat its claims as hypotheses until a read or a run confirms them. It never replaces build/test verification.

Skip it when the task is trivial (one obvious edit, a read, a question answered from code) or the user says so. If the advisor is unavailable or errors, say "advisor unavailable" in the final report and self-review with the `verify-and-stop` checklist.

## Retries

- Retry an action only with new evidence: changed input, a different error, or a changed file. Same action + same error = stop (advisor checkpoint 2).
- Transient failures (timeout, network, "file in use"): one retry, then report.
- Gate on exit codes. Never put `| tail` or `| head` before `&&`; the pipe hides a failing exit code. Read the test result before reporting success.
- Never retry a step the user denied; change approach or ask.
- Grep with a narrow `path`, never the repo root: `.godot/` and `Assets/` make it time out.

## Commits

Never commit. The user commits manually.

## Account server

The account server repo `D:\dev\sigilos-server` may be read and modified from this project. Never commit there either.
