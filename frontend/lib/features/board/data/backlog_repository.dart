import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_query.dart';

/// What the Backlog tab needs from the server: the query, single items for
/// the parents it groups under, and setting dates.
abstract class BacklogRepository {
  /// Every work item matching [query], across all of the server's pages, in
  /// Rank order.
  Future<List<WorkItem>> queryItems(WorkItemQuery query);

  /// One work item as it is now.
  Future<WorkItem> getItem(String id);

  /// Sets the work item identified by [itemId]'s scheduled start/end.
  /// Either may be null.
  Future<WorkItem> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate, {
    required int expectedVersion,
  });
}
