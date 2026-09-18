# Small, self-hosted-friendly image: build the web bundle with the Flutter
# SDK, then ship only the static output behind nginx. The Flutter/Dart
# toolchain never reaches the final image.

FROM ghcr.io/cirruslabs/flutter:stable AS build
WORKDIR /app

COPY frontend/pubspec.yaml frontend/pubspec.lock ./
RUN flutter pub get

COPY frontend .
RUN flutter build web --release

FROM nginx:1.27-alpine AS runtime
COPY docker/frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/build/web /usr/share/nginx/html

EXPOSE 8080
