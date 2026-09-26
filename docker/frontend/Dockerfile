# Small, self-hosted-friendly image: build the web bundle with the Flutter
# SDK, then ship only the static output behind nginx. The Flutter/Dart
# toolchain never reaches the final image.

# Keep in step with FLUTTER_VERSION in .github/workflows/docker-publish.yml
# (CI passes it in, so the default only matters for local builds).
ARG FLUTTER_VERSION=3.44.0
FROM ghcr.io/cirruslabs/flutter:${FLUTTER_VERSION} AS build
WORKDIR /app

COPY frontend/pubspec.yaml frontend/pubspec.lock ./
RUN flutter pub get

COPY frontend .
RUN flutter build web --release

FROM nginx:1.27-alpine AS runtime
COPY docker/frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/build/web /usr/share/nginx/html

EXPOSE 8080
