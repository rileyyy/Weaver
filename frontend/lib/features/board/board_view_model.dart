import 'dart:ui' show Color;

import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/state/board_filters.dart';
import 'package:weaver/features/board/state/hierarchy_tree_builder.dart';
import 'package:weaver/features/board/state/swimlane_edits.dart';
import 'package:weaver/features/board/state/user_directory.dart';
import 'package:weaver/features/board/state/work_item_sorting.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item_status.dart';

Future<void> _noRetry() => Future.value();

/// Orchestrates the board: which scope is shown (loading, drill-down,
/// breadcrumbs, retry) and optimistic mutations. Filtering, sorting, tree
/// building, lane edits and user lookups live in `state/` as pure units.
@injectable
class BoardViewModel extends ViewModel {
  BoardViewModel(this._repository);

  final BoardRepository _repository;

  List<WorkItemStatus> _statuses = const [];
  Map<String, WorkItemStatus> _statusById = const {};
  List<Swimlane> _swimlanes = const [];
  List<ScopeCrumb> _breadcrumbs = const [];
  bool _isLoading = true;
  String? _loadError;
  String? _moveError;
  Future<void> Function() _retry = _noRetry;
  BoardFilters _filters = const BoardFilters();
  List<HierarchyItem> _hierarchyItems = const [];
  bool _isHierarchyLoading = false;
  String? _hierarchyLoadError;
  bool _hierarchyLoaded = false;
  UserDirectory _users = UserDirectory.empty;

  // Bumped by every scope/hierarchy load so a slower, older response can't
  // overwrite a newer one, and so optimistic rollbacks know when the data
  // they captured has since been replaced.
  int _scopeGeneration = 0;
  int _hierarchyGeneration = 0;

  // Derived values the views read many times per frame. Every state change
  // notifies, so they're cached until the next notification.
  List<HierarchyNode>? _hierarchyRootsCache;
  List<WorkItemStatus>? _visibleStatusesCache;
  List<String>? _availableTagsCache;

  @override
  void notifyIfActive() {
    _hierarchyRootsCache = null;
    _visibleStatusesCache = null;
    _availableTagsCache = null;
    super.notifyIfActive();
  }

  List<WorkItemStatus> get statuses => _statuses;
  List<Swimlane> get swimlanes => _swimlanes;

  /// Every registered user, for building an assignee picker. Empty until
  /// [load] resolves the user directory (best-effort — see [load]).
  List<User> get users => _users.users;

  /// The path from the board's root scope down to what's currently shown,
  /// for breadcrumb navigation. Always has at least one entry once [load]
  /// has completed.
  List<ScopeCrumb> get breadcrumbs => _breadcrumbs;

  bool get isLoading => _isLoading;

  /// Creating needs a loaded scope to create into and to reload afterwards.
  bool get canCreateWorkItem =>
      !_isLoading && _loadError == null && _breadcrumbs.isNotEmpty;

  /// Set when the current scope failed to load; the view replaces the
  /// board with a retry prompt while this is non-null. [retry] re-attempts
  /// whichever navigation caused the failure.
  String? get loadError => _loadError;

  /// Set when a board mutation ([moveCard], [reparentCard], [rescheduleCard],
  /// [assign], [setTags], [createWorkItem]) fails. Unlike [loadError] it
  /// leaves the board in place: it's meant to be surfaced once (e.g. as a
  /// SnackBar) and then cleared via [clearMoveError].
  String? get moveError => _moveError;

  DateTime? get filterStart => _filters.start;
  DateTime? get filterEnd => _filters.end;
  String get searchQuery => _filters.searchQuery;
  CardSortOption get sortOption => _filters.sortOption;

  /// Status ids whose column is currently hidden (read-only). Checked
  /// against every status, not just [visibleStatuses], so a toggle control
  /// can always show every status as a candidate to re-enable.
  Set<String> get hiddenStatusIds => _filters.hiddenStatusIds;

  /// [statuses], excluding any hidden via [toggleStatusVisibility].
  List<WorkItemStatus> get visibleStatuses => _visibleStatusesCache ??= [
    for (final status in _statuses)
      if (!_filters.hiddenStatusIds.contains(status.id)) status,
  ];

  /// Whether [tag] (any casing) is selected in the Filters dialog. With
  /// nothing selected no tag filter is applied — unlike [hiddenStatusIds],
  /// this is a filter you opt into, not out of.
  bool isTagFilterSelected(String tag) => _filters.isTagSelected(tag);

