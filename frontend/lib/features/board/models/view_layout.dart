import 'package:weaver/features/board/views/board_tab.dart';

/// How a View first draws its items, mirroring the server's `ViewLayout`.
enum ViewLayout {
  board('Board', BoardTab.swimLanes),
  hierarchy('Hierarchy', BoardTab.hierarchy),
  roadmap('Roadmap', BoardTab.roadmap);

  const ViewLayout(this.wireName, this.tab);

  final String wireName;

  /// The tab that shows this layout.
  final BoardTab tab;

  /// The layout [tab] shows, or the Board for a tab that isn't a layout
  /// (Backlog, Repeating, Trash).
  static ViewLayout ofTab(BoardTab tab) {
    for (final layout in values) {
      if (layout.tab == tab) return layout;
    }
    return board;
  }

  /// A layout this client doesn't know yet (Agenda, before ticket 24) opens
  /// as the Board.
  static ViewLayout fromWire(String? name) {
    for (final layout in values) {
      if (layout.wireName == name) return layout;
    }
    return board;
  }
}
