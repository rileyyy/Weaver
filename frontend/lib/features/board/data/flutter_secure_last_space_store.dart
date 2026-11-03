import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:injectable/injectable.dart';
import 'package:weaver/features/board/data/last_space_store.dart';

/// The id isn't secret; it lives in secure storage for the same reason the
/// server address does (`FlutterSecureServerOriginStore`): that plugin is
/// already there, and a second storage plugin isn't worth it for one value.
@LazySingleton(as: LastSpaceStore)
class FlutterSecureLastSpaceStore implements LastSpaceStore {
  static const _spaceIdKey = 'last_space_id';

  final FlutterSecureStorage _storage = const FlutterSecureStorage();

  @override
  Future<String?> readSpaceId() async {
    try {
      return await _storage.read(key: _spaceIdKey);
    } on Exception {
      // Unreadable storage only means opening the first Space instead.
      return null;
    }
  }

  @override
  Future<void> writeSpaceId(String spaceId) async {
    try {
      await _storage.write(key: _spaceIdKey, value: spaceId);
    } on Exception {
      // Forgetting the choice is harmless; failing the switch over it isn't.
    }
  }
}
