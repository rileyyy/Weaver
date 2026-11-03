import 'package:weaver/shared/models/work_item.dart';

/// One swimlane as loaded: its own work item and its direct children, in
/// rank order. The board stores the items and keeps only the layout.
class SwimlaneItems {
  const SwimlaneItems({required this.lane, required this.cards});

  final WorkItem lane;
  final List<WorkItem> cards;
}
