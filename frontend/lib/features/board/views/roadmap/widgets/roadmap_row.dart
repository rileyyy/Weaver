import 'package:weaver/features/board/models/hierarchy_item.dart';

/// One flattened row: a [HierarchyNode] paired with how deep it sits in the
/// (currently expanded) tree — the same flattening [HierarchyView] does, so
/// both views' collapse/indent behavior stays visually consistent.
class RoadmapRow {
  const RoadmapRow({required this.node, required this.depth});

  final HierarchyNode node;
  final int depth;
}