  /// Every distinct tag currently seen across loaded swimlane cards and
  /// Hierarchy items, sorted case-insensitively — for populating the
  /// Filters dialog's tag chip list.
  List<String> get availableTags => _availableTagsCache ??= () {
    final seen = <String, String>{};
    final allTags = [
      for (final lane in _swimlanes)
        for (final card in lane.cards) ...card.tags,
      for (final item in _hierarchyItems) ...item.tags,
    ];
    for (final tag in allTags) {
      seen.putIfAbsent(tag.toLowerCase(), () => tag);
    }
    return seen.values.toList()
      ..sort((a, b) => a.toLowerCase().compareTo(b.toLowerCase()));
  }();

  bool get isHierarchyLoading => _isHierarchyLoading;

  /// Set when the last [loadHierarchy] call failed; the Hierarchy view
  /// replaces its list with a retry prompt while this is non-null.
  String? get hierarchyLoadError => _hierarchyLoadError;

  /// True once [loadHierarchy] has been attempted at least once —
  /// regardless of success — so callers (e.g. after a delete elsewhere on
  /// the board) know whether refreshing the Hierarchy view is worthwhile.
  bool get hierarchyLoaded => _hierarchyLoaded;

  /// The Hierarchy (and Roadmap) view's root nodes, filtered and sorted with
  /// the same filters as the board — see [buildHierarchyTree].
  List<HierarchyNode> get hierarchyRoots =>
      _hierarchyRootsCache ??= buildHierarchyTree(
        _hierarchyItems,
        isVisible: _filters.hierarchyItemVisible,
        comparator: hierarchyComparator,
      );

  /// Loads every work item for the Hierarchy view. Independent of the
  /// swim-lane board's current drill-down scope — the Hierarchy view always
  /// shows the whole tree.
  Future<void> loadHierarchy() async {
    final generation = ++_hierarchyGeneration;
    _isHierarchyLoading = true;
    _hierarchyLoadError = null;
    notifyIfActive();

    try {
      final items = await _repository.loadAllItems();
      if (generation != _hierarchyGeneration) return;
      _hierarchyItems = items;
    } on ApiException {
      if (generation != _hierarchyGeneration) return;
      _hierarchyLoadError =
          'Could not load the hierarchy view. Check your connection and try again.';
    } finally {
      if (generation == _hierarchyGeneration) {
        _hierarchyLoaded = true;
        _isHierarchyLoading = false;
        notifyIfActive();
      }
    }
  }

  /// Re-fetches the Hierarchy view's data only if it's already been loaded
  /// once — e.g. after a delete elsewhere on the board, so a still-unopened
  /// Hierarchy tab doesn't trigger a needless network call.
  Future<void> refreshHierarchyIfLoaded() =>
      _hierarchyLoaded ? loadHierarchy() : Future.value();

  String? statusNameFor(String statusId) => _statusById[statusId]?.name;

  Color? statusColorFor(String statusId) => _statusById[statusId]?.color;

  /// The uppercased first letter of the assigned user's username, for a
  /// small avatar — null if [userId] is null or unresolved (e.g. the user
  /// directory hasn't loaded yet).
  String? assigneeInitialFor(String? userId) => _users.initialFor(userId);

  /// The assigned user's full username — null on the same terms as
  /// [assigneeInitialFor].
  String? usernameFor(String? userId) => _users.usernameFor(userId);

  Future<void> load() => _changeScope(
    () async {
      final rootScopeId = await _repository.loadRootScopeItemId();
      final data = await _repository.loadBoard(rootScopeId);
      // Best-effort: a user directory failure shouldn't block the board
      // itself from loading — assignee initials just won't show.
      List<User> users;
      try {
        users = await _repository.loadUsers();
      } on ApiException {
        users = const [];
      }
      return () {
        _applyScope(data);
        _breadcrumbs = [ScopeCrumb(id: rootScopeId, title: 'Board')];
        _users = UserDirectory(users);
      };
    },
    errorMessage:
        'Could not load the board. Check your connection and try again.',
  );

  /// Re-attempts the scope load that last failed. A no-op once a load has
  /// succeeded; use [refreshCurrentScope] to reload what's shown.
  Future<void> retry() => _retry();

  /// Reloads the scope currently shown, e.g. after an item was deleted from
  /// the detail dialog. Keeps the breadcrumb trail as it is.
  Future<void> refreshCurrentScope() {
    if (_breadcrumbs.isEmpty) return load();

    final current = _breadcrumbs.last;
    return _changeScope(
      () async {
        final data = await _repository.loadBoard(current.id);
        return () => _applyScope(data);
      },
      errorMessage:
          'Could not refresh the board. Check your connection and try again.',
    );
  }

