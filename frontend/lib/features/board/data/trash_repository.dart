import 'package:weaver/features/board/models/trashed_work_item.dart';

/// Deleted work items, waiting in the Trash to be restored or purged
/// (ADR-0007).
abstract class TrashRepository {
  /// Each delete, newest first.
  Future<List<TrashedWorkItem>> list();

  /// Brings the item back with everything deleted with it, under its nearest
  /// ancestor that still exists, or at the top of its Space.
  Future<void> restore(String workItemId);

  /// Permanently deletes the item and everything deleted with it. Refused
  /// (400) for an item that went to the Trash with an ancestor.
  Future<void> purge(String workItemId);

  /// Permanently deletes everything in the Trash, in every Space.
  Future<void> empty();
}
