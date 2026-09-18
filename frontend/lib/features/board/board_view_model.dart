import 'package:injectable/injectable.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

@injectable
class BoardViewModel extends ViewModel {
  BoardViewModel(this._repository);

  final BoardRepository _repository;

  List<BoardStatus> _statuses = const [];
  List<Swimlane> _swimlanes = const [];
  bool _isLoading = true;

  List<BoardStatus> get statuses => _statuses;
  List<Swimlane> get swimlanes => _swimlanes;
  bool get isLoading => _isLoading;

  Future<void> load() async {
    final data = await _repository.loadBoard();
    _statuses = [...data.statuses]..sort((a, b) => a.order.compareTo(b.order));
    _swimlanes = data.swimlanes;
    _isLoading = false;
    notifyIfActive();
  }

  /// Moves [card] to [newStatusId], within whichever swimlane already owns
  /// it. There is no swimlane parameter to pass — that's what makes a
  /// cross-swimlane move structurally impossible here, the same way the
  /// backend's `ChangeStatus`/`Reparent` split keeps a status change from
  /// ever touching `ParentId`.
  void moveCard(WorkItemCard card, String newStatusId) {
    if (card.statusId == newStatusId) return;

    final laneIndex = _swimlanes.indexWhere(
      (lane) => lane.parentId == card.parentId,
    );
    if (laneIndex == -1) return;

    final lane = _swimlanes[laneIndex];
    final updatedCards = [
      for (final c in lane.cards)
        if (c.id == card.id) c.copyWith(statusId: newStatusId) else c,
    ];

    _swimlanes = [
      for (var i = 0; i < _swimlanes.length; i++)
        if (i == laneIndex) lane.copyWithCards(updatedCards) else _swimlanes[i],
    ];
    notifyIfActive();
  }
}
