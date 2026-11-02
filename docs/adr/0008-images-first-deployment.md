# 8. The published images are the product; compose.yaml is an example

Status: accepted (2026-11-02)

## Context

Riley's homelab runs its stacks under Komodo, with its own compose files.
tsdproxy provides each stack's Tailscale name and TLS certificate. The repo
is public, and the current `compose.yaml` with Caddy is useful for anyone
trying Weaver. But production doesn't depend on it. The one-shot `migrate`
service is the part of that compose file that's hardest to copy into
another one.

## Decision

- **The GHCR images are what ships.** The README documents every environment
  variable and volume in one place, including:
  - the database connection
  - the JWT key
  - the time zone
  - the attachments path, kept separate so it can live on other storage
  - the first admin's username and password, since registration is removed
    and an admin creates every other account (ticket 04)
- **The backend migrates on startup by default.** An environment variable
  turns that off. This is safe with a single backend instance.
- **`compose.yaml` stays as a public example**, including Caddy with its
  internal certificate authority and the backup service. The backup service
  gets a healthcheck that fails when the newest dump is too old, so a failed
  backup is visible.
- **Backups belong to whoever runs the images.** Riley's homelab backs up
  Postgres, and it must also back up the attachments path.

## Consequences

- A minimal external compose needs only `db`, `backend` and `frontend`, plus
  the environment and volumes.
- The dedicated `migrate` command stays available for deployments that want
  an explicit step.
