import 'package:flutter/foundation.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/work_item_status.dart';

/// One Space's part of the board: its own visible status columns and its
/// lanes. A board showing several Spaces stacks one section per Space,
/// since each Space has its own statuses.
class BoardSection {
  const BoardSection({
    required this.space,
    required this.statuses,
    required this.lanes,
  });

  final Space space;
  final List<WorkItemStatus> statuses;
  final List<ValueListenable<Swimlane>> lanes;
}
