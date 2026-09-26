import 'package:weaver/features/auth/data/secure_token_store.dart';

/// flutter_secure_storage has no test-environment implementation.
class InMemoryTokenStore implements SecureTokenStore {
  String? token;

  @override
  Future<String?> readRefreshToken() async => token;

  @override
  Future<void> writeRefreshToken(String value) async => token = value;

  @override
  Future<void> clear() async => token = null;
}
