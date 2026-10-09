import 'package:flutter/foundation.dart';
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_config.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/server_origin.dart';
import 'package:weaver/core/network/server_origin_store.dart';

/// Which server the app talks to.
///
/// Web builds always use the origin they were served from, because the
/// nginx image proxies `/api` to the backend. Native builds have no such
/// origin, so the sign-in screen asks for one and it is remembered for the
/// next launch. An `API_BASE_URL` given at build time is the starting value
/// on both.
@lazySingleton
class ServerSettings implements ApiEndpoint {
  ServerSettings(
    this._store, {
    @ignoreParam String? buildTimeOrigin,
    @ignoreParam bool? isWeb,
  }) : canChangeServer = !(isWeb ?? kIsWeb),
       _origin = normalizeServerOrigin(
         buildTimeOrigin ?? ApiConfig.buildTimeOrigin,
       );

  final ServerOriginStore _store;

  /// False on web, where the server is wherever the app was loaded from.
  final bool canChangeServer;

  String? _origin;

  /// The chosen server, e.g. `https://weaver.example.ts.net`. Null until one
  /// is chosen, and on web when no build-time origin was given.
  String? get origin => _origin;

  /// Whether requests have somewhere to go.
  bool get isConfigured => _origin != null || !canChangeServer;

  /// Throws a [StateError] on a native build with no server chosen yet:
  /// nothing should send a request before sign-in has picked one.
  @override
  String get baseUrl {
    final origin = _origin;
    if (origin != null) return '$origin/api';
    if (!canChangeServer) return '/api';
    throw StateError('No server has been chosen yet.');
  }

  /// Restores the server remembered by [remember], if there is one.
  Future<void> load() async {
    if (!canChangeServer) return;
    final saved = await _store.readOrigin();
    if (saved != null) _origin = saved;
  }

  /// Sends requests to [origin] (already normalized) from now on, without
  /// remembering it across launches.
  void use(String origin) => _origin = origin;

  /// Remembers the current server for the next launch. Best-effort, like
  /// the refresh token: failing only means being asked again.
  Future<void> remember() async {
    final origin = _origin;
    if (!canChangeServer || origin == null) return;
    try {
      await _store.writeOrigin(origin);
    } on Exception {
      // See doc comment.
    }
  }
}
