import 'package:weaver/shared/models/work_item_status.dart';

/// The configured work-item statuses, shared by every feature.
abstract class StatusRepository {
  Future<List<WorkItemStatus>> loadStatuses();
}
