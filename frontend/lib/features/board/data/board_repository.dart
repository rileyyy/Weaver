import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item.dart';

/// Source of board data and the operations [BoardViewModel] can perform on
/// it. [BoardViewModel] depends on this interface, not a concrete source.
///
/// Every write takes the `expectedVersion` the change was based on and
/// returns the saved item, carrying its new version. A stale version fails
/// with a 409 [ApiException].
abstract class BoardRepository {
  /// The board's root scope: the first board's scope item, or null
  /// (top-level items) if no board exists yet. Resolved once; navigating
  /// the hierarchy from there only calls [loadBoard].
  Future<String?> loadRootScopeItemId();

  /// Swimlanes are the direct children of [scopeItemId] (or top-level items
  /// when null); each swimlane's cards are its own direct children.
  Future<BoardData> loadBoard(String? scopeItemId);

  /// Every work item in the system, flat and unscoped — used by the
  /// Hierarchy view to build a full parent/child tree client-side.
  Future<List<WorkItem>> loadAllItems();

  /// One work item as it is now, e.g. after a write was rejected as stale.
  Future<WorkItem> getItem(String id);

  /// Every active user, for resolving a work item's assignee to a
  /// display name/initial on the board.
  Future<List<User>> loadUsers();

  /// Moves the work item identified by [cardId] to [newStatusId], within
  /// whichever parent already owns it, ranked directly after [afterId] in
  /// that cell, or first when [afterId] is null. [newStatusId] may be the
  /// item's current status, which only reorders it.
  Future<WorkItem> changeStatus(
    String cardId,
    String newStatusId, {
    required String? afterId,
    required int expectedVersion,
  });

  /// Moves the work item identified by [itemId] to [newParentId], keeping
  /// its current status.
  Future<WorkItem> reparentItem(
    String itemId,
    String newParentId, {
    required int expectedVersion,
  });

  /// Sets the work item identified by [itemId]'s scheduled start/end.
  /// Either may be null.
  Future<WorkItem> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  });

  /// Creates a new work item under [parentId] ("null" makes it a top-level
  /// item, with no parent at all) with [statusId]. Throws on failure. Does
  /// not return the created item — callers re-load the current scope to
  /// pick it up.
  Future<void> createWorkItem({
    required String title,
    String? description,
    required String? parentId,
    required String statusId,
  });

  /// Sets or clears who the work item identified by [workItemId] is
  /// assigned to.
  Future<WorkItem> assign(
    String workItemId,
    String? userId, {
    required int expectedVersion,
  });

  /// Replaces the work item identified by [workItemId]'s full tag list.
  Future<WorkItem> setTags(
    String workItemId,
    List<String> tags, {
    required int expectedVersion,
  });
}