  /// Re-scopes the board to [card]'s own children — they become the new
  /// swimlanes. Pushes [card] onto the breadcrumb trail.
  Future<void> drillInto(WorkItemCard card) => _changeScope(() async {
    final data = await _repository.loadBoard(card.id);
    return () {
      _applyScope(data);
      _breadcrumbs = [
        ..._breadcrumbs,
        ScopeCrumb(id: card.id, title: card.title),
      ];
    };
  }, errorMessage: 'Could not open "${card.title}". Try again.');

  /// Jumps back to the breadcrumb at [index], discarding any deeper
  /// entries. A no-op if [index] is already the current scope.
  Future<void> navigateToBreadcrumb(int index) {
    if (index == _breadcrumbs.length - 1) {
      return Future.value();
    }

    final trail = _breadcrumbs.sublist(0, index + 1);
    final target = trail.last;

    return _changeScope(() async {
      final data = await _repository.loadBoard(target.id);
      return () {
        _applyScope(data);
        _breadcrumbs = trail;
      };
    }, errorMessage: 'Could not go back to "${target.title}". Try again.');
  }

  /// Moves [card] to [newStatusId], within whichever swimlane already owns
  /// it. There is no swimlane parameter to pass — that's what makes a
  /// cross-swimlane move structurally impossible here, the same way the
  /// backend's `ChangeStatus`/`Reparent` split keeps a status change from
  /// ever touching `ParentId`. Applies the move optimistically, then rolls
  /// it back if the backend rejects it.
  Future<void> moveCard(WorkItemCard card, String newStatusId) {
    if (card.statusId == newStatusId || !_swimlanes.hasLane(card.parentId)) {
      return Future.value();
    }

    return _optimistic(
      apply: () {
        _swimlanes = _swimlanes.withCard(
          card.id,
          (c) => c.copyWith(statusId: newStatusId),
        );
        _hierarchyItems = _withHierarchyItem(
          card.id,
          (item) => item.withStatus(newStatusId),
        );
      },
      persist: () => _repository.changeStatus(card.id, newStatusId),
      revertLanes: () => _swimlanes = _swimlanes.withCard(
        card.id,
        (c) => c.copyWith(statusId: card.statusId),
      ),
      revertHierarchy: () => _hierarchyItems = _withHierarchyItem(
        card.id,
        (item) => item.withStatus(card.statusId),
      ),
      errorMessage: 'Could not move "${card.title}". Try again.',
    );
  }

  /// Moves [card] to a different swimlane (i.e. a different parent),
  /// keeping its status — the view-model mirror of the backend's
  /// `Reparent`. Applies the move optimistically, then rolls it back if
  /// the backend rejects it.
  Future<void> reparentCard(WorkItemCard card, String newParentId) {
    if (card.parentId == newParentId ||
        !_swimlanes.hasLane(card.parentId) ||
        !_swimlanes.hasLane(newParentId)) {
      return Future.value();
    }

    final originalIndex = _swimlanes
        .firstWhere((lane) => lane.parentId == card.parentId)
        .cards
        .indexWhere((c) => c.id == card.id);

    return _optimistic(
      apply: () {
        _swimlanes = _swimlanes.withCardMovedToLane(
          card.id,
          card.parentId,
          newParentId,
        );
        _hierarchyItems = _withHierarchyItem(
          card.id,
          (item) => item.movedToParent(newParentId),
        );
      },
      persist: () => _repository.reparentItem(card.id, newParentId),
      revertLanes: () => _swimlanes = _swimlanes.withCardMovedToLane(
        card.id,
        newParentId,
        card.parentId,
        atIndex: originalIndex,
      ),
      revertHierarchy: () => _hierarchyItems = _withHierarchyItem(
        card.id,
        (item) => item.movedToParent(card.parentId),
      ),
      errorMessage: 'Could not move "${card.title}". Try again.',
    );
  }

  /// Sets [card]'s scheduled start/end, keeping its status and swimlane —
  /// the view-model mirror of the backend's `Reschedule`. Applies the
  /// change optimistically, then rolls it back if the backend rejects it.
  Future<void> rescheduleCard(
    WorkItemCard card,
    DateTime? startDate,
    DateTime? endDate,
  ) {
    if (!_swimlanes.hasLane(card.parentId)) return Future.value();

    return _optimistic(
      apply: () {
        _swimlanes = _swimlanes.withCard(
          card.id,
          (c) => c.rescheduled(startDate, endDate),
        );
        _hierarchyItems = _withHierarchyItem(
          card.id,
          (item) => item.rescheduled(startDate, endDate),
        );
      },
      persist: () => _repository.rescheduleItem(card.id, startDate, endDate),
      revertLanes: () => _swimlanes = _swimlanes.withCard(
        card.id,
        (c) => c.rescheduled(card.startDate, card.endDate),
      ),
      revertHierarchy: () => _hierarchyItems = _withHierarchyItem(
        card.id,
        (item) => item.rescheduled(card.startDate, card.endDate),
      ),
      errorMessage: 'Could not reschedule "${card.title}". Try again.',
    );
  }

