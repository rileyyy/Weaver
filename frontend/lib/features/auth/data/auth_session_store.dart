import 'package:flutter/foundation.dart';
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/auth/data/auth_repository.dart';
import 'package:weaver/features/auth/data/secure_token_store.dart';
import 'package:weaver/features/auth/models/auth_session.dart';

/// Single source of truth for "is anyone signed in, and with what token" —
/// shared by [AuthViewModel] (view-facing login/logout state) and
/// [AuthHttpClient] (attaches/refreshes the access token on every API
/// call), so both react to the same session rather than drifting apart.
///
/// A [ChangeNotifier] rather than a [ViewModel] on purpose: it's a plain
/// data-layer store with no view lifecycle of its own (it's a lazy
/// singleton for the app's lifetime, never disposed), reused by both a
/// ViewModel and a non-widget HTTP client.
@lazySingleton
class AuthSessionStore extends ChangeNotifier {
  AuthSessionStore(this._authRepository, this._tokenStore);

  final AuthRepository _authRepository;
  final SecureTokenStore _tokenStore;

  AuthSession? _session;
  AuthSession? get current => _session;

  Future<bool>? _refreshInFlight;

  bool get isAuthenticated => _session != null;

  Future<void> setSession(AuthSession session) async {
    _session = session;
    notifyListeners();
    await _persistBestEffort(
      () => _tokenStore.writeRefreshToken(session.refreshToken),
    );
  }

  Future<void> clear() async {
    final hadSession = _session != null;
    _session = null;
    if (hadSession) notifyListeners();
    await _persistBestEffort(_tokenStore.clear);
  }

  /// The in-memory session (and the listener notification above) must never
  /// be held hostage by the underlying storage — e.g. a browser blocking
  /// IndexedDB in a private window, or (as happened once during development)
  /// a plugin that failed to register at all. Losing persistence only means
  /// the session won't survive an app restart, not that this one breaks.
  Future<void> _persistBestEffort(Future<void> Function() action) async {
    try {
      await action();
    } on Exception {
      // Best-effort — see doc comment above.
    }
  }

  /// Ensures [current] has a non-expired access token, refreshing from
  /// either the in-memory or the cached refresh token if needed (the
  /// latter is what restores a session after an app restart). Returns
  /// false if there's no usable session: none cached, or the refresh
  /// failed. The session is cleared only if the server rejected the
  /// refresh token (401).
  ///
  /// Concurrent callers share one refresh. The backend rotates the refresh
  /// token on every use, so a second refresh with the same token is
  /// rejected; before this, parallel requests (a board load, a detail
  /// open) each refreshed, all but one failed, and the failures cleared
  /// the session the winner had just stored.
  Future<bool> ensureValidSession() {
    final session = _session;
    if (session != null && !session.isAccessTokenExpired) {
      return Future.value(true);
    }

    return _refreshInFlight ??= _refresh().whenComplete(
      () => _refreshInFlight = null,
    );
  }

  Future<bool> _refresh() async {
    final startedFrom = _session;
    final refreshToken =
        startedFrom?.refreshToken ?? await _tokenStore.readRefreshToken();
    if (refreshToken == null) return false;

    try {
      await setSession(await _authRepository.refresh(refreshToken));
      return true;
    } on ApiException catch (e) {
      // A login may have replaced the session while this refresh was in
      // flight; that newer session isn't this failure's to discard.
      if (!identical(_session, startedFrom)) return isAuthenticated;
      // Only the server rejecting the refresh token ends the session; a
      // network failure keeps it so a later request can refresh again.
      if (e.isUnauthorized) await clear();
      return false;
    }
  }
}
