import 'package:weaver/features/board/models/board_data.dart';

/// Source of board data and the operations [BoardViewModel] can perform on
/// it. [BoardViewModel] depends on this interface, not a concrete source.
abstract class BoardRepository {
  Future<BoardData> loadBoard();

  /// Moves the work item identified by [cardId] to [newStatusId], within
  /// whichever parent already owns it. Throws on failure.
  Future<void> changeStatus(String cardId, String newStatusId);
}
