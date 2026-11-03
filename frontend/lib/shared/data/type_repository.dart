import 'package:weaver/shared/models/work_item_type.dart';

/// The configured work-item types, shared by every feature.
abstract class TypeRepository {
  Future<List<WorkItemType>> loadTypes();

  /// Drops the cached list, so the next [loadTypes] fetches it again.
  /// Called after an admin changes the types.
  void invalidate();
}
