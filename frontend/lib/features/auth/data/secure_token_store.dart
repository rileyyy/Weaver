/// Persists the refresh token across app restarts — OS keychain/keystore on
/// native, encrypted storage on web — so a user isn't asked to sign in every
/// time they open the app. The access token itself is never persisted here;
/// it's short-lived and kept in memory only (see `AuthSessionStore`).
abstract class SecureTokenStore {
  Future<String?> readRefreshToken();

  Future<void> writeRefreshToken(String token);

  Future<void> clear();
}
