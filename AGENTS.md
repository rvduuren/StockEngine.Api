## Agent skills

### Repo phase

Current milestone is the **auth API**: start with [`README.md`](./README.md), [`GLOSSARY.md`](./GLOSSARY.md), and [`docs/adr/0001-identity-mariadb-rotating-refresh.md`](./docs/adr/0001-identity-mariadb-rotating-refresh.md). Warehouse/inventory rules and Dapper inventory work apply only when those modules exist in this repo.

### Ticket truth

When implementing a GitHub issue, resolve acceptance criteria in this order: **child issue body** → **parent** (`Part of #n` in the body) → **relevant ADRs** → **README** operational sections. If they conflict, parent + ADRs + README win; comment on the issue before large refactors.

### Issue tracker

GitHub Issues on `rvduuren/StockEngine.Api` (via `gh` CLI). See `docs/agents/issue-tracker.md`.

### Triage labels

Five canonical triage roles; label strings match role names on GitHub. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context layout: `GLOSSARY.md` and `docs/adr/` at the repo root. See `docs/agents/domain.md`.