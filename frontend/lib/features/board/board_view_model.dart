import 'dart:async';
import 'dart:ui' show Color;

import 'package:flutter/foundation.dart';

import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/network/parallel_api_errors.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/data/last_space_store.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/models/hierarchy_node.dart';
import 'package:weaver/features/board/models/scope_crumb.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/swimlane_items.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/state/board_filters.dart';
import 'package:weaver/features/board/state/hierarchy_roll_ups.dart';
import 'package:weaver/features/board/state/hierarchy_tree_builder.dart';
import 'package:weaver/features/board/state/swimlane_edits.dart';
import 'package:weaver/features/board/state/tag_additions.dart';
import 'package:weaver/features/board/state/user_directory.dart';
import 'package:weaver/features/board/state/work_item_sorting.dart';
import 'package:weaver/shared/data/work_item_conflict.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/tag_match.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_query.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/models/work_item_type.dart';

Future<void> _noRetry() => Future.value();

/// Orchestrates the board: which Space and scope are shown (loading,
/// switching Space, drill-down, breadcrumbs, retry) and optimistic
/// mutations. Which items match the header's filters is asked of the
/// server's query (ADR-0006); sorting, tree building, lane edits and user
/// lookups live in `state/` as pure units.
///
/// The items themselves live in the shared [WorkItemStore]; this keeps only
/// the layout (which lanes, which cards in which order, which items the
/// Hierarchy shows) and redraws from the store whenever it changes, so an
/// edit made anywhere, including the detail dialog, shows on every layout.
///
/// An item edit doesn't notify this view model's own listeners, which
/// rebuild the whole board. Each lane has its own listenable
/// ([swimlaneListenables]) that fires only when that lane changes, and
/// [itemChanges] fires for layouts drawn from every item (Hierarchy,
/// Roadmap, the tag list).
@injectable
class BoardViewModel extends ViewModel {
  BoardViewModel(this._repository, this._items, this._lastSpace) {
    _items.addListener(_onItemsChanged);
  }

  final BoardRepository _repository;
  final WorkItemStore _items;
  final LastSpaceStore _lastSpace;

  List<Space> _spaces = const [];
  Space? _currentSpace;
  bool _spacesLoaded = false;

  List<WorkItemStatus> _statuses = const [];
  Map<String, WorkItemStatus> _statusById = const {};
  Map<String, WorkItemType> _typeById = const {};
  List<Swimlane> _swimlanes = const [];
  List<ValueNotifier<Swimlane>> _laneNotifiers = const [];
  final ValueNotifier<int> _itemRevision = ValueNotifier(0);
  List<ScopeCrumb> _breadcrumbs = const [];
  bool _isLoading = true;
  String? _loadError;
  String? _moveError;
  Future<void> Function() _retry = _noRetry;
  BoardFilters _filters = const BoardFilters();
  List<String> _hierarchyItemIds = const [];
  List<CustomField> _spaceFields = const [];
  bool _isHierarchyLoading = false;
  String? _hierarchyLoadError;
  bool _hierarchyLoaded = false;
  UserDirectory _users = UserDirectory.empty;

  // Bumped by every scope/hierarchy load so a slower, older response can't
  // overwrite a newer one, and so optimistic rollbacks know when the data
  // they captured has since been replaced.
  int _scopeGeneration = 0;
  int _hierarchyGeneration = 0;

  // The ids the server matched for the header's filters; null while no
  // filter is set, when every item matches. Bumping the generation drops a
  // slower answer to an older question, e.g. a search typed further since.
  Set<String>? _matchingIds;
  int _matchGeneration = 0;
  bool _matchRefreshScheduled = false;

  // Derived values the views read many times per frame. Every state change
  // notifies, so they're cached until the next notification.
  List<HierarchyNode>? _hierarchyRootsCache;
  List<HierarchyItem>? _hierarchyItemsCache;
  HierarchyRollUps? _hierarchyRollUpsCache;
  List<WorkItemStatus>? _visibleStatusesCache;
  List<String>? _availableTagsCache;

  @override
  void notifyIfActive() {
    _clearItemCaches();
    _visibleStatusesCache = null;
    super.notifyIfActive();
  }

