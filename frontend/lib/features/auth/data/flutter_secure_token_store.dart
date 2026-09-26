import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:injectable/injectable.dart';
import 'package:weaver/features/auth/data/secure_token_store.dart';

@LazySingleton(as: SecureTokenStore)
class FlutterSecureTokenStore implements SecureTokenStore {
  static const _refreshTokenKey = 'refresh_token';

  final FlutterSecureStorage _storage = const FlutterSecureStorage();

  @override
  Future<String?> readRefreshToken() async {
    try {
      return await _storage.read(key: _refreshTokenKey);
    } on Exception {
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
