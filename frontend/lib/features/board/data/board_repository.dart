import 'package:weaver/features/board/models/board_data.dart';

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
}
