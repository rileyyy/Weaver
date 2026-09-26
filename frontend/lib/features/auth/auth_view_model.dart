import 'dart:async';

import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/auth/data/auth_repository.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/features/auth/models/auth_session.dart';
import 'package:weaver/features/auth/models/auth_user.dart';

@injectable
class AuthViewModel extends ViewModel {
  AuthViewModel(this._repository, this._sessionStore) {
    _sessionStore.addListener(notifyIfActive);
  }

  final AuthRepository _repository;
  final AuthSessionStore _sessionStore;

  /// True once startup session restoration (see [restoreSession]) has
  /// finished — the view uses this to show a splash state instead of
  /// flashing the login screen before a cached session has had a chance to
  /// restore.
  bool _hasRestored = false;
  bool get hasRestored => _hasRestored;

  bool isSubmitting = false;
  String? errorMessage;

  bool get isAuthenticated => _sessionStore.isAuthenticated;
  AuthUser? get currentUser => _sessionStore.current?.user;

  @override
  void dispose() {
    _sessionStore.removeListener(notifyIfActive);
    super.dispose();
  }

  Future<void> restoreSession() async {
    await _sessionStore.ensureValidSession();
    _hasRestored = true;
    notifyIfActive();
  }

  Future<void> login(String username, String password) => _submit(
    () => _repository.login(username, password),
    errorMessage: 'Could not sign in. Check your username and password.',
  );

  Future<void> register(String username, String password) => _submit(
    () => _repository.register(username, password),
    errorMessage: 'Could not create an account. Try a different username.',
  );

  Future<void> logout() async {
    final refreshToken = _sessionStore.current?.refreshToken;
    await _sessionStore.clear();
    if (refreshToken != null) unawaited(_revokeBestEffort(refreshToken));
  }

  /// The session is already cleared client-side, so a failed server-side
  /// revoke only means the refresh token lives until it expires; it must
  /// not surface as an unhandled async error.
  Future<void> _revokeBestEffort(String refreshToken) async {
    try {
      await _repository.logout(refreshToken);
    } on Exception {
      // See doc comment.
    }
  }

  Future<void> _submit(
    Future<AuthSession> Function() action, {
    required String errorMessage,
  }) async {
    isSubmitting = true;
    this.errorMessage = null;
    notifyIfActive();

    try {
      final session = await action();
      await _sessionStore.setSession(session);
    } on ApiException {
      this.errorMessage = errorMessage;
    } finally {
      isSubmitting = false;
      notifyIfActive();
    }
  }
}
