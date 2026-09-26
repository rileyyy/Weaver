import 'package:weaver/features/board/models/hierarchy_item.dart';

/// One flattened row: a [HierarchyNode] paired with how deep it sits in the
/// (currently expanded) tree, for [ListView.builder] to render linearly.
class HierarchyRow {
  const HierarchyRow({required this.node, required this.depth});

  final HierarchyNode node;
  final int depth;
}
