import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/server_origin_store.dart';

/// The address isn't secret; it lives in secure storage only because that
/// plugin is already how the refresh token survives a restart, and a second
/// storage plugin isn't worth it for one value.
@LazySingleton(as: ServerOriginStore)
class FlutterSecureServerOriginStore implements ServerOriginStore {
  static const _originKey = 'server_origin';

  final FlutterSecureStorage _storage = const FlutterSecureStorage();

  @override
  Future<String?> readOrigin() async {
    try {
      return await _storage.read(key: _originKey);
    } on Exception {
      // Unreadable storage only means asking for the server again.
      return null;
    }
  }

  @override
  Future<void> writeOrigin(String origin) =>
      _storage.write(key: _originKey, value: origin);
}
