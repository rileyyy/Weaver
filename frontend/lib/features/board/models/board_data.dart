import 'package:weaver/features/board/models/swimlane_items.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

/// Everything a [BoardView] needs to render, as returned by a
/// [BoardRepository].
class BoardData {
  const BoardData({
    required this.statuses,
    required this.swimlanes,
    this.types = const [],
  });

  final List<WorkItemStatus> statuses;
  final List<SwimlaneItems> swimlanes;

  /// Every type, so cards can be coloured by theirs.
  final List<WorkItemType> types;
}
