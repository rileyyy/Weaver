import 'package:http/http.dart' as http;
import 'package:weaver/features/auth/data/auth_session_store.dart';

/// Wraps a plain [http.Client], attaching the current access token (proactively
/// refreshed if expired) to every request. This is what every API repository
/// other than [ApiAuthRepository] is injected with — see `NetworkModule` —
/// so none of them need to know auth exists.
///
/// A definitive 401 (the token was valid at send time but the server
/// rejected it anyway — e.g. revoked mid-flight) clears the session rather
/// than retrying — but only if the rejected token is still the current one,
/// since a parallel request may already have refreshed it. The failed call still surfaces as an [ApiException] to its
/// caller, and the app's top-level auth gate reacts to the now-signed-out
/// session by returning to the login screen. A full "rebuild and replay the
/// original request" retry was considered and skipped as more complexity
/// than this app's request patterns (small, single-shot JSON calls) justify.
class AuthHttpClient extends http.BaseClient {
  AuthHttpClient(this._inner, this._sessionStore);

  final http.Client _inner;
  final AuthSessionStore _sessionStore;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    await _sessionStore.ensureValidSession();
    final token = _sessionStore.current?.accessToken;
    if (token != null) {
      request.headers['Authorization'] = 'Bearer $token';
    }

    final response = await _inner.send(request);
    if (response.statusCode == 401 && _sessionStore.current?.accessToken == token) {
      await _sessionStore.clear();
    }
    return response;
  }
}
