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

  static final String baseUrl = resolveApiBaseUrl(
    configured: _configured,
    isWeb: kIsWeb,
  );

  /// Resolves [baseUrl] now, so a missing native `API_BASE_URL` stops the app
  /// at launch with a clear message instead of deep inside dependency setup.
  static void validate() => baseUrl;
}

/// The pure rule behind [ApiConfig.baseUrl]. Throws a [StateError] when a
/// native build has no configured origin.
String resolveApiBaseUrl({required String configured, required bool isWeb}) {
  final origin = configured.trim().replaceFirst(RegExp(r'/+$'), '');
  if (origin.isNotEmpty) return '$origin/api';
  if (isWeb) return '/api';

  throw StateError(
    'API_BASE_URL must be provided via --dart-define for native builds.',
  );
}
