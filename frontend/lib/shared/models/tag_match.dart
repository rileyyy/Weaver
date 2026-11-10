/// How a query with several tags combines them, mirroring the server's
/// `TagMatch`.
enum TagMatch {
  /// An item with at least one of the tags matches.
  any('Any'),

  /// Only an item with every one of the tags matches.
  all('All');

  const TagMatch(this.wireName);

  final String wireName;

  static TagMatch fromWire(String? name) => name == all.wireName ? all : any;
}
