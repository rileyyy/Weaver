import 'package:weaver/shared/recurrence/models/recurrence_draft.dart';
import 'package:weaver/shared/recurrence/models/work_item_recurrence.dart';

/// Repeat schedules, shared by the detail dialog's Repeat section and the
/// board's Repeating tab.
abstract class RecurrenceRepository {
  /// Null when the work item doesn't repeat.
  Future<WorkItemRecurrence?> get(String workItemId);

  Future<List<WorkItemRecurrence>> list();

  /// Creates or replaces the schedule. The server immediately creates any
  /// occurrence due within the next week, so the board may have new items.
  Future<WorkItemRecurrence> save(String workItemId, RecurrenceDraft draft);

  /// Stops the item repeating; items it already created are kept.
  Future<void> remove(String workItemId);
}
