import 'package:weaver/features/board/data/last_space_store.dart';

/// A [LastSpaceStore] that remembers in memory, starting from [spaceId].
class MemoryLastSpaceStore implements LastSpaceStore {
  MemoryLastSpaceStore([this.spaceId]);

  String? spaceId;

  @override
  Future<String?> readSpaceId() async => spaceId;

  @override
  Future<void> writeSpaceId(String spaceId) async => this.spaceId = spaceId;
}
