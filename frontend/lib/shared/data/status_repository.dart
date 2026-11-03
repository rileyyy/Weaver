import 'package:weaver/shared/models/work_item_status.dart';

/// Each Space's work-item statuses, shared by every feature.
abstract class StatusRepository {
  /// The statuses of the Space [spaceId], in board order.
  Future<List<WorkItemStatus>> loadStatuses(String spaceId);

  /// Drops every cached list, so the next [loadStatuses] fetches again.
  /// Called after an admin changes the statuses.
  void invalidate();
}
