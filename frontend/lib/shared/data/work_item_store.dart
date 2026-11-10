import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/shared/data/work_item_conflict.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_blocker.dart';

/// An optimistic change to a work item, applied over its stored copy.
typedef WorkItemEdit = WorkItem Function(WorkItem item);

/// Fetches the server's current copy of the work item [id].
typedef WorkItemReload = Future<WorkItem> Function(String id);

/// The app's one copy of each work item it has loaded, keyed by id and
/// carrying the item's version. Board, Hierarchy, Roadmap and the detail
/// dialog all read items from here and save through [write], so an edit
/// made in one shows in all and every save sends the version it was based
/// on.
@lazySingleton
class WorkItemStore extends ChangeNotifier {
  final Map<String, WorkItem> _confirmed = {};

  // Optimistic edits not yet confirmed by the server, oldest first. They're
  // applied over the confirmed item on every read rather than baked in, so
  // a failed edit is undone by dropping it, without undoing anything else.
  final Map<String, List<WorkItemEdit>> _pending = {};

  final Map<String, Future<void>> _queues = {};

  // Bumped by a conflict. A write queued under an older value was made
  // against the copy the conflict proved stale.
  final Map<String, int> _conflictCounts = {};
  final Map<String, WorkItemConflict> _lastConflicts = {};

  int _revision = 0;
  final Map<String, int> _savedAt = {};

  /// The item as it should be shown: the last server copy with any edits
  /// still being saved applied on top.
  WorkItem? operator [](String id) {
    final confirmed = _confirmed[id];
    if (confirmed == null) return null;
    var shown = confirmed;
    for (final edit in _pending[id] ?? const <WorkItemEdit>[]) {
      shown = edit(shown);
    }
    return shown;
  }

  /// Moves on with every saved write. Read it before starting a load and
  /// pass it to [putAll] as `asOf`.
  int get revision => _revision;

  /// Stores freshly loaded [items], replacing what was held for them —
  /// except items saved since [asOf] (a [revision] read when the load
  /// started), whose stored copy is newer than the loaded one.
  void putAll(Iterable<WorkItem> items, {required int asOf}) {
    for (final item in items) {
      final savedAt = _savedAt[item.id];
      if (savedAt != null && savedAt > asOf) continue;
      _confirmed[item.id] = item;
    }
    notifyListeners();
  }

  /// Forgets [id], e.g. once it has been deleted. Layouts drop an item they
  /// can no longer find here.
  void remove(String id) {
    _confirmed.remove(id);
    _pending.remove(id);
    _savedAt.remove(id);
    _conflictCounts.remove(id);
    _lastConflicts.remove(id);
    notifyListeners();
  }

  void _confirm(WorkItem item) {
    _confirmed[item.id] = item;
    _savedAt[item.id] = ++_revision;
  }

  /// Saves a change to the loaded item [id]. [optimistic], if given, is
  /// shown at once and dropped again if the save fails. [send] is called
  /// with the stored version to pass as `expectedVersion`, and the item it
  /// returns replaces the stored one.
  ///
  /// On a 409 the item is reloaded with [reload] and a [WorkItemConflict]
  /// thrown; writes to the same item already queued behind it fail the same
  /// way without being sent. A 409 for an item that turns out unchanged is
  /// a rule the change broke, and is rethrown as it is, like other failures.
  ///
  /// A save that moves the item's dates also reloads, in the background,
  /// the stored items it blocks: whether they conflict with it is worked
  /// out by the server and can change without their version changing.
  Future<WorkItem> write(
    String id, {
    WorkItemEdit? optimistic,
    required Future<WorkItem> Function(int expectedVersion) send,
    required WorkItemReload reload,
  }) {
    if (!_confirmed.containsKey(id)) {
      throw StateError('Work item $id is written before it was loaded.');
    }
    if (optimistic != null) {
      (_pending[id] ??= []).add(optimistic);
      notifyListeners();
    }
    final conflictCount = _conflictCounts[id] ?? 0;
    // Every write bumps the version, so a second write sent before the
    // first answered would carry a stale one and be rejected. Writes to one
    // item therefore wait for the one before; other items aren't held up.
    final result = (_queues[id] ?? Future<void>.value()).then(
      (_) => _send(id, optimistic, send, reload, conflictCount),
    );
    _queues[id] = result.then<void>((_) {}, onError: (Object _) {});
    return result;
  }

  Future<WorkItem> _send(
    String id,
    WorkItemEdit? optimistic,
    Future<WorkItem> Function(int expectedVersion) send,
    WorkItemReload reload,
    int conflictCount,
  ) async {
    final stored = _confirmed[id];
    if (stored == null) {
      _pending.remove(id);
      notifyListeners();
      throw const ApiException('This work item was deleted.', statusCode: 404);
    }
    if ((_conflictCounts[id] ?? 0) != conflictCount) {
      throw _lastConflicts[id]!;
    }
    try {
      final saved = await send(stored.version);
      _confirm(saved);
      if (_datesMoved(stored, saved)) unawaited(_reloadBlockedBy(id, reload));
      return saved;
    } on ApiException catch (e) {
      if (!e.isConflict) rethrow;
      throw await _reloadAfterConflict(id, e, reload, stored.version);
    } finally {
      _pending[id]?.remove(optimistic);
      notifyListeners();
    }
  }

  /// The [ApiException] to fail the write with: a [WorkItemConflict], or
  /// [rejection] itself when the item turns out not to have changed since
  /// [sentVersion]. The server also answers 409 when the change breaks a
  /// rule (a parent that would make a cycle), and that says nothing about
  /// the stored copy being stale.
  Future<ApiException> _reloadAfterConflict(
    String id,
    ApiException rejection,
    WorkItemReload reload,
    int sentVersion,
  ) async {
    final WorkItem latest;
    try {
      latest = await reload(id);
    } on Exception {
      return _markStale(id, WorkItemConflict(rejection.message, latest: null));
    }
    if (latest.version == sentVersion) return rejection;
    _confirm(latest);
    return _markStale(id, WorkItemConflict(rejection.message, latest: latest));
  }

  // Safe to do after the reload: the writes queued behind this one only
  // check for a conflict once this write has finished.
  WorkItemConflict _markStale(String id, WorkItemConflict conflict) {
    _conflictCounts[id] = (_conflictCounts[id] ?? 0) + 1;
    // Every edit still pending was made against the stale copy too; the
    // writes they belong to are rejected when their turn comes.
    _pending.remove(id);
    return _lastConflicts[id] = conflict;
  }

  static bool _datesMoved(WorkItem before, WorkItem after) =>
      before.startDate != after.startDate || before.endDate != after.endDate;

  /// Reloads the stored items [blockerId] blocks, so their
  /// [WorkItemBlocker.conflicts] reflect its new dates. Best effort: a
  /// failed reload leaves that item's badge stale until the next load,
  /// rather than failing a save that already succeeded.
  Future<void> _reloadBlockedBy(String blockerId, WorkItemReload reload) async {
    final blockedIds = [
      for (final item in _confirmed.values)
        if (item.blockedBy.any((blocker) => blocker.id == blockerId)) item.id,
    ];
    if (blockedIds.isEmpty) return;
    final asOf = _revision;
    final reloaded = await Future.wait([
      for (final id in blockedIds) _tryReload(id, reload),
    ]);
    putAll([
      for (final item in reloaded)
        if (item != null && _confirmed.containsKey(item.id)) item,
    ], asOf: asOf);
  }

  static Future<WorkItem?> _tryReload(String id, WorkItemReload reload) async {
    try {
      return await reload(id);
    } on Exception {
      return null;
    }
  }
}
