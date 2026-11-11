import 'package:weaver/features/board/models/view_layout.dart';

/// The board's alternate views, selected via the tabs in the top bar, in
/// tab order.
enum BoardTab {
  swimLanes('Swim Lanes'),
  agenda('Agenda'),
  roadmap('Roadmap'),
  backlog('Backlog'),
  hierarchy('Hierarchy'),
  repeating('Repeating'),
  trash('Trash');

  const BoardTab(this.label);

  final String label;

  /// The tab that shows [layout].
  static BoardTab showing(ViewLayout layout) => switch (layout) {
    ViewLayout.board => swimLanes,
    ViewLayout.hierarchy => hierarchy,
    ViewLayout.roadmap => roadmap,
    ViewLayout.agenda => agenda,
  };

  /// The layout this tab shows, or the Board for a tab that isn't a
  /// layout (Backlog, Repeating, Trash), so a View saved there opens on
  /// the Board.
  ViewLayout get layout => switch (this) {
    roadmap => ViewLayout.roadmap,
    hierarchy => ViewLayout.hierarchy,
    agenda => ViewLayout.agenda,
    _ => ViewLayout.board,
  };
}
