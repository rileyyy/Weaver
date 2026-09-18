import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/swimlane.dart';

/// Everything a [BoardView] needs to render, as returned by a
/// [BoardRepository].
class BoardData {
  const BoardData({required this.statuses, required this.swimlanes});

  final List<BoardStatus> statuses;
  final List<Swimlane> swimlanes;
}
