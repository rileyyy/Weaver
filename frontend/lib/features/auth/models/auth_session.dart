import 'package:weaver/features/auth/models/auth_user.dart';

/// The result of a successful login/register/refresh: a short-lived access
/// token to send with API calls, and a longer-lived refresh token used to
/// silently obtain a new one. Neither is persisted by this class itself —
/// see `AuthRepository`/`SecureTokenStore` for where the refresh token is
/// cached across app restarts.
class AuthSession {
  const AuthSession({
    required this.accessToken,
    required this.accessTokenExpiresAtUtc,
    required this.refreshToken,
    required this.user,
  });

  final String accessToken;
  final DateTime accessTokenExpiresAtUtc;
  final String refreshToken;
  final AuthUser user;

  bool get isAccessTokenExpired => DateTime.now().toUtc().isAfter(accessTokenExpiresAtUtc);
}
