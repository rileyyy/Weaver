import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/auth/data/auth_repository.dart';
import 'package:weaver/features/auth/models/auth_session.dart';
import 'package:weaver/features/auth/models/auth_user.dart';

/// Talks to `/api/auth/*` using a plain, unauthenticated [http.Client] — see
/// the `rawHttpClient` binding in `NetworkModule`. These endpoints must never
/// go through the token-attaching/auto-refresh client: register/login have
/// no token yet, and refresh is what that client would otherwise recurse
/// into trying to obtain one.
@LazySingleton(as: AuthRepository)
class ApiAuthRepository implements AuthRepository {
  ApiAuthRepository(
    @Named('rawHttpClient') this._client,
    @Named('apiBaseUrl') this._baseUrl,
  );

  final http.Client _client;
  final String _baseUrl;

  @override
  Future<AuthSession> register(String username, String password) =>
      _authenticate('/auth/register', {
        'username': username,
        'password': password,
      });

  @override
  Future<AuthSession> login(String username, String password) => _authenticate(
    '/auth/login',
    {'username': username, 'password': password},
  );

  @override
  Future<AuthSession> refresh(String refreshToken) =>
      _authenticate('/auth/refresh', {'refreshToken': refreshToken});

  @override
  Future<void> logout(String refreshToken) async {
    await _client.post(
      _uri('/auth/logout'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({'refreshToken': refreshToken}),
    );
  }

  Future<AuthSession> _authenticate(
    String path,
    Map<String, String> body,
  ) async {
    final response = await _client.post(
      _uri(path),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode(body),
    );
    _checkOk(response, 'Sign-in failed');

    final json = jsonDecode(response.body) as Map<String, dynamic>;
    final user = json['user'] as Map<String, dynamic>;

    return AuthSession(
      accessToken: json['accessToken'] as String,
      accessTokenExpiresAtUtc: DateTime.parse(
        json['accessTokenExpiresAtUtc'] as String,
      ),
      refreshToken: json['refreshToken'] as String,
      user: AuthUser(
        id: user['id'] as String,
        username: user['username'] as String,
        kind: userKindFromWire(user['kind'] as String),
      ),
    );
  }

  Uri _uri(String path) => Uri.parse('$_baseUrl$path');

  void _checkOk(http.Response response, String fallbackMessage) {
    if (response.statusCode >= 200 && response.statusCode < 300) return;
    throw ApiException(
      '${_problemDetail(response) ?? fallbackMessage} (${response.statusCode}).',
    );
  }

  String? _problemDetail(http.Response response) {
    try {
      final body = jsonDecode(response.body);
      return body is Map<String, dynamic> ? body['detail'] as String? : null;
    } on FormatException {
      return null;
    }
  }
}
