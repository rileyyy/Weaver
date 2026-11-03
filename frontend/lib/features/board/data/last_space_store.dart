/// Remembers, on this device only, which Space the board last showed, so
/// the next launch opens it again.
abstract class LastSpaceStore {
  /// Null when nothing is remembered or the store can't be read.
  Future<String?> readSpaceId();

  Future<void> writeSpaceId(String spaceId);
}
