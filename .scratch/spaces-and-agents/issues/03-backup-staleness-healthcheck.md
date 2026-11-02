# 03: Backup staleness healthcheck

**What to build:** The example compose's backup service reports unhealthy when its newest dump is older than a threshold, so a silently failing backup shows up in `docker compose ps` and in Komodo. See ADR-0008.

**Blocked by:** None (can start immediately)

**Status:** ready-for-agent

- [ ] The backup service has a healthcheck that fails when the newest dump is older than a configurable age (default comfortably over one day)
- [ ] It's healthy straight after the startup dump
- [ ] The threshold is listed in the README's deployment reference
- [ ] Verified by hand: aging the newest dump, or lowering the threshold, turns the service unhealthy