  void _clearItemCaches() {
    _hierarchyRootsCache = null;
    _hierarchyItemsCache = null;
    _hierarchyRollUpsCache = null;
    _availableTagsCache = null;
  }

  @override
  void dispose() {
    _items.removeListener(_onItemsChanged);
    for (final lane in _laneNotifiers) {
      lane.dispose();
    }
    _itemRevision.dispose();
    super.dispose();
  }

  void _onItemsChanged() {
    _clearItemCaches();
    _setSwimlanes(_drawnFromStore(_swimlanes));
    _itemRevision.value++;
    _scheduleMatchRefresh();
  }

  /// Shows [lanes], notifying only the lanes that changed. Adding, removing,
  /// reordering or renaming a lane changes what the rest of the board shows
  /// (the label column, the drill-down targets), so that notifies the whole
  /// board too.
  void _setSwimlanes(List<Swimlane> lanes) {
    // A save's rollback can land after the board was closed, when the lane
    // notifiers are already disposed.
    if (isDisposed) return;
    final reshaped = !_sameLaneHeaders(_swimlanes, lanes);
    _swimlanes = lanes;
    final previousById = {
      for (final notifier in _laneNotifiers) notifier.value.parentId: notifier,
    };
    _laneNotifiers = [
      for (final lane in lanes)
        previousById.remove(lane.parentId) ?? ValueNotifier(lane),
    ];
    for (final dropped in previousById.values) {
      dropped.dispose();
    }
    // Set only once both lists are in place, so a lane listener reading the
    // board sees the new state.
    for (var i = 0; i < lanes.length; i++) {
      _laneNotifiers[i].value = lanes[i];
    }
    if (reshaped) notifyIfActive();
  }

  static bool _sameLaneHeaders(List<Swimlane> a, List<Swimlane> b) {
    if (a.length != b.length) return false;
    for (var i = 0; i < a.length; i++) {
      if (a[i].parentId != b[i].parentId || a[i].title != b[i].title) {
        return false;
      }
    }
    return true;
  }

  /// [lanes] with every lane and card redrawn from the store, keeping their
  /// order. An item the store no longer holds (deleted) is dropped.
  List<Swimlane> _drawnFromStore(List<Swimlane> lanes) => [
    for (final lane in lanes)
      if (_items[lane.parentId] case final laneItem?)
        Swimlane(
          parentId: lane.parentId,
          title: laneItem.title,
          assignedToUserId: laneItem.assignedToUserId,
          cards: [
            for (final card in lane.cards)
              if (_items[card.id] case final item?)
                WorkItemCard.fromWorkItem(item, laneId: lane.parentId),
          ],
        ),
  ];

  List<HierarchyItem> get _hierarchyItems => _hierarchyItemsCache ??= [
    for (final id in _hierarchyItemIds)
      if (_items[id] case final item?) HierarchyItem.fromWorkItem(item),
  ];

  List<WorkItemStatus> get statuses => _statuses;
  List<Swimlane> get swimlanes => _swimlanes;

  /// One listenable per lane in [swimlanes], in the same order, each firing
  /// only when its own lane changes. The list itself changes only along with
  /// a notification of this view model.
  List<ValueListenable<Swimlane>> get swimlaneListenables => _laneNotifiers;

  /// Fires after any item the board holds changes, for layouts that draw
  /// from every item rather than one lane.
  Listenable get itemChanges => _itemRevision;

  List<Space> get spaces => _spaces;

  /// The Space the board, Hierarchy and Roadmap show. Null until [load]
  /// has resolved one, and while no Space exists at all.
  Space? get currentSpace => _currentSpace;

  /// True once a load found no Space at all: there's nothing to show until
  /// an admin creates one.
  bool get hasNoSpaces => _spacesLoaded && _spaces.isEmpty;

  /// Every active user, for building an assignee picker. Empty until
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

  /// The Hierarchy (and Roadmap) view's root nodes, showing the same matches
  /// as the board plus their ancestors, sorted — see [buildHierarchyTree].
  List<HierarchyNode> get hierarchyRoots =>
      _hierarchyRootsCache ??= buildHierarchyTree(
        _hierarchyItems,
        isVisible: hierarchyItemVisible,
        comparator: hierarchyComparator,
      );

