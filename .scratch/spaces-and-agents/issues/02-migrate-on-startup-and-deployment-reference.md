# 02: Migrate on startup by default, and one deployment reference

**What to build:** The backend image applies pending migrations when it starts unless an environment variable opts out, so an external compose (Komodo with tsdproxy) needs only `db`, `backend` and `frontend`. The README documents in one place every image, environment variable and volume an operator needs. See ADR-0008.

**Blocked by:** None (can start immediately)

**Status:** done

- [x] The backend migrates on startup by default; one documented env var turns it off
- [x] The dedicated migrate command still works for deployments that want an explicit step
- [x] Startup fails clearly if migration fails, rather than serving against an old schema
- [x] The README has a single reference section listing the images, every env var (default, and whether required) and every volume
- [x] The example `compose.yaml` still works, either dropping its one-shot migrate service or keeping it with migrate-on-startup off
- [x] Integration test covers startup with migration on and off (`StartupMigrationTests`)