  /// Sets or clears who the work item identified by [workItemId] is
  /// assigned to. [workItemId] may be a card, a swimlane's own parent item,
  /// a Hierarchy item, or several of those at once (the same work item can
  /// appear in more than one place) — every matching in-memory
  /// representation is updated together. Applies optimistically, then rolls
  /// back if the backend rejects it.
  Future<void> assign(String workItemId, String? userId) {
    final previousUserId = _currentAssigneeOf(workItemId);

    return _optimistic(
      apply: () {
        _swimlanes = _swimlanes.withAssignee(workItemId, userId);
        _hierarchyItems = _withHierarchyItem(
          workItemId,
          (item) => item.assigned(userId),
        );
      },
      persist: () => _repository.assign(workItemId, userId),
      revertLanes: () =>
          _swimlanes = _swimlanes.withAssignee(workItemId, previousUserId),
      revertHierarchy: () => _hierarchyItems = _withHierarchyItem(
        workItemId,
        (item) => item.assigned(previousUserId),
      ),
      errorMessage: 'Could not update the assignee. Try again.',
    );
  }

  /// Replaces the tag list of the work item identified by [workItemId] —
  /// the view-model mirror of [assign], same dual-update-then-rollback
  /// shape (a work item's tags may need updating in both a swimlane card
  /// and a Hierarchy item at once).
  Future<void> setTags(String workItemId, List<String> tags) {
    final previousTags = _currentTagsOf(workItemId);

    return _optimistic(
      apply: () {
        _swimlanes = _swimlanes.withCard(workItemId, (c) => c.tagged(tags));
        _hierarchyItems = _withHierarchyItem(
          workItemId,
          (item) => item.tagged(tags),
        );
      },
      persist: () => _repository.setTags(workItemId, tags),
      revertLanes: () {
        if (previousTags != null) {
          _swimlanes = _swimlanes.withCard(
            workItemId,
            (c) => c.tagged(previousTags),
          );
        }
      },
      revertHierarchy: () {
        if (previousTags != null) {
          _hierarchyItems = _withHierarchyItem(
            workItemId,
            (item) => item.tagged(previousTags),
          );
        }
      },
      errorMessage: 'Could not update the tags. Try again.',
    );
  }

  void clearMoveError() => _moveError = null;

  /// Creates a new work item under [parentId] (null for a new top-level
  /// item) with [statusId], then reloads the current scope to pick it up —
  /// there's no optimistic add, since the created item's id isn't known
  /// until the repository call returns.
  ///
  /// Deliberately not routed through [_changeScope]: a failed create keeps
  /// the board on screen (reported via [moveError]), and it must never
  /// become the [retry] target, since retrying would POST the item again.
  Future<void> createWorkItem({
    required String title,
    String? description,
    required String? parentId,
    required String statusId,
  }) async {
    final scopeId = _breadcrumbs.last.id;
    try {
      await _repository.createWorkItem(
        title: title,
        description: description,
        parentId: parentId,
        statusId: statusId,
      );
    } on ApiException {
      _moveError = 'Could not create "$title". Try again.';
      notifyIfActive();
      return;
    }

    // If the user navigated elsewhere while the create was in flight, that
    // navigation already loaded the scope they're now looking at.
    if (_breadcrumbs.last.id == scopeId) await refreshCurrentScope();
  }

  /// Sets the board's time-frame filter. Either bound may be null (open on
  /// that side); an inverted range is swapped.
  void setTimeFilter({DateTime? start, DateTime? end}) =>
      _setFilters(_filters.withTimeRange(start: start, end: end));

  void clearTimeFilter() => setTimeFilter();

  /// True if [card]'s schedule overlaps the time-frame filter; a missing
  /// bound on either side is open-ended.
  bool matchesTimeFilter(WorkItemCard card) =>
      _filters.matchesTimeWindow(card.startDate, card.endDate);

  void setSearchQuery(String query) =>
      _setFilters(_filters.withSearchQuery(query));

  void clearSearchQuery() => setSearchQuery('');

  /// True if [card]'s title, description, or any tag contains [searchQuery]
  /// (case-insensitive). Always true when the query is empty.
  bool matchesSearch(WorkItemCard card) =>
      _filters.matchesSearchText(card.title, card.description, card.tags);

