import 'package:weaver/features/board/models/hierarchy_item.dart';

/// A [HierarchyItem] together with its own children, already filtered and
/// ordered — the shape the Hierarchy view renders directly.
class HierarchyNode {
  const HierarchyNode({required this.item, required this.children});

  final HierarchyItem item;
  final List<HierarchyNode> children;
}
