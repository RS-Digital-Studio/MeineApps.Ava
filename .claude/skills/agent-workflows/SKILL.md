---
name: agent-workflows
description: Projekt-spezifische Agent-Verkettung pro Szenario - welche Agents und Skills in welcher Reihenfolge laufen fuer Neue View, Neuer Service, Bug fixen, Release (App/Server), BingXBot-Problem, MVVM-Sanierung, Refactoring und Game-Update. Verwenden beim Start einer dieser Aufgaben, wenn unklar ist welcher Agent zuerst dran ist.
---

# Agent-Workflows (projekt-spezifisch)

Migriert aus der Root-`CLAUDE.md` (§6), damit die Tabelle nicht in jeder Session resident ist.
Inhalt unveraendert. Agent-Roster (Modell/Effort) → **globale CLAUDE.md**.

## Verkettung pro Szenario

| Szenario | Ablauf |
|----------|--------|
| **Neue View** | `planner` → Skill `new-view` → `mvvm-auditor` → `code-review` |
| **Neuer Service** | `planner` → Skill `new-service` → `code-review` → `tester` |
| **Bug fixen** | `debugger` → fixen → `code-review` → ggf. `tester` (Regression) |
| **Release (App)** | `pre-release` → `localize` → `deploy` |
| **Release (Server)** | `pre-release` → Skill `server-deploy` → `server-ops` (Verifikation) |
| **BingXBot-Problem** | `bingxbot` (Domain) → ggf. `debugger` / `server-ops` |
| **MVVM-Sanierung** | `mvvm-auditor` (App-weit) → `code-review` → Build-Verifikation |
| **Refactoring** | `health` → `refactor` → `code-review` → `tester` |
| **Game-Update** | `game-audit` → implementieren → `skiasharp` (falls Rendering) → `pre-release` |

## Skills (projekt-lokal)

`build-check`, `app-status`, `new-view`, `new-service`, `mvvm-check`, `localize-check`,
`release`, `server-deploy`, `changelog`, `build-release`, `agent-workflows`.

## Hooks (User-Settings)

- *SessionStart* — MVVM-Strict-Reminder, Auto-Commit-Erlaubnis, deutsche Umlaute,
  CLAUDE.md-Pflicht.
- *PostToolUse* (Write/Edit auf `View*.axaml.cs`) — Code-Behind-Hygiene-Reminder.
