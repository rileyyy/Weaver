import 'package:injectable/injectable.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

Future<void> _noRetry() => Future.value();

@injectable
class BoardViewModel extends ViewModel {
  BoardViewModel(this._repository);

  final BoardRepository _repository;

  List<BoardStatus> _statuses = const [];
  List<Swimlane> _swimlanes = const [];
  List<ScopeCrumb> _breadcrumbs = const [];
  bool _isLoading = true;
  String? _loadError;
  String? _moveError;
  Future<void> Function() _retry = _noRetry;
  DateTime? _filterStart;
  DateTime? _filterEnd;
  String _searchQuery = '';
  final Set<String> _hiddenStatusIds = {};
  CardSortOption _sortOption = CardSortOption.manual;

  List<BoardStatus> get statuses => _statuses;
  List<Swimlane> get swimlanes => _swimlanes;

  /// The path from the board's root scope down to what's currently shown,
  /// for breadcrumb navigation. Always has at least one entry once [load]
  /// has completed.
  List<ScopeCrumb> get breadcrumbs => _breadcrumbs;

  bool get isLoading => _isLoading;

  /// Set when the current scope failed to load; the view replaces the
  /// board with a retry prompt while this is non-null. [retry] re-attempts
  /// whichever navigation caused the failure.
  String? get loadError => _loadError;

  /// Set when a [moveCard], [reparentCard], or [rescheduleCard] call fails
  /// after already having applied its optimistic UI update. Meant to be
  /// surfaced once (e.g. as a SnackBar) and then cleared via
  /// [clearMoveError].
  String? get moveError => _moveError;

  DateTime? get filterStart => _filterStart;
  DateTime? get filterEnd => _filterEnd;

  String get searchQuery => _searchQuery;

  /// Status ids whose column is currently hidden. Checked against every
  /// status, not just [visibleStatuses], so a toggle control can always
  /// show every status as a candidate to re-enable.
  Set<String> get hiddenStatusIds => _hiddenStatusIds;

  /// [statuses], excluding any hidden via [toggleStatusVisibility].
  List<BoardStatus> get visibleStatuses =>
      _statuses.where((s) => !_hiddenStatusIds.contains(s.id)).toList();

  CardSortOption get sortOption => _sortOption;

  Future<void> load() => _changeScope(() async {
        final rootScopeId = await _repository.loadRootScopeItemId();
        final data = await _repository.loadBoard(rootScopeId);
        _applyScope(data);
        _breadcrumbs = [ScopeCrumb(id: rootScopeId, title: 'Board')];
      }, errorMessage: 'Could not load the board. Check your connection and try again.');

  Future<void> retry() => _retry();

  /// Re-scopes the board to [card]'s own children — they become the new
  /// swimlanes. Pushes [card] onto the breadcrumb trail.
  Future<void> drillInto(WorkItemCard card) => _changeScope(() async {
        final data = await _repository.loadBoard(card.id);
        _applyScope(data);
        _breadcrumbs = [
          ..._breadcrumbs,
          ScopeCrumb(id: card.id, title: card.title),
        ];
      }, errorMessage: 'Could not open "${card.title}". Try again.');

  /// Jumps back to the breadcrumb at [index], discarding any deeper
  /// entries. A no-op if [index] is already the current scope.
  Future<void> navigateToBreadcrumb(int index) {
    if (index == _breadcrumbs.length - 1) return Future.value();
    final target = _breadcrumbs[index];

    return _changeScope(() async {
      final data = await _repository.loadBoard(target.id);
      _applyScope(data);
      _breadcrumbs = _breadcrumbs.sublist(0, index + 1);
    }, errorMessage: 'Could not go back to "${target.title}". Try again.');
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
    _swimlanes = _movedWithinLane(laneIndex, card, newStatusId);
    notifyIfActive();

    try {
      await _repository.changeStatus(card.id, newStatusId);
    } catch (_) {
      _swimlanes = previousSwimlanes;
      _moveError = 'Could not move "${card.title}". Try again.';
      notifyIfActive();
    }
  }

  /// Moves [card] to a different swimlane (i.e. a different parent),
  /// keeping its status — the view-model mirror of the backend's
  /// `Reparent`. Applies the move optimistically, then rolls it back if
  /// the backend rejects it.
  Future<void> reparentCard(WorkItemCard card, String newParentId) async {
    if (card.parentId == newParentId) return;

    final oldLaneIndex = _swimlanes.indexWhere(
      (lane) => lane.parentId == card.parentId,
    );
    final newLaneIndex = _swimlanes.indexWhere(
      (lane) => lane.parentId == newParentId,
    );
    if (oldLaneIndex == -1 || newLaneIndex == -1) return;

    final previousSwimlanes = _swimlanes;
    _swimlanes = _movedToLane(oldLaneIndex, newLaneIndex, card, newParentId);
    notifyIfActive();

    try {
      await _repository.reparentItem(card.id, newParentId);
    } catch (_) {
      _swimlanes = previousSwimlanes;
      _moveError = 'Could not move "${card.title}". Try again.';
      notifyIfActive();
    }
  }

  /// Sets [card]'s scheduled start/end, keeping its status and swimlane —
  /// the view-model mirror of the backend's `Reschedule`. Applies the
  /// change optimistically, then rolls it back if the backend rejects it.
  Future<void> rescheduleCard(
    WorkItemCard card,
    DateTime? startDate,
    DateTime? endDate,
  ) async {
    final laneIndex = _swimlanes.indexWhere(
      (lane) => lane.parentId == card.parentId,
    );
    if (laneIndex == -1) return;

    final previousSwimlanes = _swimlanes;
    _swimlanes = _rescheduledWithinLane(laneIndex, card, startDate, endDate);
    notifyIfActive();

    try {
      await _repository.rescheduleItem(card.id, startDate, endDate);
    } catch (_) {
      _swimlanes = previousSwimlanes;
      _moveError = 'Could not reschedule "${card.title}". Try again.';
      notifyIfActive();
    }
  }

