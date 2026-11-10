import 'dart:async';

import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/core/presentation/view_model.dart';
import 'package:weaver/features/board/data/backlog_repository.dart';
import 'package:weaver/features/board/models/backlog_group.dart';
import 'package:weaver/shared/data/work_item_conflict.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_query.dart';

/// The board's Backlog tab: every item of the shown Spaces that isn't Done
/// and has no dates (ADR-0003), grouped by parent, where it can be given
/// dates. Which items are in the Backlog is the server's decision: after
/// any save, here or anywhere else, it's asked again, so an item given
/// dates leaves the list.
///
/// Items are drawn from the shared [WorkItemStore], so an edit made in the
/// detail dialog shows here at once.
@injectable
class BacklogViewModel extends ViewModel {
  BacklogViewModel(this._repository, this._items) {
    _items
      ..addListener(_onItemsChanged)
      ..addSaveListener(_onItemSaved);
  }

  final BacklogRepository _repository;
  final WorkItemStore _items;

  List<Space> _spaces = const [];

  // Whether a save asks again: only while the Backlog tab is shown, since
  // every visit loads afresh anyway.
  bool _refreshing = false;
  List<String> _itemIds = const [];
  bool _isLoading = false;
  bool _hasLoaded = false;
  String? _loadError;
  String? _actionError;
  int _loadGeneration = 0;
  bool _reloadScheduled = false;
  List<BacklogGroup>? _groupsCache;

  /// Only true for the first load of a set of Spaces; later refreshes keep
  /// showing the list.
  bool get isLoading => _isLoading && !_hasLoaded;
  String? get loadError => _loadError;

  /// Why the last date change failed, for a snackbar; cleared by
  /// [clearActionError].
  String? get actionError => _actionError;

  void clearActionError() => _actionError = null;

  /// The top-level Backlog items first (one group per Space while several
  /// are shown), then the rest under their parents, by the parent's title.
  List<BacklogGroup> get groups => _groupsCache ??= _group();

  @override
  void notifyIfActive() {
    _groupsCache = null;
    super.notifyIfActive();
  }

  @override
  void dispose() {
    _items
      ..removeListener(_onItemsChanged)
      ..removeSaveListener(_onItemSaved);
    super.dispose();
  }

  /// Loads [spaces]' Backlog, replacing what's shown once it arrives. A
  /// load for other Spaces started since wins.
  Future<void> load(List<Space> spaces) async {
    final generation = ++_loadGeneration;
    if (!_sameSpaces(spaces, _spaces)) _hasLoaded = false;
    _spaces = spaces;
    _refreshing = true;
    _isLoading = true;
    _loadError = null;
    notifyIfActive();
    try {
      final asOf = _items.revision;
      final items = await _repository.queryItems(
        WorkItemQuery(
          spaceIds: {for (final space in spaces) space.id},
          backlogOnly: true,
        ),
      );
      final parents = await _missingParents(items);
      if (generation != _loadGeneration || isDisposed) return;
      _items.putAll([...items, ...parents], asOf: asOf);
      _itemIds = [for (final item in items) item.id];
      _hasLoaded = true;
    } on ApiException {
      if (generation != _loadGeneration || isDisposed) return;
      _loadError = 'Could not load the Backlog. Check your connection.';
    } finally {
      if (generation == _loadGeneration) {
        _isLoading = false;
        notifyIfActive();
      }
    }
  }

  /// Stops asking again after saves, once the Backlog tab is left. The next
  /// [load] starts again.
  void stopRefreshing() => _refreshing = false;

  /// Sets [workItemId]'s dates. True when saved; it then leaves the Backlog
  /// once the server confirms it's no longer listed there.
  Future<bool> setDates(
    String workItemId,
    DateTime? startDate,
    DateTime? endDate,
  ) async {
    try {
      await _items.write(
        workItemId,
        optimistic: (item) => item.rescheduled(startDate, endDate),
        send: (version) => _repository.rescheduleItem(
          workItemId,
          startDate,
          endDate,
          expectedVersion: version,
        ),
        reload: _repository.getItem,
      );
      return true;
    } on WorkItemConflict {
      _actionError =
          'Someone else changed this item first, so the dates were not saved. '
          'It now shows the latest version.';
    } on ApiException {
      _actionError = 'Could not set the dates. Try again.';
    }
    notifyIfActive();
    return false;
  }

  /// The parents of [items] that neither the store nor [items] hold, to
  /// title the groups with.
  Future<List<WorkItem>> _missingParents(List<WorkItem> items) {
    final listed = {for (final item in items) item.id};
    return Future.wait([
      for (final parentId in {for (final item in items) item.parentId})
        if (parentId != null &&
            !listed.contains(parentId) &&
            _items[parentId] == null)
          _repository.getItem(parentId),
    ]);
  }

  List<BacklogGroup> _group() {
    final byParent = <String?, List<WorkItem>>{};
    for (final id in _itemIds) {
      if (_items[id] case final item?) {
        (byParent[item.parentId] ??= []).add(item);
      }
    }
    final topLevel = byParent.remove(null) ?? const [];
    final underParents = [
      for (final MapEntry(key: parentId, value: items) in byParent.entries)
        BacklogGroup(parent: _items[parentId!], items: items),
    ]..sort((a, b) => _titleOf(a).compareTo(_titleOf(b)));
    return [..._topLevelGroups(topLevel), ...underParents];
  }

  List<BacklogGroup> _topLevelGroups(List<WorkItem> topLevel) {
    if (topLevel.isEmpty) return const [];
    if (_spaces.length < 2) {
      return [BacklogGroup(parent: null, items: topLevel)];
    }
    return [
      for (final space in _spaces)
        if ([
              for (final item in topLevel)
                if (item.spaceId == space.id) item,
            ]
            case final items when items.isNotEmpty)
          BacklogGroup(parent: null, items: items, space: space),
    ];
  }

  static bool _sameSpaces(List<Space> a, List<Space> b) =>
      a.length == b.length &&
      a.indexed.every((entry) => entry.$2.id == b[entry.$1].id);

  static String _titleOf(BacklogGroup group) =>
      group.parent?.title.toLowerCase() ?? '';

  void _onItemsChanged() => notifyIfActive();

  /// Any save can move an item into or out of the Backlog (dates, status,
  /// parent), so the server is asked again once the current burst settles.
  void _onItemSaved(WorkItem before, WorkItem saved, WorkItemReload reload) {
    if (!_refreshing || _reloadScheduled) return;
    _reloadScheduled = true;
    scheduleMicrotask(() {
      _reloadScheduled = false;
      if (_spaces.isNotEmpty && _refreshing && !isDisposed) {
        unawaited(load(_spaces));
      }
    });
  }
}
