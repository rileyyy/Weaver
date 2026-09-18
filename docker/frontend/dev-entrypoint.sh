#!/usr/bin/env bash
set -euo pipefail

flutter pub get

# flutter run only hot-reloads on a keypress; there's no built-in file
# watcher for headless (non-IDE) use. We give it a fifo as stdin and inject
# the 'r' keystroke ourselves whenever the mounted source changes on the
# host, so a save reloads automatically without anyone attaching a tty.
# (The container still sets stdin_open/tty in compose, so `docker compose
# attach frontend` and pressing r/R by hand works too, as a fallback if the
# watcher ever misses something.)
mkfifo /tmp/flutter-stdin
flutter run -d web-server \
  --web-hostname 0.0.0.0 \
  --web-port 8080 \
  --dart-define=API_BASE_URL="${API_BASE_URL:-http://localhost:8080}" \
  < /tmp/flutter-stdin &
FLUTTER_PID=$!

exec 3>/tmp/flutter-stdin
trap 'kill "$FLUTTER_PID" 2>/dev/null || true' EXIT

inotifywait -m -r -e modify,create,delete,move \
  --exclude '(\.dart_tool|/build/|\.git)' \
  lib/ pubspec.yaml \
  | while read -r _; do
      echo "r" >&3
    done &
WATCHER_PID=$!

wait "$FLUTTER_PID"
kill "$WATCHER_PID" 2>/dev/null || true
