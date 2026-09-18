import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/auth/data/auth_repository.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/features/auth/data/secure_token_store.dart';
import 'package:weaver/features/auth/models/auth_session.dart';
import 'package:weaver/features/auth/models/auth_user.dart';

class _FakeAuthRepository implements AuthRepository {
  _FakeAuthRepository({this.refreshError});

  final Exception? refreshError;
  final List<String> refreshCalls = [];

  @override
  Future<AuthSession> login(String username, String password) => throw UnimplementedError();

  @override
  Future<AuthSession> register(String username, String password) => throw UnimplementedError();

  @override
  Future<AuthSession> refresh(String refreshToken) async {
    refreshCalls.add(refreshToken);
    final error = refreshError;
    if (error != null) throw error;
    return _session('refreshed-access', 'refreshed-refresh');
  }

  @override
  Future<void> logout(String refreshToken) => throw UnimplementedError();
}

class _ThrowingTokenStore implements SecureTokenStore {
  @override
  Future<String?> readRefreshToken() async => null;

  @override
  Future<void> writeRefreshToken(String value) async =>
      throw Exception('MissingPluginException (simulated)');

  @override
  Future<void> clear() async => throw Exception('MissingPluginException (simulated)');
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

AuthSession _session(
  String accessToken,
  String refreshToken, {
  DateTime? expiresAtUtc,
}) => AuthSession(
  accessToken: accessToken,
  accessTokenExpiresAtUtc: expiresAtUtc ?? DateTime.now().toUtc().add(const Duration(hours: 1)),
  refreshToken: refreshToken,
  user: const AuthUser(id: 'user-1', username: 'alice', kind: UserKind.human),
);

void main() {
  late _FakeAuthRepository repository;
  late _InMemoryTokenStore tokenStore;
  late AuthSessionStore store;

  setUp(() {
    repository = _FakeAuthRepository();
    tokenStore = _InMemoryTokenStore();
    store = AuthSessionStore(repository, tokenStore);
  });

  test('ensureValidSession with a non-expired session does not call refresh', () async {
    await store.setSession(_session('access', 'refresh'));

    final result = await store.ensureValidSession();

    expect(result, isTrue);
    expect(repository.refreshCalls, isEmpty);
  });

  test('ensureValidSession with an expired session refreshes it', () async {
    await store.setSession(
      _session('access', 'refresh', expiresAtUtc: DateTime.now().toUtc().subtract(const Duration(minutes: 1))),
    );

    final result = await store.ensureValidSession();

    expect(result, isTrue);
    expect(store.current!.accessToken, 'refreshed-access');
    expect(repository.refreshCalls, ['refresh']);
  });

  test('ensureValidSession with no session but a cached refresh token restores one', () async {
    tokenStore.token = 'cached-refresh';

    final result = await store.ensureValidSession();

    expect(result, isTrue);
    expect(store.current!.accessToken, 'refreshed-access');
  });

  test('ensureValidSession with nothing cached returns false', () async {
    final result = await store.ensureValidSession();

    expect(result, isFalse);
    expect(store.isAuthenticated, isFalse);
  });

  test('ensureValidSession clears the session when the refresh call fails', () async {
    store = AuthSessionStore(_FakeAuthRepository(refreshError: Exception('expired')), tokenStore);
    await store.setSession(
      _session('access', 'refresh', expiresAtUtc: DateTime.now().toUtc().subtract(const Duration(minutes: 1))),
    );

    final result = await store.ensureValidSession();

    expect(result, isFalse);
    expect(store.isAuthenticated, isFalse);
    expect(tokenStore.token, isNull);
  });

  test('setSession persists the refresh token to the token store', () async {
    await store.setSession(_session('access', 'a-refresh-token'));

    expect(tokenStore.token, 'a-refresh-token');
  });

  test('clear removes the session and the persisted refresh token', () async {
    await store.setSession(_session('access', 'refresh'));

    await store.clear();

    expect(store.isAuthenticated, isFalse);
    expect(tokenStore.token, isNull);
  });

  test('setSession still updates in-memory state and notifies when persistence throws', () async {
    final failingTokenStore = _ThrowingTokenStore();
    store = AuthSessionStore(repository, failingTokenStore);
    var notified = false;
    store.addListener(() => notified = true);

    await store.setSession(_session('access', 'refresh'));

    expect(store.isAuthenticated, isTrue);
    expect(notified, isTrue);
  });

  test('clear still updates in-memory state and notifies when persistence throws', () async {
    final failingTokenStore = _ThrowingTokenStore();
    store = AuthSessionStore(repository, failingTokenStore);
    await store.setSession(_session('access', 'refresh'));
    var notified = false;
    store.addListener(() => notified = true);

    await store.clear();

    expect(store.isAuthenticated, isFalse);
    expect(notified, isTrue);
  });

  test('setSession notifies listeners', () async {
    var notified = false;
    store.addListener(() => notified = true);

    await store.setSession(_session('access', 'refresh'));

    expect(notified, isTrue);
  });
}
