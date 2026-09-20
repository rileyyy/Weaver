import 'package:weaver/features/auth/models/auth_user.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';

/// Source of board data and the operations [BoardViewModel] can perform on
/// it. [BoardViewModel] depends on this interface, not a concrete source.
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
  Future<List<HierarchyItem>> loadAllItems();

  /// Every registered user, for resolving a work item's assignee to a
  /// display name/initial on the board.
  Future<List<AuthUser>> loadUsers();

  /// Moves the work item identified by [cardId] to [newStatusId], within
  /// whichever parent already owns it. Throws on failure.
  Future<void> changeStatus(String cardId, String newStatusId);

  /// Moves the work item identified by [itemId] to [newParentId], keeping
  /// its current status. Throws on failure.
  Future<void> reparentItem(String itemId, String newParentId);

  /// Sets the work item identified by [itemId]'s scheduled start/end.
  /// Either may be null. Throws on failure.
  Future<void> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate,
  );

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
}