  /// Shows or hides [tag] from the current tag filter. An empty selection
  /// means no tag filter is applied.
  void toggleTagFilter(String tag) => _setFilters(_filters.withTagToggled(tag));

  /// True if no tag filter is selected, or [tags] contains at least one
  /// selected tag, ignoring case.
  bool matchesTagFilter(List<String> tags) => _filters.matchesTags(tags);

  /// Whether [card] should be shown on the board under the current
  /// time-frame filter, search query, and tag filter together.
  bool cardVisible(WorkItemCard card) => _filters.cardVisible(card);

  /// Whether [item] should be shown in the Hierarchy view; unlike
  /// [cardVisible] this also checks [hiddenStatusIds].
  bool hierarchyItemVisible(HierarchyItem item) =>
      _filters.hierarchyItemVisible(item);

  /// Shows or hides [statusId]'s column. Purely a display toggle.
  void toggleStatusVisibility(String statusId) =>
      _setFilters(_filters.withStatusToggled(statusId));

  void setSortOption(CardSortOption option) =>
      _setFilters(_filters.withSortOption(option));

  /// Null for [CardSortOption.manual] — see [sortComparator].
  Comparator<WorkItemCard>? get cardComparator => sortComparator(
    _filters.sortOption,
    title: (card) => card.title,
    startDate: (card) => card.startDate,
    endDate: (card) => card.endDate,
  );

  /// The Hierarchy view's mirror of [cardComparator], applied within each
  /// sibling group.
  Comparator<HierarchyItem>? get hierarchyComparator => sortComparator(
    _filters.sortOption,
    title: (item) => item.title,
    startDate: (item) => item.startDate,
    endDate: (item) => item.endDate,
  );

  void _setFilters(BoardFilters filters) {
    _filters = filters;
    notifyIfActive();
  }

  /// Runs a scope load. [fetch] does the network work and returns a
  /// closure that applies the result; it's only applied if no newer scope
  /// load started in the meantime, so clicking a breadcrumb then quickly
  /// drilling into a card can't leave the lanes and breadcrumbs describing
  /// different scopes.
  Future<void> _changeScope(
    Future<void Function()> Function() fetch, {
    required String errorMessage,
  }) async {
    final generation = ++_scopeGeneration;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();

    try {
      final apply = await fetch();
      if (generation != _scopeGeneration) return;
      apply();
      _retry = _noRetry;
    } on ApiException {
      if (generation != _scopeGeneration) return;
      _loadError = errorMessage;
      // Only a failure is retryable: replaying a successful drill-in would
      // push its breadcrumb a second time.
      _retry = () => _changeScope(fetch, errorMessage: errorMessage);
    } finally {
      if (generation == _scopeGeneration) {
        _isLoading = false;
        notifyIfActive();
      }
    }
  }

  /// Applies a change locally, persists it, and on failure reverts just
  /// that change (per item, not a whole-list snapshot, so a failed move
  /// can't undo a different move that succeeded in the meantime). A revert
  /// is skipped if the data it would touch has been reloaded since — the
  /// reload already reflects the server.
  Future<void> _optimistic({
    required void Function() apply,
    required Future<void> Function() persist,
    required void Function() revertLanes,
    void Function()? revertHierarchy,
    required String errorMessage,
  }) async {
    final scopeGeneration = _scopeGeneration;
    final hierarchyGeneration = _hierarchyGeneration;
    apply();
    notifyIfActive();

    try {
      await persist();
    } on ApiException {
      if (scopeGeneration == _scopeGeneration) revertLanes();
      if (hierarchyGeneration == _hierarchyGeneration) revertHierarchy?.call();
      _moveError = errorMessage;
      notifyIfActive();
    }
  }

  void _applyScope(BoardData data) {
    _statuses = [...data.statuses]..sort((a, b) => a.order.compareTo(b.order));
    _statusById = {for (final status in _statuses) status.id: status};
    _swimlanes = data.swimlanes;
  }

  List<HierarchyItem> _withHierarchyItem(
    String id,
    HierarchyItem Function(HierarchyItem) update,
  ) => [
    for (final item in _hierarchyItems)
      if (item.id == id) update(item) else item,
  ];

  String? _currentAssigneeOf(String workItemId) =>
      _swimlanes.assigneeOf(workItemId) ??
      _hierarchyItems
          .where((item) => item.id == workItemId)
          .firstOrNull
          ?.assignedToUserId;

  List<String>? _currentTagsOf(String workItemId) =>
      _swimlanes.tagsOf(workItemId) ??
      _hierarchyItems.where((item) => item.id == workItemId).firstOrNull?.tags;
}
