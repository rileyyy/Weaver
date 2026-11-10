import 'package:weaver/shared/models/work_item.dart';

/// Backlog items that share a parent, in the order the server listed them.
class BacklogGroup {
  const BacklogGroup({required this.parent, required this.items});

  /// Null for the Space's top-level items.
  final WorkItem? parent;
  final List<WorkItem> items;
}
