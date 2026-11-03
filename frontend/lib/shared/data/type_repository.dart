import 'package:weaver/shared/models/work_item_type.dart';

/// Each Space's work-item Types, shared by every feature.
abstract class TypeRepository {
  /// The Types of the Space [spaceId], in display order.
  Future<List<WorkItemType>> loadTypes(String spaceId);

  /// Drops every cached list, so the next [loadTypes] fetches again.
  /// Called after an admin changes the Types.
  void invalidate();
}
