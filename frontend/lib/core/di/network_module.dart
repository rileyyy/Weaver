import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_config.dart';
import 'package:weaver/features/auth/data/auth_http_client.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/shared/data/current_user.dart';

@module
abstract class NetworkModule {
  /// The unauthenticated client — used only by [ApiAuthRepository], which
  /// must never go through [AuthHttpClient] (see its own doc comment).
  @Named('rawHttpClient')
  @lazySingleton
  http.Client get rawHttpClient => http.Client();

  /// The default `http.Client` binding: every other repository asks for
  /// plain `http.Client` and gets this token-attaching wrapper for free,
  /// with no auth-specific code of its own.
  @lazySingleton
  http.Client httpClient(
    @Named('rawHttpClient') http.Client raw,
    AuthSessionStore sessionStore,
  ) => AuthHttpClient(raw, sessionStore);

  @Named('apiBaseUrl')
  String get apiBaseUrl => ApiConfig.baseUrl;

  CurrentUser currentUser(AuthSessionStore sessionStore) => sessionStore;
}
