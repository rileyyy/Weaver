import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/work_item.dart';

/// Backlog items that share a parent, in the order the server listed them.
class BacklogGroup {
  const BacklogGroup({required this.parent, required this.items, this.space});

  /// Null for a Space's top-level items.
  final WorkItem? parent;
  final List<WorkItem> items;

  /// Whose top-level items these are, while the Backlog shows several
  /// Spaces; null otherwise.
  final Space? space;
}
