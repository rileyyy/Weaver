import 'package:flutter/foundation.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_query.dart';

/// The ids of the items the server matched for the latest question asked
/// of it (ADR-0006). Each new question supersedes the ones before, so a
/// slower answer to an older question, e.g. a search typed further since,
/// is dropped rather than shown.
class ServerMatches {
  ServerMatches(this._ask);

  final Future<List<WorkItem>> Function(WorkItemQuery query) _ask;

  Set<String>? _ids;
  int _generation = 0;

  /// Null until a question has been answered, or after [settle] with null.
  Set<String>? get ids => _ids;

  /// Asks [query]. Returns whether the answer changed [ids]; false too when
  /// a newer question superseded it. A failure is rethrown, unless
  /// superseded, and leaves [ids] as they were.
  Future<bool> refresh(WorkItemQuery query) async {
    final generation = ++_generation;
    final List<WorkItem> matched;
    try {
      matched = await _ask(query);
    } on Object {
      if (generation != _generation) return false;
      rethrow;
    }
    if (generation != _generation) return false;
    return _set({for (final item in matched) item.id});
  }

  /// Sets [ids] to an answer known without asking, superseding any question
  /// still on its way. Returns whether that changed them.
  bool settle(Set<String>? ids) {
    _generation++;
    return _set(ids);
  }

  bool _set(Set<String>? ids) {
    // Every edit asks again, and the answer is usually the same. Reporting
    // a change then would redraw the whole board.
    if (ids == null ? _ids == null : setEquals(ids, _ids)) return false;
    _ids = ids;
    return true;
  }
}