  void clearMoveError() => _moveError = null;

  /// Sets the board's time-frame filter. Either bound may be null (open on
  /// that side); passing both null is equivalent to [clearTimeFilter].
  void setTimeFilter({DateTime? start, DateTime? end}) {
    _filterStart = start;
    _filterEnd = end;
    notifyIfActive();
  }

  void clearTimeFilter() => setTimeFilter();

  /// True if [card] should be visible under the current time-frame filter:
  /// its own scheduled window overlaps the filter's, treating either
  /// side's missing bound (the card's or the filter's) as open-ended
  /// rather than excluding the card.
  bool matchesTimeFilter(WorkItemCard card) {
    final filterStart = _filterStart;
    final filterEnd = _filterEnd;
    if (filterStart == null && filterEnd == null) return true;

    final startsInTime =
        filterEnd == null ||
        card.startDate == null ||
        !card.startDate!.isAfter(filterEnd);
    final endsInTime =
        filterStart == null ||
        card.endDate == null ||
        !card.endDate!.isBefore(filterStart);

    return startsInTime && endsInTime;
  }

  void setSearchQuery(String query) {
    _searchQuery = query;
    notifyIfActive();
  }

  void clearSearchQuery() => setSearchQuery('');

  /// True if [card]'s title or description contains [searchQuery]
  /// (case-insensitive). Always true when the query is empty.
  bool matchesSearch(WorkItemCard card) {
    final query = _searchQuery.trim().toLowerCase();
    if (query.isEmpty) return true;

    return card.title.toLowerCase().contains(query) ||
        (card.description?.toLowerCase().contains(query) ?? false);
  }

  /// Whether [card] should be shown on the board under the current
  /// time-frame filter and search query together.
  bool cardVisible(WorkItemCard card) =>
      matchesTimeFilter(card) && matchesSearch(card);

  /// Shows or hides [statusId]'s column. Hiding a status doesn't move or
  /// otherwise change any work item in it — it's purely a display toggle.
  void toggleStatusVisibility(String statusId) {
    if (!_hiddenStatusIds.remove(statusId)) {
      _hiddenStatusIds.add(statusId);
    }
    notifyIfActive();
  }

  void setSortOption(CardSortOption option) {
    _sortOption = option;
    notifyIfActive();
  }

  /// Null for [CardSortOption.manual]: cards stay in the order the backend
  /// returned them (by `Rank`). Any other option returns a comparator the
  /// view applies on top of that order — sorting is display-only and never
  /// changes `Rank` or persists anywhere.
  Comparator<WorkItemCard>? get cardComparator => switch (_sortOption) {
        CardSortOption.manual => null,
        CardSortOption.title => (a, b) =>
            a.title.toLowerCase().compareTo(b.title.toLowerCase()),
        CardSortOption.startDate => (a, b) =>
            _compareOpenEndedDates(a.startDate, b.startDate),
        CardSortOption.dueDate => (a, b) =>
            _compareOpenEndedDates(a.endDate, b.endDate),
      };

  /// Ascending, with a missing date sorted after every present date — an
  /// unscheduled item has no position to sort by, so it falls to the end
  /// rather than being treated as earliest.
  int _compareOpenEndedDates(DateTime? a, DateTime? b) {
    if (a == null) return b == null ? 0 : 1;
    if (b == null) return -1;
    return a.compareTo(b);
  }

  Future<void> _changeScope(
    Future<void> Function() action, {
    required String errorMessage,
  }) async {
    _isLoading = true;
    _loadError = null;
    _retry = () => _changeScope(action, errorMessage: errorMessage);
    notifyIfActive();

    try {
      await action();
    } catch (_) {
      _loadError = errorMessage;
    } finally {
      _isLoading = false;
      notifyIfActive();
    }
  }

  void _applyScope(BoardData data) {
    _statuses = [...data.statuses]..sort((a, b) => a.order.compareTo(b.order));
    _swimlanes = data.swimlanes;
  }

  List<Swimlane> _movedWithinLane(
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

  List<Swimlane> _rescheduledWithinLane(
    int laneIndex,
    WorkItemCard card,
    DateTime? startDate,
    DateTime? endDate,
  ) {
    final lane = _swimlanes[laneIndex];
    final updatedCards = [
      for (final c in lane.cards)
        if (c.id == card.id) c.rescheduled(startDate, endDate) else c,
    ];

    return [
      for (var i = 0; i < _swimlanes.length; i++)
        if (i == laneIndex) lane.copyWithCards(updatedCards) else _swimlanes[i],
    ];
  }

  List<Swimlane> _movedToLane(
    int oldLaneIndex,
    int newLaneIndex,
    WorkItemCard card,
    String newParentId,
  ) {
    final oldLane = _swimlanes[oldLaneIndex];
    final newLane = _swimlanes[newLaneIndex];
    final movedCard = card.movedToParent(newParentId);

    final updatedOldLaneCards = [
      for (final c in oldLane.cards)
        if (c.id != card.id) c,
    ];
    final updatedNewLaneCards = [...newLane.cards, movedCard];

    return [
      for (var i = 0; i < _swimlanes.length; i++)
        if (i == oldLaneIndex)
          oldLane.copyWithCards(updatedOldLaneCards)
        else if (i == newLaneIndex)
          newLane.copyWithCards(updatedNewLaneCards)
        else
          _swimlanes[i],
    ];
  }
}
