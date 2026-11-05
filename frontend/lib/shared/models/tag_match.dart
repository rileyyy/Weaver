/// How a query with several tags combines them, mirroring the server's
/// `TagMatch`.
enum TagMatch {
  /// An item with at least one of the tags matches.
  any,

  /// Only an item with every one of the tags matches.
  all,
}
