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
  String? _loadError;
  String? _moveError;

  List<BoardStatus> get statuses => _statuses;
  List<Swimlane> get swimlanes => _swimlanes;
  bool get isLoading => _isLoading;

  /// Set when [load] fails; the view replaces the board with a retry prompt
  /// while this is non-null.
  String? get loadError => _loadError;

  /// Set when a [moveCard] call fails after already having applied its
  /// optimistic UI update. Meant to be surfaced once (e.g. as a SnackBar)
  /// and then cleared via [clearMoveError].
  String? get moveError => _moveError;

  Future<void> load() async {
    _isLoading = true;
    _loadError = null;
    notifyIfActive();

    try {
      final data = await _repository.loadBoard();
      _statuses = [...data.statuses]
        ..sort((a, b) => a.order.compareTo(b.order));
      _swimlanes = data.swimlanes;
    } catch (_) {
      _loadError = 'Could not load the board. Check your connection and try again.';
    } finally {
      _isLoading = false;
      notifyIfActive();
    }
  }

  /// Moves [card] to [newStatusId], within whichever swimlane already owns
  /// it. There is no swimlane parameter to pass — that's what makes a
  /// cross-swimlane move structurally impossible here, the same way the
  /// backend's `ChangeStatus`/`Reparent` split keeps a status change from
  /// ever touching `ParentId`. Applies the move optimistically, then rolls
  /// it back if the backend rejects it.
  Future<void> moveCard(WorkItemCard card, String newStatusId) async {
    if (card.statusId == newStatusId) return;

    final laneIndex = _swimlanes.indexWhere(
      (lane) => lane.parentId == card.parentId,
    );
    if (laneIndex == -1) return;

    final previousSwimlanes = _swimlanes;
    _swimlanes = _movedTo(laneIndex, card, newStatusId);
    notifyIfActive();

    try {
      await _repository.changeStatus(card.id, newStatusId);
    } catch (_) {
      _swimlanes = previousSwimlanes;
      _moveError = 'Could not move "${card.title}". Try again.';
      notifyIfActive();
    }
  }

  void clearMoveError() => _moveError = null;

  List<Swimlane> _movedTo(
    int laneIndex,
    WorkItemCard card,
    String newStatusId,
  ) {
    final lane = _swimlanes[laneIndex];
    final updatedCards = [
      for (final c in lane.cards)
        if (c.id == card.id) c.copyWith(statusId: newStatusId) else c,
    ];

    return [
      for (var i = 0; i < _swimlanes.length; i++)
        if (i == laneIndex) lane.copyWithCards(updatedCards) else _swimlanes[i],
    ];
  }
}
