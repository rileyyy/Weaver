import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:weaver/features/auth/data/auth_http_client.dart';
import 'package:weaver/features/auth/data/auth_repository.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/features/auth/data/secure_token_store.dart';
import 'package:weaver/features/auth/models/auth_session.dart';
import 'package:weaver/shared/models/user.dart';

class _UnusedAuthRepository implements AuthRepository {
  @override
  Future<AuthSession> login(String username, String password) =>
      throw UnimplementedError();

  @override
  Future<AuthSession> register(String username, String password) =>
      throw UnimplementedError();

  @override
  Future<AuthSession> refresh(String refreshToken) =>
      throw UnimplementedError();

  @override
  Future<void> logout(String refreshToken) => throw UnimplementedError();
}

class _InMemoryTokenStore implements SecureTokenStore {
  String? token;

  @override
  Future<String?> readRefreshToken() async => token;

  @override
  Future<void> writeRefreshToken(String value) async => token = value;

  @override
  Future<void> clear() async => token = null;
}

AuthSession _session(String accessToken) => AuthSession(
  accessToken: accessToken,
  accessTokenExpiresAtUtc: DateTime.now().toUtc().add(const Duration(hours: 1)),
  refreshToken: 'a-refresh-token',
  user: const User(id: 'user-1', username: 'alice', kind: UserKind.human),
);

void main() {
  test('attaches the current access token as a bearer header', () async {
    http.BaseRequest? sentRequest;
    final inner = MockClient((request) async {
      sentRequest = request;
      return http.Response('ok', 200);
    });
    final sessionStore = AuthSessionStore(
      _UnusedAuthRepository(),
      _InMemoryTokenStore(),
    );
    await sessionStore.setSession(_session('the-access-token'));
    final client = AuthHttpClient(inner, sessionStore);

    await client.get(Uri.parse('http://backend.test/api/statuses'));

    expect(sentRequest!.headers['Authorization'], 'Bearer the-access-token');
  });

  test('sends no Authorization header when there is no session', () async {
    http.BaseRequest? sentRequest;
    final inner = MockClient((request) async {
      sentRequest = request;
      return http.Response('ok', 200);
    });
    final sessionStore = AuthSessionStore(
      _UnusedAuthRepository(),
      _InMemoryTokenStore(),
    );
    final client = AuthHttpClient(inner, sessionStore);

    await client.get(Uri.parse('http://backend.test/api/statuses'));

    expect(sentRequest!.headers.containsKey('Authorization'), isFalse);
  });

  test('a 401 response clears the session', () async {
    final inner = MockClient((request) async => http.Response('', 401));
    final sessionStore = AuthSessionStore(
      _UnusedAuthRepository(),
      _InMemoryTokenStore(),
    );
    await sessionStore.setSession(_session('an-expired-or-revoked-token'));
    final client = AuthHttpClient(inner, sessionStore);

    await client.get(Uri.parse('http://backend.test/api/statuses'));

    expect(sessionStore.isAuthenticated, isFalse);
  });

  test('a 200 response leaves the session untouched', () async {
    final inner = MockClient((request) async => http.Response('ok', 200));
    final sessionStore = AuthSessionStore(
      _UnusedAuthRepository(),
      _InMemoryTokenStore(),
    );
    await sessionStore.setSession(_session('a-valid-token'));
    final client = AuthHttpClient(inner, sessionStore);

    await client.get(Uri.parse('http://backend.test/api/statuses'));

    expect(sessionStore.isAuthenticated, isTrue);
  });

  test(
    'a 401 for a token that has since been replaced does not clear the newer session',
    () async {
      final sessionStore = AuthSessionStore(
        _UnusedAuthRepository(),
        _InMemoryTokenStore(),
      );
      await sessionStore.setSession(_session('old-token'));
      final inner = MockClient((request) async {
        // A parallel request refreshed the session while this one was in flight.
        await sessionStore.setSession(_session('new-token'));
        return http.Response('', 401);
      });
      final client = AuthHttpClient(inner, sessionStore);

      await client.get(Uri.parse('http://backend.test/api/statuses'));

      expect(sessionStore.current!.accessToken, 'new-token');
    },
  );
}