  /// The roll-ups of every Hierarchy item: its own value and its sub-items'
  /// total of each field that rolls up. Summed from the store, so they
  /// follow every edit without a reload.
  HierarchyRollUps get hierarchyRollUps => _hierarchyRollUpsCache ??=
      HierarchyRollUps(_hierarchyItems, _spaceFields);

  /// Loads every work item in the current Space for the Hierarchy view, and
  /// the Space's fields for its roll-ups. Independent of the swim-lane
  /// board's current drill-down scope — the Hierarchy view always shows the
  /// Space's whole tree.
  Future<void> loadHierarchy() async {
    final generation = ++_hierarchyGeneration;
    _isHierarchyLoading = true;
    _hierarchyLoadError = null;
    notifyIfActive();

    try {
      final asOf = _items.revision;
      final space = _currentSpace;
      final (items, fields) = space == null
          ? (const <WorkItem>[], const <CustomField>[])
          : await (
              _repository.loadAllItems(space.id),
              _repository.loadCustomFields(space.id),
            ).wait;
      if (generation != _hierarchyGeneration) return;
      _hierarchyItemIds = [for (final item in items) item.id];
      _spaceFields = fields;
      _items.putAll(items, asOf: asOf);
    } on ParallelWaitError<Object?, Object?> catch (e) {
      rethrowNonApiErrors(asyncErrorsOf(e.errors));
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

  /// Reloads the users, the Spaces, the current scope (and with it the
  /// statuses) and, if loaded, the hierarchy — after the Admin page, where
  /// Spaces can be added or renamed, statuses renamed, recoloured or
  /// deleted and users renamed or deactivated.
  Future<void> refreshAfterAdminChanges() async {
    try {
      final users = await _repository.loadUsers();
      if (isDisposed) return;
      _users = UserDirectory(users);
    } on ApiException {
      // Keep the directory already loaded; only names and initials go stale.
    }
    // With no Space shown there's no scope to refresh, and the first Space
    // an admin just created should open.
    if (_currentSpace == null) return load();

    await _refreshSpaces();
    await refreshCurrentScope();
    await refreshHierarchyIfLoaded();
  }

  /// Picks up renamed Spaces, keeping the current one (spaces can't be
  /// deleted, so it's still there).
  Future<void> _refreshSpaces() async {
    try {
      final spaces = await _repository.loadSpaces();
      if (isDisposed) return;
      _spaces = spaces;
      final current = _spaceById(spaces, _currentSpace?.id) ?? _currentSpace;
      _currentSpace = current;
      if (current != null && _breadcrumbs.isNotEmpty) {
        _breadcrumbs = [_rootCrumb(current), ..._breadcrumbs.skip(1)];
      }
      notifyIfActive();
    } on ApiException {
      // Keep the Spaces already loaded; only a renamed one's name goes stale.
    }
  }

  static Space? _spaceById(List<Space> spaces, String? id) {
    for (final space in spaces) {
      if (space.id == id) return space;
    }
    return null;
  }

  static ScopeCrumb _rootCrumb(Space space) =>
      ScopeCrumb(id: null, title: space.name);

  /// Re-fetches the Hierarchy view's data only if it's already been loaded
  /// once — e.g. after a delete elsewhere on the board, so a still-unopened
  /// Hierarchy tab doesn't trigger a needless network call.
  Future<void> refreshHierarchyIfLoaded() =>
      _hierarchyLoaded ? loadHierarchy() : Future.value();

  String? statusNameFor(String statusId) => _statusById[statusId]?.name;

  Color? statusColorFor(String statusId) => _statusById[statusId]?.color;

  /// The colour of the type [typeId] names, marking an item's kind on the
  /// board — null when it has no type or the type is unknown.
  Color? typeColorFor(String? typeId) => _typeById[typeId]?.color;

  /// The uppercased first letter of the assigned user's username, for a
  /// small avatar — null if [userId] is null or unresolved (e.g. the user
  /// directory hasn't loaded yet).
  String? assigneeInitialFor(String? userId) => _users.initialFor(userId);

  /// The assigned user's full username — null on the same terms as
  /// [assigneeInitialFor].
  String? usernameFor(String? userId) => _users.usernameFor(userId);

  /// Loads the Spaces and opens the last-used one (or the first, if that's
  /// gone or none was remembered) at its top level.
  Future<void> load() => _changeScope(
    () async {
      final asOf = _items.revision;
      final spaces = await _repository.loadSpaces();
      final space = _chooseSpace(spaces, await _lastSpace.readSpaceId());
      final data = space == null
          ? null
          : await _repository.loadBoard(space.id, null);
      if (isDisposed) return () {};
      // Best-effort: a user directory failure shouldn't block the board
      // itself from loading — assignee initials just won't show.
      List<User> users;
      try {
        users = await _repository.loadUsers();
      } on ApiException {
        users = const [];
      }
      return () {
        _spaces = spaces;
        _spacesLoaded = true;
        _currentSpace = space;
        if (data == null) {
          _setSwimlanes(const []);
          _breadcrumbs = const [];
        } else {
          _applyScope(data, asOf);
          _breadcrumbs = [_rootCrumb(space!)];
        }
        _users = UserDirectory(users);
      };
    },
    errorMessage:
        'Could not load the board. Check your connection and try again.',
  );

  static Space? _chooseSpace(List<Space> spaces, String? lastUsedId) =>
      _spaceById(spaces, lastUsedId) ?? (spaces.isEmpty ? null : spaces.first);

  /// Shows [space]'s top level on the board (and in the Hierarchy and
  /// Roadmap, if they were loaded), and remembers it for the next launch.
  Future<void> switchSpace(Space space) async {
    if (space.id == _currentSpace?.id) return;

    var hierarchyWanted = false;
    await _changeScope(() async {
      final asOf = _items.revision;
      final data = await _repository.loadBoard(space.id, null);
      return () {
        _currentSpace = space;
        _forgetMatches();
        _applyScope(data, asOf);
        _breadcrumbs = [_rootCrumb(space)];
        hierarchyWanted = _clearHierarchy();
      };
    }, errorMessage: 'Could not open "${space.name}". Try again.');
    if (isDisposed || _currentSpace?.id != space.id) return;

    _scheduleMatchRefresh();
    await _lastSpace.writeSpaceId(space.id);
    if (hierarchyWanted) await loadHierarchy();
  }

  /// Drops the old Space's Hierarchy/Roadmap items so they never show under
  /// the new Space, and discards a load of them still in flight. Returns
  /// whether the hierarchy had been asked for, so it can be reloaded.
  bool _clearHierarchy() {
    final wanted = _hierarchyLoaded || _isHierarchyLoading;
    _hierarchyGeneration++;
    _hierarchyItemIds = const [];
    _isHierarchyLoading = false;
    return wanted;
  }

  /// Re-attempts the scope load that last failed. A no-op once a load has
  /// succeeded; use [refreshCurrentScope] to reload what's shown.
  Future<void> retry() => _retry();

  /// Reloads the scope currently shown, e.g. after an item was deleted from
  /// the detail dialog. Keeps the breadcrumb trail as it is.
  Future<void> refreshCurrentScope() {
    if (_breadcrumbs.isEmpty) return load();

    final current = _breadcrumbs.last;
    final space = _currentSpace!;
    return _changeScope(
      () async {
        final asOf = _items.revision;
        final data = await _repository.loadBoard(space.id, current.id);
        return () => _applyScope(data, asOf);
      },
      errorMessage:
          'Could not refresh the board. Check your connection and try again.',
    );
  }

  /// The current scope's swimlanes, as the scopes the board can drill one
  /// level further into.
  List<ScopeCrumb> get drillDownTargets => [
    for (final lane in _swimlanes)
      ScopeCrumb(id: lane.parentId, title: lane.title),
  ];

  /// Re-scopes the board to [card]'s own children — they become the new
  /// swimlanes. Pushes [card] onto the breadcrumb trail.
  Future<void> drillInto(WorkItemCard card) =>
      drillIntoScope(ScopeCrumb(id: card.id, title: card.title));

  /// Re-scopes the board to [target]'s own children, typically one of
  /// [drillDownTargets], and pushes [target] onto the breadcrumb trail.
  Future<void> drillIntoScope(ScopeCrumb target) => _changeScope(() async {
    final asOf = _items.revision;
    final data = await _repository.loadBoard(_currentSpace!.id, target.id);
    return () {
      _applyScope(data, asOf);
      _breadcrumbs = [..._breadcrumbs, target];
    };
  }, errorMessage: 'Could not open "${target.title}". Try again.');

  /// Jumps back to the breadcrumb at [index], discarding any deeper
  /// entries. A no-op if [index] is already the current scope.
  Future<void> navigateToBreadcrumb(int index) {
    if (index == _breadcrumbs.length - 1) {
      return Future.value();
    }

    final trail = _breadcrumbs.sublist(0, index + 1);
    final target = trail.last;

    return _changeScope(() async {
      final asOf = _items.revision;
      final data = await _repository.loadBoard(_currentSpace!.id, target.id);
      return () {
        _applyScope(data, asOf);
        _breadcrumbs = trail;
      };
    }, errorMessage: 'Could not go back to "${target.title}". Try again.');
  }

  /// Moves [card] to [newStatusId], within whichever swimlane already owns
  /// it, placing it directly before [beforeCardId] in that cell, or at the
  /// end of the cell when that is null. [newStatusId] may be the card's own
  /// status, which only reorders it. There is no swimlane parameter to
  /// pass — that's what makes a cross-swimlane move structurally impossible
  /// here, the same way the backend's `ChangeStatus`/`Reparent` split keeps
  /// a status change from ever touching `ParentId`. Applies the move
  /// optimistically, then puts the card back if the backend rejects it.
  Future<void> moveCard(
    WorkItemCard card,
    String newStatusId, {
    String? beforeCardId,
  }) {
    if (!_swimlanes.hasLane(card.parentId)) return Future.value();

    final originalCellIds = _swimlanes.cellIdsOf(card.id);
    final originalIndex = originalCellIds.indexOf(card.id);
    final originalAnchor = originalIndex > 0
        ? originalCellIds[originalIndex - 1]
        : null;
    final afterId = _swimlanes.rankAnchorForDrop(
      card.id,
      newStatusId,
      beforeCardId: beforeCardId,
    );
    final isAlreadyThere =
        newStatusId == card.statusId && afterId == originalAnchor;
    if (isAlreadyThere) return Future.value();

    return _save(
      card.id,
      optimistic: (item) => item.withStatus(newStatusId),
      send: (version) => _repository.changeStatus(
        card.id,
        newStatusId,
        afterId: afterId,
        expectedVersion: version,
      ),
      applyLayout: () => _setSwimlanes(
        _swimlanes.withCardPlaced(card.id, newStatusId, afterId: afterId),
      ),
      revertLayout: () => _setSwimlanes(
        _swimlanes.withCardRestored(
          card.id,
          card.statusId,
          originalCellIds: originalCellIds,
        ),
      ),
      title: card.title,
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

    return _save(
      card.id,
      optimistic: (item) => item.movedToParent(newParentId),
      send: (version) => _repository.reparentItem(
        card.id,
        newParentId,
        expectedVersion: version,
      ),
      applyLayout: () => _setSwimlanes(
        _swimlanes.withCardMovedToLane(card.id, card.parentId, newParentId),
      ),
      revertLayout: () => _setSwimlanes(
        _swimlanes.withCardMovedToLane(
          card.id,
          newParentId,
          card.parentId,
          atIndex: originalIndex,
        ),
      ),
      title: card.title,
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

    return _save(
      card.id,
      optimistic: (item) => item.rescheduled(startDate, endDate),
      send: (version) => _repository.rescheduleItem(
        card.id,
        startDate,
        endDate,
        expectedVersion: version,
      ),
      title: card.title,
      errorMessage: 'Could not reschedule "${card.title}". Try again.',
    );
  }

  /// Sets or clears who the work item identified by [workItemId] is
  /// assigned to. [workItemId] may be a card, a swimlane's own parent item
  /// or a Hierarchy item; every layout showing it follows. Applies
  /// optimistically, then rolls back if the backend rejects it.
  Future<void> assign(String workItemId, String? userId) => _save(
    workItemId,
    optimistic: (item) => item.assigned(userId),
    send: (version) =>
        _repository.assign(workItemId, userId, expectedVersion: version),
    errorMessage: 'Could not update the assignee. Try again.',
  );

  /// Replaces the tag list of the work item identified by [workItemId],
  /// the same way [assign] does.
  Future<void> setTags(String workItemId, List<String> tags) => _save(
    workItemId,
    optimistic: (item) => item.tagged(tags),
    send: (version) =>
        _repository.setTags(workItemId, tags, expectedVersion: version),
    errorMessage: 'Could not update the tags. Try again.',
  );

  /// Adds [tags] to the work item identified by [workItemId] — the quick
  /// add from a card, Hierarchy row or Roadmap row. Tags it already has
  /// (ignoring case) and blank entries are dropped; if nothing is left to
  /// add, nothing is sent.
  Future<void> addTags(String workItemId, List<String> tags) {
    final current = _items[workItemId]?.tags ?? const [];
    final updated = withTagsAdded(current, tags);
    if (updated.length == current.length) return Future.value();
    return setTags(workItemId, updated);
  }

  void clearMoveError() => _moveError = null;

  /// Creates a new work item under [parentId] (null for a new top-level
  /// item of the current Space) with [statusId], then reloads the current scope to pick it up —
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
    final spaceId = _currentSpace!.id;
    try {
      await _repository.createWorkItem(
        title: title,
        description: description,
        spaceId: spaceId,
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
    if (!isDisposed &&
        _currentSpace?.id == spaceId &&
        _breadcrumbs.last.id == scopeId) {
      await refreshCurrentScope();
    }
  }

  /// Sets the board's time-frame filter. Either bound may be null (open on
  /// that side); an inverted range is swapped.
  void setTimeFilter({DateTime? start, DateTime? end}) =>
      _setFilters(_filters.withTimeRange(start: start, end: end));

  void clearTimeFilter() => setTimeFilter();

  void setSearchQuery(String query) =>
      _setFilters(_filters.withSearchQuery(query));

  void clearSearchQuery() => setSearchQuery('');

  /// Shows or hides [tag] from the current tag filter. An empty selection
  /// means no tag filter is applied.
  void toggleTagFilter(String tag) => _setFilters(_filters.withTagToggled(tag));

  /// Whether the selected tags match an item with any of them or only one
  /// with all of them.
  TagMatch get tagMatch => _filters.tagMatch;

  void setTagMatch(TagMatch match) => _setFilters(_filters.withTagMatch(match));

  bool _matches(String itemId) => _matchingIds?.contains(itemId) ?? true;

  /// Whether [card] is one the server matched for the header's time range,
  /// search, tags and done-hiding. Hidden statuses aren't checked here: the board only
  /// draws visible columns.
  bool cardVisible(WorkItemCard card) => _matches(card.id);

  /// Whether [item] should be shown in the Hierarchy view; unlike
  /// [cardVisible] this also checks [hiddenStatusIds].
  bool hierarchyItemVisible(HierarchyItem item) =>
      !_filters.hiddenStatusIds.contains(item.statusId) && _matches(item.id);

  /// Shows or hides [statusId]'s column. Purely a display toggle.
  void toggleStatusVisibility(String statusId) =>
      _setFilters(_filters.withStatusToggled(statusId));

  void setSortOption(CardSortOption option) =>
      _setFilters(_filters.withSortOption(option));

  /// Whether Done items completed longer ago than the current Space's
  /// done-hiding period show. Off by default, and like the other header
  /// filters it isn't remembered across launches.
  bool get showDone => _filters.showDone;

  void setShowDone(bool showDone) =>
      _setFilters(_filters.withShowDone(showDone));

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
    final before = _activeQuery;
    _filters = filters;
    notifyIfActive();
    if (_activeQuery != before) unawaited(_refreshMatches());
  }

  /// The question the header's filters put to the server for the current
  /// Space; null while no Space is shown.
  WorkItemQuery? get _activeQuery => switch (_currentSpace) {
    final space? => _filters.toQuery(space.id),
    null => null,
  };

  /// Asks again once the current burst of item changes settles, since an
  /// edit (here, in the detail dialog or a reload) can change what matches.
  void _scheduleMatchRefresh() {
    final filtering = _activeQuery?.isUnfiltered == false;
    if (_matchRefreshScheduled || !filtering) return;
    _matchRefreshScheduled = true;
    scheduleMicrotask(() {
      _matchRefreshScheduled = false;
      if (!isDisposed) unawaited(_refreshMatches());
    });
  }

  /// Drops the matches for the Space being left, and any answer still on
  /// its way for it, so they can't hide the new Space's items.
  void _forgetMatches() {
    _matchGeneration++;
    _matchingIds = null;
  }

  /// Asks the server which items of the current Space match the header's
  /// filters (ADR-0006). With no filter set every item matches, so nothing
  /// is asked. A failure keeps the last answer and reports it once.
  Future<void> _refreshMatches() async {
    final generation = ++_matchGeneration;
    final query = _activeQuery;
    if (query == null || query.isUnfiltered) {
      if (_matchingIds == null) return;
      _matchingIds = null;
      notifyIfActive();
      return;
    }

    try {
      final matched = await _repository.queryItems(query);
      if (generation != _matchGeneration || isDisposed) return;
      final ids = {for (final item in matched) item.id};
      // Every edit asks again, and the answer is usually the same. Notifying
      // then would redraw the whole board, undoing the per-lane rebuilds.
      if (setEquals(ids, _matchingIds)) return;
      _matchingIds = ids;
    } on ApiException {
      if (generation != _matchGeneration || isDisposed) return;
      _moveError =
          'Could not apply the filters. Check your connection and try again.';
    }
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

  /// Saves a change to [id] through the store, which shows [optimistic] at
  /// once, sends the item's version and undoes [optimistic] if the save
  /// fails. [applyLayout] and [revertLayout] do the same for where the card
  /// sits on the board; the revert undoes just this change, by id, and is
  /// skipped if the scope was reloaded since, as the reload already shows
  /// the server's layout.
  Future<void> _save(
    String id, {
    required WorkItemEdit optimistic,
    required Future<WorkItem> Function(int expectedVersion) send,
    void Function()? applyLayout,
    void Function()? revertLayout,
    String? title,
    required String errorMessage,
  }) async {
    final scopeGeneration = _scopeGeneration;
    applyLayout?.call();
    try {
      await _items.write(
        id,
        optimistic: optimistic,
        send: send,
        reload: () => _repository.getItem(id),
      );
    } on WorkItemConflict catch (conflict) {
      if (scopeGeneration == _scopeGeneration) {
        revertLayout?.call();
        _placeInLane(id, conflict.latest);
      }
      _moveError = _conflictMessage(title ?? _items[id]?.title, conflict);
      notifyIfActive();
    } on ApiException {
      if (scopeGeneration == _scopeGeneration) revertLayout?.call();
      _moveError = errorMessage;
      notifyIfActive();
    }
  }

  /// After a conflict reload, someone else may have moved the card to
  /// another parent: show it in that lane, or not at all if this board
  /// doesn't show that lane. Its status needs nothing, since cells are
  /// drawn from the stored status.
  void _placeInLane(String id, WorkItem? latest) {
    final laneId = _swimlanes.laneIdOf(id);
    final parentId = latest?.parentId;
    if (latest == null || laneId == null || parentId == laneId) return;
    _setSwimlanes(
      parentId != null && _swimlanes.hasLane(parentId)
          ? _swimlanes.withCardMovedToLane(id, laneId, parentId)
          : _swimlanes.withoutCard(id),
    );
  }

  static String _conflictMessage(String? title, WorkItemConflict conflict) {
    final what = title == null ? 'this item' : '"$title"';
    final notSaved =
        'Someone else changed $what first, so your change was not saved.';
    return conflict.latest == null
        ? '$notSaved Refresh to see the latest version.'
        : '$notSaved It now shows the latest version.';
  }

  void _applyScope(BoardData data, int asOf) {
    _statuses = [...data.statuses]..sort((a, b) => a.order.compareTo(b.order));
    _statusById = {for (final status in _statuses) status.id: status};
    _typeById = {for (final type in data.types) type.id: type};
    _items.putAll([
      for (final lane in data.swimlanes) ...[lane.lane, ...lane.cards],
    ], asOf: asOf);
    _setSwimlanes(
      _drawnFromStore([for (final lane in data.swimlanes) _layoutOf(lane)]),
    );
  }

  static Swimlane _layoutOf(SwimlaneItems lane) => Swimlane(
    parentId: lane.lane.id,
    title: lane.lane.title,
    assignedToUserId: lane.lane.assignedToUserId,
    cards: [
      for (final card in lane.cards)
        WorkItemCard.fromWorkItem(card, laneId: lane.lane.id),
    ],
  );
}
