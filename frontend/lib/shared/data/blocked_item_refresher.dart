import 'dart:async';

import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_blocker.dart';

/// Keeps the [WorkItemBlocker.conflicts] flags in [WorkItemStore] current
/// when the client itself changes something they depend on. The server
/// works conflicts out from the blocker's dates and Done-ness and from the
/// live blocks links, none of which bump the blocked item's version, so
/// the blocked item has to be fetched again.
///
/// Best effort throughout: a failed reload leaves that item's flag stale
/// until the next load, rather than failing a change that already
/// succeeded.
class BlockedItemRefresher {
  BlockedItemRefresher(this._store, this._reload);

  final WorkItemStore _store;
  final WorkItemReload _reload;

  /// Hooks into [store] so that every save which can change whether the
  /// items it blocks conflict with it reloads them in the background.
  static void attachTo(WorkItemStore store) {
    store.addSaveListener((before, saved, reload) {
      if (!changesConflicts(before, saved)) return;
      unawaited(
        BlockedItemRefresher(store, reload).refreshItemsBlockedBy(saved.id),
      );
    });
  }

  /// Whether saving [before] as [after] can change the conflicts of the
  /// items it blocks: its dates moved, or it entered or left Done. The
  /// server sets [WorkItem.completionDate] exactly while the item is Done.
  static bool changesConflicts(WorkItem before, WorkItem after) =>
      before.startDate != after.startDate ||
      before.endDate != after.endDate ||
      (before.completionDate == null) != (after.completionDate == null);

  /// Reloads the held items [blockerId] blocks.
  Future<void> refreshItemsBlockedBy(String blockerId) => refresh([
    for (final item in _store.loaded)
      if (item.blockedBy.any((blocker) => blocker.id == blockerId)) item.id,
  ]);

  /// Reloads those of [ids] the store holds; the rest are left unloaded.
  Future<void> refresh(Iterable<String> ids) async {
    final heldIds = [
      for (final id in ids)
        if (_store[id] != null) id,
    ];
    if (heldIds.isEmpty) return;
    final asOf = _store.revision;
    final reloaded = await Future.wait([
      for (final id in heldIds) _tryReload(id),
    ]);
    // An item removed while its reload was in flight (deleted) stays gone.
    _store.putAll([
      for (final item in reloaded)
        if (item != null && _store[item.id] != null) item,
    ], asOf: asOf);
  }

  Future<WorkItem?> _tryReload(String id) async {
    try {
      return await _reload(id);
    } on Exception {
      return null;
    }
  }
}
