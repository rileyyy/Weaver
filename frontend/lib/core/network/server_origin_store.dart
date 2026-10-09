/// Remembers the last server signed in to, so the next launch can restore
/// the session against it without asking again.
abstract class ServerOriginStore {
  Future<String?> readOrigin();

  Future<void> writeOrigin(String origin);
}
