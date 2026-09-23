import 'dart:ui' show Color;

import 'package:injectable/injectable.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/auth/models/auth_user.dart';
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
  final Set<String> _selectedTagFilters = {};
  List<HierarchyItem> _hierarchyItems = const [];
  bool _isHierarchyLoading = false;
  String? _hierarchyLoadError;
  bool _hierarchyLoaded = false;
  List<AuthUser> _users = const [];

  List<BoardStatus> get statuses => _statuses;
  List<Swimlane> get swimlanes => _swimlanes;

  /// Every registered user, for building an assignee picker. Empty until
  /// [load] resolves the user directory (best-effort — see [load]).
  List<AuthUser> get users => _users;

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

  /// Tags currently selected in the Filters dialog. Empty means no tag
  /// filter is applied (shows everything) — unlike [hiddenStatusIds], this
  /// is a filter you opt into, not out of.
  Set<String> get selectedTagFilters => _selectedTagFilters;

  /// Every distinct tag currently seen across loaded swimlane cards and
  /// Hierarchy items, sorted case-insensitively — for populating the
  /// Filters dialog's tag chip list.
  List<String> get availableTags {
    final seen = <String, String>{};
    for (final lane in _swimlanes) {
      for (final card in lane.cards) {
        for (final tag in card.tags) {
          seen.putIfAbsent(tag.toLowerCase(), () => tag);
        }
      }
    }

    for (final item in _hierarchyItems) {
      for (final tag in item.tags) {
        seen.putIfAbsent(tag.toLowerCase(), () => tag);
      }
    }

    return seen.values.toList()
      ..sort((a, b) => a.toLowerCase().compareTo(b.toLowerCase()));
  }

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
      final ordered = comparator == null
          ? children
          : ([...children]..sort(comparator));

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
      _hierarchyLoadError =
          'Could not load the hierarchy view. Check your connection and try again.';
    } finally {
      _hierarchyLoaded = true;
      _isHierarchyLoading = false;
      notifyIfActive();
    }
  }

  /// Re-fetches the Hierarchy view's data only if it's already been loaded
  /// once — e.g. after a delete elsewhere on the board, so a still-unopened
  /// Hierarchy tab doesn't trigger a needless network call.
  Future<void> refreshHierarchyIfLoaded() =>
      _hierarchyLoaded ? loadHierarchy() : Future.value();

  String? statusNameFor(String statusId) {
    for (final status in _statuses) {
      if (status.id == statusId) {
        return status.name;
      }
    }

    return null;
  }

  Color? statusColorFor(String statusId) {
    for (final status in _statuses) {
      if (status.id == statusId) return status.color;
    }

    return null;
  }

  /// The uppercased first letter of the assigned user's username, for a
  /// small avatar — null if [userId] is null or unresolved (e.g. the user
  /// directory hasn't loaded yet).
  String? assigneeInitialFor(String? userId) {
    if (userId == null) {
      return null;
    }

    for (final user in _users) {
      if (user.id == userId) {
        return user.username.isEmpty ? null : user.username[0].toUpperCase();
      }
    }

    return null;
  }

  /// The assigned user's full username, for a picker/column with room to
  /// show more than just an initial — null on the same terms as
  /// [assigneeInitialFor].
  String? usernameFor(String? userId) {
    if (userId == null) {
      return null;
    }

    for (final user in _users) {
      if (user.id == userId) {
        return user.username;
      }
    }

    return null;
  }

  Future<void> load() => _changeScope(
    () async {
      final rootScopeId = await _repository.loadRootScopeItemId();
      final data = await _repository.loadBoard(rootScopeId);
      _applyScope(data);
      _breadcrumbs = [ScopeCrumb(id: rootScopeId, title: 'Board')];
      // Best-effort: a user directory failure shouldn't block the board
      // itself from loading — assignee initials just won't show.
      try {
        _users = await _repository.loadUsers();
      } catch (_) {
        _users = const [];
      }
    },
    errorMessage:
        'Could not load the board. Check your connection and try again.',
  );

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
    if (index == _breadcrumbs.length - 1) {
      return Future.value();
    }

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
    if (card.statusId == newStatusId) {
      return;
    }

    final laneIndex = _swimlanes.indexWhere(
      (lane) => lane.parentId == card.parentId,
    );
    if (laneIndex == -1) {
      return;
    }

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
    if (card.parentId == newParentId) {
      return;
    }

    final oldLaneIndex = _swimlanes.indexWhere(
      (lane) => lane.parentId == card.parentId,
    );
    final newLaneIndex = _swimlanes.indexWhere(
      (lane) => lane.parentId == newParentId,
    );
    if (oldLaneIndex == -1 || newLaneIndex == -1) {
      return;
    }

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
    if (laneIndex == -1) {
      return;
    }

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

  /// Sets or clears who the work item identified by [workItemId] is
  /// assigned to. [workItemId] may be a card, a swimlane's own parent item,
  /// a Hierarchy item, or several of those at once (the same work item can
  /// appear in more than one place) — every matching in-memory
  /// representation is updated together. Applies optimistically, then rolls
  /// back if the backend rejects it.
  Future<void> assign(String workItemId, String? userId) async {
    final previousSwimlanes = _swimlanes;
    final previousHierarchyItems = _hierarchyItems;

    _swimlanes = _assignedInSwimlanes(workItemId, userId);
    _hierarchyItems = [
      for (final item in _hierarchyItems)
        if (item.id == workItemId) item.assigned(userId) else item,
    ];
    notifyIfActive();

    try {
      await _repository.assign(workItemId, userId);
    } catch (_) {
      _swimlanes = previousSwimlanes;
      _hierarchyItems = previousHierarchyItems;
      _moveError = 'Could not update the assignee. Try again.';
      notifyIfActive();
    }
  }

  List<Swimlane> _assignedInSwimlanes(String workItemId, String? userId) => [
    for (final lane in _swimlanes)
      if (lane.parentId == workItemId)
        lane
            .assigned(userId)
            .copyWithCards(_assignedInCards(lane.cards, workItemId, userId))
      else
        lane.copyWithCards(_assignedInCards(lane.cards, workItemId, userId)),
  ];

  List<WorkItemCard> _assignedInCards(
    List<WorkItemCard> cards,
    String workItemId,
    String? userId,
  ) => [
    for (final card in cards)
      if (card.id == workItemId) card.assigned(userId) else card,
  ];

  /// Replaces the tag list of the work item identified by [workItemId] —
  /// the view-model mirror of [assign], same dual-update-then-rollback
  /// shape (a work item's tags may need updating in both a swimlane card
  /// and a Hierarchy item at once).
  Future<void> setTags(String workItemId, List<String> tags) async {
    final previousSwimlanes = _swimlanes;
    final previousHierarchyItems = _hierarchyItems;

    _swimlanes = _taggedInSwimlanes(workItemId, tags);
    _hierarchyItems = [
      for (final item in _hierarchyItems)
        if (item.id == workItemId) item.tagged(tags) else item,
    ];
    notifyIfActive();

    try {
      await _repository.setTags(workItemId, tags);
    } catch (_) {
      _swimlanes = previousSwimlanes;
      _hierarchyItems = previousHierarchyItems;
      _moveError = 'Could not update the tags. Try again.';
      notifyIfActive();
    }
  }

  List<Swimlane> _taggedInSwimlanes(String workItemId, List<String> tags) => [
    for (final lane in _swimlanes)
      lane.copyWithCards(_taggedInCards(lane.cards, workItemId, tags)),
  ];

  List<WorkItemCard> _taggedInCards(
    List<WorkItemCard> cards,
    String workItemId,
    List<String> tags,
  ) => [
    for (final card in cards)
      if (card.id == workItemId) card.tagged(tags) else card,
  ];

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
    if (_filterStart == null && _filterEnd == null) {
      return true;
    }

    final startsInTime =
        _filterEnd == null ||
        itemStart == null ||
        !itemStart.isAfter(_filterEnd!);

    final endsInTime =
        _filterStart == null ||
        itemEnd == null ||
        !itemEnd.isBefore(_filterStart!);

    return startsInTime && endsInTime;
  }

  void setSearchQuery(String query) {
    _searchQuery = query;
    notifyIfActive();
  }

  void clearSearchQuery() => setSearchQuery('');

  /// True if [card]'s title, description, or any tag contains [searchQuery]
  /// (case-insensitive). Always true when the query is empty.
  bool matchesSearch(WorkItemCard card) =>
      _matchesSearchText(card.title, card.description, card.tags);

  /// Core of [matchesSearch], generalized to bare title/description/tags so
  /// [hierarchyItemVisible] can share it without needing a [WorkItemCard].
  bool _matchesSearchText(
    String title,
    String? description,
    List<String> tags,
  ) {
    final query = _searchQuery.trim().toLowerCase();
    if (query.isEmpty) {
      return true;
    }

    return title.toLowerCase().contains(query) ||
        (description?.toLowerCase().contains(query) ?? false) ||
        tags.any((tag) => tag.toLowerCase().contains(query));
  }

  /// Shows or hides [tag] from the current tag filter. An empty selection
  /// means no tag filter is applied.
  void toggleTagFilter(String tag) {
    if (!_selectedTagFilters.remove(tag)) {
      _selectedTagFilters.add(tag);
    }

    notifyIfActive();
  }

  /// True if no tag filter is selected, or [tags] contains at least one
  /// selected tag.
  bool matchesTagFilter(List<String> tags) =>
      _selectedTagFilters.isEmpty || tags.any(_selectedTagFilters.contains);

  /// Whether [card] should be shown on the board under the current
  /// time-frame filter, search query, and tag filter together.
  bool cardVisible(WorkItemCard card) =>
      matchesTimeFilter(card) &&
      matchesSearch(card) &&
      matchesTagFilter(card.tags);

  /// Whether [item] should be shown in the Hierarchy view under the current
  /// time-frame filter, search query, tag filter, and hidden-status columns
  /// together. Unlike [cardVisible], this also checks [hiddenStatusIds]
  /// directly — the swim-lane board gets that for free by only ever
  /// iterating [visibleStatuses], but the Hierarchy view isn't organized by
  /// column.
  bool hierarchyItemVisible(HierarchyItem item) =>
      !_hiddenStatusIds.contains(item.statusId) &&
      _matchesTimeWindow(item.startDate, item.endDate) &&
      _matchesSearchText(item.title, item.description, item.tags) &&
      matchesTagFilter(item.tags);

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
    CardSortOption.title => (a, b) => a.title.toLowerCase().compareTo(
      b.title.toLowerCase(),
    ),
    CardSortOption.startDate => (a, b) => _compareOpenEndedDates(
      a.startDate,
      b.startDate,
    ),
    CardSortOption.dueDate => (a, b) => _compareOpenEndedDates(
      a.endDate,
      b.endDate,
    ),
  };

  /// The Hierarchy view's mirror of [cardComparator]: null for
  /// [CardSortOption.manual] (each sibling group keeps the backend's
  /// order), otherwise a comparator applied within each sibling group by
  /// [hierarchyRoots] — sorting the whole flat list at once would destroy
  /// the nesting a tree view depends on.
  Comparator<HierarchyItem>? get hierarchyComparator => switch (_sortOption) {
    CardSortOption.manual => null,
    CardSortOption.title => (a, b) => a.title.toLowerCase().compareTo(
      b.title.toLowerCase(),
    ),
    CardSortOption.startDate => (a, b) => _compareOpenEndedDates(
      a.startDate,
      b.startDate,
    ),
    CardSortOption.dueDate => (a, b) => _compareOpenEndedDates(
      a.endDate,
      b.endDate,
    ),
  };

  /// Ascending, with a missing date sorted after every present date — an
  /// unscheduled item has no position to sort by, so it falls to the end
  /// rather than being treated as earliest.
  int _compareOpenEndedDates(DateTime? a, DateTime? b) {
    if (a == null) {
      return b == null ? 0 : 1;
    }
    
    if (b == null) {
      return -1;
    }

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
