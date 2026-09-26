import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/auth/auth_view_model.dart';
import 'package:weaver/features/auth/data/auth_repository.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/features/auth/data/secure_token_store.dart';
import 'package:weaver/features/auth/models/auth_session.dart';
import 'package:weaver/features/auth/models/auth_user.dart';

class _FakeAuthRepository implements AuthRepository {
  _FakeAuthRepository({this.loginError, this.registerError});

  final Exception? loginError;
  final Exception? registerError;
  final List<String> loginCalls = [];

  @override
  Future<AuthSession> login(String username, String password) async {
    loginCalls.add(username);
    final error = loginError;
    if (error != null) throw error;
    return _session(username);
  }

  @override
  Future<AuthSession> register(String username, String password) async {
    final error = registerError;
    if (error != null) throw error;
    return _session(username);
  }

  @override
  Future<AuthSession> refresh(String refreshToken) async =>
      throw UnimplementedError();

  @override
  Future<void> logout(String refreshToken) async {}
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

AuthSession _session(String username) => AuthSession(
  accessToken: 'access-for-$username',
  accessTokenExpiresAtUtc: DateTime.now().toUtc().add(const Duration(hours: 1)),
  refreshToken: 'refresh-for-$username',
  user: AuthUser(id: 'id-$username', username: username, kind: UserKind.human),
);

void main() {
  late _FakeAuthRepository repository;
  late AuthSessionStore sessionStore;
  late AuthViewModel viewModel;

  setUp(() {
    repository = _FakeAuthRepository();
    sessionStore = AuthSessionStore(repository, _InMemoryTokenStore());
    viewModel = AuthViewModel(repository, sessionStore);
  });

  test('is not authenticated before any login', () {
    expect(viewModel.isAuthenticated, isFalse);
    expect(viewModel.currentUser, isNull);
  });

  test('login on success sets the session and clears any error', () async {
    await viewModel.login('alice', 'a valid password');

    expect(viewModel.isAuthenticated, isTrue);
    expect(viewModel.currentUser!.username, 'alice');
    expect(viewModel.errorMessage, isNull);
    expect(viewModel.isSubmitting, isFalse);
  });

  test(
    'login on failure sets an error message and stays unauthenticated',
    () async {
      viewModel = AuthViewModel(
        _FakeAuthRepository(loginError: Exception('bad creds')),
        sessionStore,
      );

      await viewModel.login('alice', 'wrong password');

      expect(viewModel.isAuthenticated, isFalse);
      expect(viewModel.errorMessage, isNotNull);
    },
  );

  test('register on success authenticates the same as login', () async {
    await viewModel.register('newuser', 'a valid password');

    expect(viewModel.isAuthenticated, isTrue);
    expect(viewModel.currentUser!.username, 'newuser');
  });

  test('register on failure sets an error message', () async {
    viewModel = AuthViewModel(
      _FakeAuthRepository(registerError: Exception('taken')),
      sessionStore,
    );

    await viewModel.register('alice', 'a valid password');

    expect(viewModel.isAuthenticated, isFalse);
    expect(viewModel.errorMessage, isNotNull);
  });

  test('logout clears the session', () async {
    await viewModel.login('alice', 'a valid password');

    await viewModel.logout();

    expect(viewModel.isAuthenticated, isFalse);
    expect(viewModel.currentUser, isNull);
  });

  test('restoreSession sets hasRestored regardless of outcome', () async {
    expect(viewModel.hasRestored, isFalse);

    await viewModel.restoreSession();

    expect(viewModel.hasRestored, isTrue);
    expect(viewModel.isAuthenticated, isFalse);
  });

  test(
    'reacts to session changes made outside of its own login/register/logout',
    () async {
      var notified = false;
      viewModel.addListener(() => notified = true);

      await sessionStore.setSession(_session('external'));

      expect(notified, isTrue);
      expect(viewModel.isAuthenticated, isTrue);
    },
  );
}
