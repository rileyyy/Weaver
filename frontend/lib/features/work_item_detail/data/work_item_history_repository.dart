import 'package:weaver/features/work_item_detail/models/work_item_history_entry.dart';

abstract class WorkItemHistoryRepository {
  /// Every change to the item, newest first.
  Future<List<WorkItemHistoryEntry>> loadHistory(String workItemId);
}
