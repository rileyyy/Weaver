import 'dart:async';

import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/data/backlog_repository.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_query.dart';

import '../../../shared/fakes/work_items.dart';

/// Holds work items in memory and answers the Backlog query the way the
/// server does for these tests: every item of the Space with no dates,
/// except the ids in [done].
class FakeBacklogRepository implements BacklogRepository {
  FakeBacklogRepository(Iterable<WorkItem> items) {
    for (final item in items) {
      server[item.id] = item;
    }
  }

  final Map<String, WorkItem> server = {};
  final Set<String> done = {};
  final List<WorkItemQuery> queries = [];
  final List<String> itemLoads = [];

  /// The `expectedVersion` each reschedule was sent with, as `'<id>@<v>'`.
  final List<String> reschedules = [];

  /// When set, queries fail with it.
  Exception? queryError;

  /// When set, reschedules fail with it.
  Exception? rescheduleError;

  /// When non-null, each query parks a completer here instead of answering.
  List<Completer<List<WorkItem>>>? pendingQueries;

  @override
  Future<List<WorkItem>> queryItems(WorkItemQuery query) async {
    queries.add(query);
    final pending = pendingQueries;
    if (pending != null) {
      final answer = Completer<List<WorkItem>>();
      pending.add(answer);
      return answer.future;
    }
    if (queryError case final error?) throw error;
    return [
      for (final item in server.values)
        if (item.spaceId == query.spaceId &&
            item.startDate == null &&
            item.endDate == null &&
            !done.contains(item.id))
          item,
    ];
  }

  @override
  Future<WorkItem> getItem(String id) async {
    itemLoads.add(id);
    return server[id] ?? (throw const ApiException('Gone', statusCode: 404));
  }

  @override
  Future<WorkItem> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  }) async {
    reschedules.add('$itemId@$expectedVersion');
    if (rescheduleError case final error?) throw error;
    final current = server[itemId]!;
    return server[itemId] = withVersion(
      current.rescheduled(startDate, endDate),
      current.version + 1,
    );
  }
}
