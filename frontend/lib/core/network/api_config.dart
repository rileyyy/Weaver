import 'package:flutter/foundation.dart';

/// Resolves the backend's base URL, always ending in `/api`.
///
/// On web, requests go to a same-origin relative path (`/api`) by default —
/// the deployed nginx image reverse-proxies that to the backend, so the same
/// built image works regardless of the domain it's served from. Native
/// builds have no "same origin" to rely on, so they require an explicit
/// origin passed via `--dart-define=API_BASE_URL=...` (e.g.
/// `http://10.0.2.2:8080`); the same override also works for web dev, where
/// there's no nginx proxy in front of `flutter run`.
class ApiConfig {
  const ApiConfig._();

  // --dart-define is the only way to pass this at compile time.
  // ignore: do_not_use_environment
  static const String _configured = String.fromEnvironment('API_BASE_URL');

  static String get baseUrl {
    if (kIsWeb) {
      return _configured.isEmpty ? '/api' : '$_configured/api';
    }

    if (_configured.isEmpty) {
      throw StateError(
        'API_BASE_URL must be provided via --dart-define for native builds.',
      );
    }
    return '$_configured/api';
  }
}
