import 'dart:async';

import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/network/network_exception.dart';
import 'package:weaver/core/network/server_origin.dart';
import 'package:weaver/core/network/server_settings.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/auth/data/auth_repository.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/features/auth/models/auth_session.dart';
import 'package:weaver/shared/models/user.dart';

@injectable
class AuthViewModel extends ViewModel {
  AuthViewModel(this._repository, this._sessionStore, this._serverSettings) {
    _sessionStore.addListener(notifyIfActive);
  }

  static const invalidServerMessage =
      'Enter the server address, e.g. https://weaver.example.ts.net.';

  final AuthRepository _repository;
  final AuthSessionStore _sessionStore;
  final ServerSettings _serverSettings;

  /// True once startup session restoration (see [restoreSession]) has
  /// finished — the view uses this to show a splash state instead of
  /// flashing the login screen before a cached session has had a chance to
  /// restore.
  bool _hasRestored = false;
  bool get hasRestored => _hasRestored;

  bool isSubmitting = false;
  String? errorMessage;

  bool get isAuthenticated => _sessionStore.isAuthenticated;
  User? get currentUser => _sessionStore.current?.user;

  /// Whether the sign-in screen asks which server to use (native builds).
  bool get canChangeServer => _serverSettings.canChangeServer;

  /// The server to prefill the address field with, or empty if none yet.
  String get serverOrigin => _serverSettings.origin ?? '';

  @override
  void dispose() {
    _sessionStore.removeListener(notifyIfActive);
    super.dispose();
  }

  Future<void> restoreSession() async {
    await _serverSettings.load();
    // A cached refresh token is no use without a server to send it to.
    if (_serverSettings.isConfigured) await _sessionStore.ensureValidSession();
    _hasRestored = true;
    notifyIfActive();
  }

  /// [serverAddress] is what was typed in the address field; it's ignored
  /// where [canChangeServer] is false.
  Future<void> login(
    String username,
    String password, {
    String serverAddress = '',
  }) => _submit(
    () => _repository.login(username, password),
    serverAddress: serverAddress,
    errorMessage: 'Could not sign in. Check your username and password.',
  );

  /// See [login] for [serverAddress].
  Future<void> register(
    String username,
    String password, {
    String serverAddress = '',
  }) => _submit(
    () => _repository.register(username, password),
    serverAddress: serverAddress,
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
    required String serverAddress,
    required String errorMessage,
  }) async {
    if (!_selectServer(serverAddress)) {
      this.errorMessage = invalidServerMessage;
      notifyIfActive();
      return;
    }

    isSubmitting = true;
    this.errorMessage = null;
    notifyIfActive();

    try {
      final session = await action();
      // Remembered before the session is stored, so a refresh token saved
      // for the next launch always has its server saved alongside it.
      await _serverSettings.remember();
      await _sessionStore.setSession(session);
    } on ApiException catch (e) {
      // A 429 carries the server's "wait and try again" message, and an
      // unreachable server most likely means a mistyped address. Either is
      // something the user can act on; the generic message would send them
      // hunting for a typo in their password instead.
      this.errorMessage = e.isTooManyRequests || e is NetworkException
          ? e.message
          : errorMessage;
    } finally {
      isSubmitting = false;
      notifyIfActive();
    }
  }

  /// Points requests at the typed server, returning false if it isn't a
  /// usable address. Always true where the server can't be changed.
  bool _selectServer(String address) {
    if (!_serverSettings.canChangeServer) return true;

    final origin = normalizeServerOrigin(address);
    if (origin == null) return false;
    _serverSettings.use(origin);
    return true;
  }
}
