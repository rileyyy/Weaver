import 'dart:ui' show Color;

import 'package:injectable/injectable.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
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
  List<HierarchyItem> _hierarchyItems = const [];
  bool _isHierarchyLoading = false;
  String? _hierarchyLoadError;
  bool _hierarchyLoaded = false;

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

  bool get isHierarchyLoading => _isHierarchyLoading;

  /// Set when the last [loadHierarchy] call failed; the Hierarchy view
  /// replaces its list with a retry prompt while this is non-null.
  String? get hierarchyLoadError => _hierarchyLoadError;

  /// True once [loadHierarchy] has been attempted at least once —
  /// regardless of success — so callers (e.g. after a delete elsewhere on
  /// the board) know whether refreshing the Hierarchy view is worthwhile.
  bool get hierarchyLoaded => _hierarchyLoaded;

  /// The Hierarchy view's root nodes: every top-level work item, each with
  /// its descendants nested underneath. Built fresh from [_hierarchyItems]
  /// on every access — grouped by parent, filtered by the same time/search/
  /// status-visibility predicates the swim-lane board applies (see
  /// [hierarchyItemVisible]), and each sibling group ordered by
  /// [hierarchyComparator]. A node whose own fields don't match the current
  /// filters is still included if any descendant does, so a matching item's
  /// ancestors stay visible to show where it sits in the tree.
  List<HierarchyNode> get hierarchyRoots {
    final byParent = <String?, List<HierarchyItem>>{};
    for (final item in _hierarchyItems) {
      (byParent[item.parentId] ??= []).add(item);
    }

    List<HierarchyNode> buildLevel(String? parentId) {
      final children = byParent[parentId] ?? const [];
      final comparator = hierarchyComparator;
      final ordered = comparator == null ? children : ([...children]..sort(comparator));

      final nodes = <HierarchyNode>[];
      for (final item in ordered) {
        final childNodes = buildLevel(item.id);
        if (!hierarchyItemVisible(item) && childNodes.isEmpty) continue;
        nodes.add(HierarchyNode(item: item, children: childNodes));
      }
      return nodes;
    }

    return buildLevel(null);
  }

  /// Loads every work item for the Hierarchy view. Independent of the
  /// swim-lane board's current drill-down scope — the Hierarchy view always
  /// shows the whole tree.
  Future<void> loadHierarchy() async {
    _isHierarchyLoading = true;
    _hierarchyLoadError = null;
    notifyIfActive();

    try {
      _hierarchyItems = await _repository.loadAllItems();
    } catch (_) {
      _hierarchyLoadError = 'Could not load the hierarchy view. Check your connection and try again.';
    } finally {
      _hierarchyLoaded = true;
      _isHierarchyLoading = false;
      notifyIfActive();
    }
  }

  /// Re-fetches the Hierarchy view's data only if it's already been loaded
  /// once — e.g. after a delete elsewhere on the board, so a still-unopened
  /// Hierarchy tab doesn't trigger a needless network call.
  Future<void> refreshHierarchyIfLoaded() => _hierarchyLoaded ? loadHierarchy() : Future.value();

  String? statusNameFor(String statusId) {
    for (final status in _statuses) {
      if (status.id == statusId) return status.name;
    }
    return null;
  }

  Color? statusColorFor(String statusId) {
    for (final status in _statuses) {
      if (status.id == statusId) return status.color;
    }
    return null;
  }

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

  /// Creates a new work item under [parentId] (null for a new top-level
  /// item) with [statusId], then reloads the current scope to pick it up —
  /// there's no optimistic add, since the created item's id isn't known
  /// until the repository call returns.
  Future<void> createWorkItem({
    required String title,
    String? description,
    required String? parentId,
    required String statusId,
  }) => _changeScope(() async {
        await _repository.createWorkItem(
          title: title,
          description: description,
          parentId: parentId,
          statusId: statusId,
        );
        final data = await _repository.loadBoard(_breadcrumbs.last.id);
        _applyScope(data);
      }, errorMessage: 'Could not create "$title". Try again.');

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
  bool matchesTimeFilter(WorkItemCard card) =>
      _matchesTimeWindow(card.startDate, card.endDate);

  /// Core of [matchesTimeFilter], generalized to a bare start/end pair so
  /// [hierarchyItemVisible] can share it without needing a [WorkItemCard].
  bool _matchesTimeWindow(DateTime? itemStart, DateTime? itemEnd) {
    final filterStart = _filterStart;
    final filterEnd = _filterEnd;
    if (filterStart == null && filterEnd == null) return true;

    final startsInTime =
        filterEnd == null ||
        itemStart == null ||
        !itemStart.isAfter(filterEnd);
    final endsInTime =
        filterStart == null ||
        itemEnd == null ||
        !itemEnd.isBefore(filterStart);

    return startsInTime && endsInTime;
  }

  void setSearchQuery(String query) {
    _searchQuery = query;
    notifyIfActive();
  }

  void clearSearchQuery() => setSearchQuery('');

  /// True if [card]'s title or description contains [searchQuery]
  /// (case-insensitive). Always true when the query is empty.
  bool matchesSearch(WorkItemCard card) =>
      _matchesSearchText(card.title, card.description);

  /// Core of [matchesSearch], generalized to bare title/description so
  /// [hierarchyItemVisible] can share it without needing a [WorkItemCard].
  bool _matchesSearchText(String title, String? description) {
    final query = _searchQuery.trim().toLowerCase();
    if (query.isEmpty) return true;

    return title.toLowerCase().contains(query) ||
        (description?.toLowerCase().contains(query) ?? false);
  }

  /// Whether [card] should be shown on the board under the current
  /// time-frame filter and search query together.
  bool cardVisible(WorkItemCard card) =>
      matchesTimeFilter(card) && matchesSearch(card);

  /// Whether [item] should be shown in the Hierarchy view under the current
  /// time-frame filter, search query, and hidden-status columns together.
  /// Unlike [cardVisible], this also checks [hiddenStatusIds] directly —
  /// the swim-lane board gets that for free by only ever iterating
  /// [visibleStatuses], but the Hierarchy view isn't organized by column.
  bool hierarchyItemVisible(HierarchyItem item) =>
      !_hiddenStatusIds.contains(item.statusId) &&
      _matchesTimeWindow(item.startDate, item.endDate) &&
      _matchesSearchText(item.title, item.description);

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

  /// The Hierarchy view's mirror of [cardComparator]: null for
  /// [CardSortOption.manual] (each sibling group keeps the backend's
  /// order), otherwise a comparator applied within each sibling group by
  /// [hierarchyRoots] — sorting the whole flat list at once would destroy
  /// the nesting a tree view depends on.
  Comparator<HierarchyItem>? get hierarchyComparator => switch (_sortOption) {
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
