import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/shared/models/work_item.dart';

/// A write to a work item was rejected (409) because someone else changed
/// it first. By the time this is thrown `WorkItemStore` has already tried
/// to reload the item: [latest] is what it now holds, or null if that
/// reload failed and the stale copy is still shown.
class WorkItemConflict extends ApiException {
  const WorkItemConflict(super.message, {required this.latest})
    : super(statusCode: 409);

  final WorkItem? latest;
}
