/// Build-time configuration passed with `--dart-define`.
class ApiConfig {
  const ApiConfig._();

  /// The server origin to start with, e.g. `http://10.0.2.2:8080` for the
  /// Android emulator or `http://localhost:8080` for `flutter run` on web
  /// (which has no nginx proxy in front of it). Optional: web otherwise
  /// uses its own origin, and native builds ask at sign-in.
  // --dart-define is the only way to pass this at compile time.
  // ignore: do_not_use_environment
  static const String buildTimeOrigin = String.fromEnvironment('API_BASE_URL');
}
