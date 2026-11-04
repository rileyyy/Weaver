import 'package:weaver/features/board/models/trashed_work_item.dart';

/// Deleted work items, waiting in the Trash to be restored (ADR-0007).
abstract class TrashRepository {
  /// Each delete, newest first.
  Future<List<TrashedWorkItem>> list();

  /// Brings the item back with everything deleted with it, under its nearest
  /// ancestor that still exists, or at the top of its Space.
  Future<void> restore(String workItemId);
}
