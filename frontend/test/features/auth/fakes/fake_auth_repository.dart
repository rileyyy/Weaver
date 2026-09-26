import 'dart:async';

import 'package:weaver/features/auth/data/auth_repository.dart';
import 'package:weaver/features/auth/models/auth_session.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/user_kind.dart';

AuthSession fakeSession(String username) => AuthSession(
  accessToken: 'access-for-$username',
  accessTokenExpiresAtUtc: DateTime.now().toUtc().add(const Duration(hours: 1)),
  refreshToken: 'refresh-for-$username',
  user: User(id: 'id-$username', username: username, kind: UserKind.human),
);

class FakeAuthRepository implements AuthRepository {
  FakeAuthRepository({this.loginError, this.registerError, this.logoutError});

  final Exception? loginError;
  final Exception? registerError;
  final Exception? logoutError;
  final List<String> loginCalls = [];
  final List<String> registerCalls = [];

  /// When set, [login] waits on it, so a test can observe the in-flight
  /// state.
  Completer<void>? loginGate;

  @override
  Future<AuthSession> login(String username, String password) async {
    loginCalls.add(username);
    await loginGate?.future;
    final error = loginError;
    if (error != null) throw error;
    return fakeSession(username);
  }

  @override
  Future<AuthSession> register(String username, String password) async {
    registerCalls.add(username);
    final error = registerError;
    if (error != null) throw error;
    return fakeSession(username);
  }

  @override
  Future<AuthSession> refresh(String refreshToken) async =>
      throw UnimplementedError();

  @override
  Future<void> logout(String refreshToken) async {
    final error = logoutError;
    if (error != null) throw error;
  }
}
