/// How a View first draws its items, mirroring the server's `ViewLayout`.
enum ViewLayout {
  board('Board'),
  hierarchy('Hierarchy'),
  roadmap('Roadmap');

  const ViewLayout(this.wireName);

  final String wireName;

  /// A layout this client doesn't know yet (Agenda, before ticket 24) opens
  /// as the Board.
  static ViewLayout fromWire(String? name) {
    for (final layout in values) {
      if (layout.wireName == name) return layout;
    }
    return board;
  }
}
