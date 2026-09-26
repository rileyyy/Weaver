import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/shared/models/work_item_status.dart';

/// Everything a [BoardView] needs to render, as returned by a
/// [BoardRepository].
class BoardData {
  const BoardData({required this.statuses, required this.swimlanes});

  final List<WorkItemStatus> statuses;
  final List<Swimlane> swimlanes;
}
