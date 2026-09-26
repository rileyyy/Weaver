import 'package:weaver/shared/models/user.dart';

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

  factory AuthSession.fromJson(Map<String, dynamic> json) => AuthSession(
    accessToken: json['accessToken'] as String,
    accessTokenExpiresAtUtc: DateTime.parse(
      json['accessTokenExpiresAtUtc'] as String,
    ),
    refreshToken: json['refreshToken'] as String,
    user: User.fromJson(json['user'] as Map<String, dynamic>),
  );

  final String accessToken;
  final DateTime accessTokenExpiresAtUtc;
  final String refreshToken;
  final User user;

  /// Treats the token as expired slightly early so it can't expire between
  /// this check and the server receiving the request.
  static const expiryLeeway = Duration(seconds: 30);

  bool get isAccessTokenExpired => DateTime.now().toUtc().isAfter(
    accessTokenExpiresAtUtc.subtract(expiryLeeway),
  );
}
