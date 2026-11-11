import 'package:weaver/features/board/models/flattened_tree_row.dart';
import 'package:weaver/features/board/models/hierarchy_node.dart';
import 'package:weaver/features/board/models/space_header_row.dart';
import 'package:weaver/features/board/models/space_tree_section.dart';
import 'package:weaver/features/board/models/tree_list_row.dart';

/// Depth-first rows for [roots], skipping the children of any node whose id
/// is in [collapsedIds]. Shared by the Hierarchy and Roadmap views so their
/// indentation and collapse behaviour stay identical.
List<FlattenedTreeRow> flattenTree(
  List<HierarchyNode> roots, {
  Set<String> collapsedIds = const {},
}) {
  final rows = <FlattenedTreeRow>[];
  void visit(List<HierarchyNode> nodes, int depth) {
    for (final node in nodes) {
      rows.add(FlattenedTreeRow(node: node, depth: depth));
      if (!collapsedIds.contains(node.item.id)) {
        visit(node.children, depth + 1);
      }
    }
  }

  visit(roots, 0);
  return rows;
}

/// [flattenTree] for each of [sections], each under a heading naming its
/// Space.
List<TreeListRow> flattenSections(
  List<SpaceTreeSection> sections, {
  Set<String> collapsedIds = const {},
}) => [
  for (final section in sections) ...[
    SpaceHeaderRow(section.space),
    ...flattenTree(section.roots, collapsedIds: collapsedIds),
  ],
];
