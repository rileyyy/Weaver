import 'package:weaver/features/board/models/board_data.dart';

/// Source of board data. [FakeBoardRepository] is the only implementation
/// until Milestone 5 swaps in one backed by the REST API — [BoardViewModel]
/// depends on this interface, not a concrete source, so that swap won't
/// touch the view or view model.
abstract class BoardRepository {
  Future<BoardData> loadBoard();
}
