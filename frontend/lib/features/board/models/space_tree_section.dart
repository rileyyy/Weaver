import 'package:weaver/features/board/models/hierarchy_node.dart';
import 'package:weaver/shared/models/space.dart';

/// One Space's trees, for the Hierarchy and Roadmap while they show several
/// Spaces.
class SpaceTreeSection {
  const SpaceTreeSection({required this.space, required this.roots});

  final Space space;
  final List<HierarchyNode> roots;
}
