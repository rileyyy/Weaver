import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/features/auth/data/auth_repository.dart';
import 'package:weaver/features/auth/models/auth_session.dart';

/// Talks to `/api/auth/*` using a plain, unauthenticated [http.Client] — see
/// the `rawHttpClient` binding in `NetworkModule`. These endpoints must never
/// go through the token-attaching/auto-refresh client: register/login have
/// no token yet, and refresh is what that client would otherwise recurse
/// into trying to obtain one.
@LazySingleton(as: AuthRepository)
class ApiAuthRepository implements AuthRepository {
  ApiAuthRepository(
    @Named('rawHttpClient') http.Client client,
    @Named('apiBaseUrl') String baseUrl,
  ) : _api = JsonApiClient(client, baseUrl);

  final JsonApiClient _api;

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
  Future<void> logout(String refreshToken) => _api.postIgnoringBody(
    '/auth/logout',
    {'refreshToken': refreshToken},
    failureMessage: 'Sign-out failed',
  );

  Future<AuthSession> _authenticate(String path, Map<String, String> body) =>
      _api.post(
        path,
        body,
        (json) => AuthSession.fromJson(json as Map<String, dynamic>),
        failureMessage: 'Sign-in failed',
      );
}
