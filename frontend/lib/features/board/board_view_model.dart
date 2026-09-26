import 'dart:ui' show Color;

import 'package:injectable/injectable.dart';
import 'package:weaver/core/dates/calendar_days.dart';
import 'package:weaver/core/network/api_exception.dart';
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
  // Lower-cased: tags are matched case-insensitively, the same way
  // availableTags merges "Urgent" and "urgent" into one chip.
  final Set<String> _selectedTagFilters = {};
  List<HierarchyItem> _hierarchyItems = const [];
  bool _isHierarchyLoading = false;
  String? _hierarchyLoadError;
  bool _hierarchyLoaded = false;
  List<AuthUser> _users = const [];

  // Bumped by every scope/hierarchy load so a slower, older response can't
  // overwrite a newer one, and so optimistic rollbacks know when the data
  // they captured has since been replaced.
  int _scopeGeneration = 0;
  int _hierarchyGeneration = 0;

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

  /// Whether [tag] (any casing) is selected in the Filters dialog. With
  /// nothing selected no tag filter is applied — unlike [hiddenStatusIds],
  /// this is a filter you opt into, not out of.
  bool isTagFilterSelected(String tag) =>
      _selectedTagFilters.contains(tag.toLowerCase());

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
      // Best-effort: a user directory failure shouldn't block the board
      // itself from loading — assignee initials just won't show.
      List<AuthUser> users;
      try {
        users = await _repository.loadUsers();
      } on ApiException {
        users = const [];
      }
      return () {
        _applyScope(data);
        _breadcrumbs = [ScopeCrumb(id: rootScopeId, title: 'Board')];
        _users = users;
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
    if (card.statusId == newStatusId || !_hasLane(card.parentId)) {
      return Future.value();
    }

    return _optimistic(
      apply: () {
        _swimlanes = _withCard(
          card.id,
          (c) => c.copyWith(statusId: newStatusId),
        );
        _hierarchyItems = _withHierarchyItem(
          card.id,
          (item) => item.withStatus(newStatusId),
        );
      },
      persist: () => _repository.changeStatus(card.id, newStatusId),
      revertLanes: () => _swimlanes = _withCard(
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
        !_hasLane(card.parentId) ||
        !_hasLane(newParentId)) {
      return Future.value();
    }

    final originalIndex = _swimlanes
        .firstWhere((lane) => lane.parentId == card.parentId)
        .cards
        .indexWhere((c) => c.id == card.id);

    return _optimistic(
      apply: () {
        _swimlanes = _withCardMovedToLane(card.id, card.parentId, newParentId);
        _hierarchyItems = _withHierarchyItem(
          card.id,
          (item) => item.movedToParent(newParentId),
        );
      },
      persist: () => _repository.reparentItem(card.id, newParentId),
      revertLanes: () => _swimlanes = _withCardMovedToLane(
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
    if (!_hasLane(card.parentId)) return Future.value();

    return _optimistic(
      apply: () {
        _swimlanes = _withCard(
          card.id,
          (c) => c.rescheduled(startDate, endDate),
        );
        _hierarchyItems = _withHierarchyItem(
          card.id,
          (item) => item.rescheduled(startDate, endDate),
        );
      },
      persist: () => _repository.rescheduleItem(card.id, startDate, endDate),
      revertLanes: () => _swimlanes = _withCard(
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
        _swimlanes = _assignedInSwimlanes(workItemId, userId);
        _hierarchyItems = _withHierarchyItem(
          workItemId,
          (item) => item.assigned(userId),
        );
      },
      persist: () => _repository.assign(workItemId, userId),
      revertLanes: () =>
          _swimlanes = _assignedInSwimlanes(workItemId, previousUserId),
      revertHierarchy: () => _hierarchyItems = _withHierarchyItem(
        workItemId,
        (item) => item.assigned(previousUserId),
      ),
      errorMessage: 'Could not update the assignee. Try again.',
    );
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
  Future<void> setTags(String workItemId, List<String> tags) {
    final previousTags = _currentTagsOf(workItemId);

    return _optimistic(
      apply: () {
        _swimlanes = _withCard(workItemId, (c) => c.tagged(tags));
        _hierarchyItems = _withHierarchyItem(
          workItemId,
          (item) => item.tagged(tags),
        );
      },
      persist: () => _repository.setTags(workItemId, tags),
      revertLanes: () {
        if (previousTags != null) {
          _swimlanes = _withCard(workItemId, (c) => c.tagged(previousTags));
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
  /// that side); passing both null is equivalent to [clearTimeFilter].
  void setTimeFilter({DateTime? start, DateTime? end}) {
    // The pickers prevent an inverted range; swap rather than silently
    // match nothing if one arrives anyway.
    final inverted = start != null && end != null && start.isAfter(end);
    _filterStart = inverted ? end : start;
    _filterEnd = inverted ? start : end;
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

    // Compared by calendar day, so both bounds include their whole day
    // whatever the time part of either side.
    final startsInTime =
        _filterEnd == null ||
        itemStart == null ||
        daysBetween(_filterEnd!, itemStart) <= 0;

    final endsInTime =
        _filterStart == null ||
        itemEnd == null ||
        daysBetween(_filterStart!, itemEnd) >= 0;

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
    final key = tag.toLowerCase();
    if (!_selectedTagFilters.remove(key)) {
      _selectedTagFilters.add(key);
    }

    notifyIfActive();
  }

  /// True if no tag filter is selected, or [tags] contains at least one
  /// selected tag, ignoring case.
  bool matchesTagFilter(List<String> tags) =>
      _selectedTagFilters.isEmpty ||
      tags.any((tag) => _selectedTagFilters.contains(tag.toLowerCase()));

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
    _swimlanes = data.swimlanes;
  }

  bool _hasLane(String parentId) =>
      _swimlanes.any((lane) => lane.parentId == parentId);

  List<Swimlane> _withCard(
    String cardId,
    WorkItemCard Function(WorkItemCard) update,
  ) => [
    for (final lane in _swimlanes)
      lane.copyWithCards([
        for (final c in lane.cards)
          if (c.id == cardId) update(c) else c,
      ]),
  ];

  List<HierarchyItem> _withHierarchyItem(
    String id,
    HierarchyItem Function(HierarchyItem) update,
  ) => [
    for (final item in _hierarchyItems)
      if (item.id == id) update(item) else item,
  ];

  /// Moves the card from [fromParentId]'s lane to [toParentId]'s, appended
  /// unless [atIndex] is given. A no-op if the card isn't in the source lane
  /// (e.g. a later move already took it elsewhere).
  List<Swimlane> _withCardMovedToLane(
    String cardId,
    String fromParentId,
    String toParentId, {
    int? atIndex,
  }) {
    final fromLane = _swimlanes
        .where((lane) => lane.parentId == fromParentId)
        .firstOrNull;
    final card = fromLane?.cards.where((c) => c.id == cardId).firstOrNull;
    if (card == null) return _swimlanes;

    final moved = card.movedToParent(toParentId);
    List<WorkItemCard> insertedInto(List<WorkItemCard> cards) {
      final index = (atIndex ?? cards.length).clamp(0, cards.length);
      return [...cards]..insert(index, moved);
    }

    return [
      for (final lane in _swimlanes)
        if (lane.parentId == fromParentId)
          lane.copyWithCards([
            for (final c in lane.cards)
              if (c.id != cardId) c,
          ])
        else if (lane.parentId == toParentId)
          lane.copyWithCards(insertedInto(lane.cards))
        else
          lane,
    ];
  }

  String? _currentAssigneeOf(String workItemId) {
    for (final lane in _swimlanes) {
      if (lane.parentId == workItemId) return lane.assignedToUserId;
      for (final card in lane.cards) {
        if (card.id == workItemId) return card.assignedToUserId;
      }
    }
    return _hierarchyItems
        .where((item) => item.id == workItemId)
        .firstOrNull
        ?.assignedToUserId;
  }

  List<String>? _currentTagsOf(String workItemId) {
    for (final lane in _swimlanes) {
      for (final card in lane.cards) {
        if (card.id == workItemId) return card.tags;
      }
    }
    return _hierarchyItems
        .where((item) => item.id == workItemId)
        .firstOrNull
        ?.tags;
  }
}
