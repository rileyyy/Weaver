import 'package:weaver/shared/models/work_item.dart';

/// What a work-item picker was closed with, so that picking "none" (a null
/// [item]) can be told apart from cancelling (no pick at all).
class WorkItemPick {
  const WorkItemPick(this.item);

  final WorkItem? item;
}
