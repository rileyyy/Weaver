import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:injectable/injectable.dart';

/// Persists the refresh token across app restarts — OS keychain/keystore on
/// native, encrypted storage on web — so a user isn't asked to sign in every
/// time they open the app. The access token itself is never persisted here;
/// it's short-lived and kept in memory only (see `AuthSessionStore`).
abstract class SecureTokenStore {
  Future<String?> readRefreshToken();

  Future<void> writeRefreshToken(String token);

  Future<void> clear();
}

@LazySingleton(as: SecureTokenStore)
class FlutterSecureTokenStore implements SecureTokenStore {
  static const _refreshTokenKey = 'refresh_token';

  final FlutterSecureStorage _storage = const FlutterSecureStorage();

  @override
  Future<String?> readRefreshToken() async {
    try {
      return await _storage.read(key: _refreshTokenKey);
    } catch (_) {
      // A corrupted keystore entry or unavailable secure storage (e.g. a
      // locked-down browser profile) should fall back to "not logged in"
      // rather than crash the app on startup.
      return null;
    }
  }

  @override
  Future<void> writeRefreshToken(String token) async {
    await _storage.write(key: _refreshTokenKey, value: token);
  }

  @override
  Future<void> clear() async {
    await _storage.delete(key: _refreshTokenKey);
  }
}
