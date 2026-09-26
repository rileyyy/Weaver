import 'package:weaver/features/board/models/hierarchy_item.dart';

/// One row of a flattened [HierarchyNode] tree, paired with how deep it sits,
/// so the Hierarchy and Roadmap views can render the tree with a linear
/// [ListView.builder].
class FlattenedTreeRow {
  const FlattenedTreeRow({required this.node, required this.depth});

  final HierarchyNode node;
  final int depth;
}
