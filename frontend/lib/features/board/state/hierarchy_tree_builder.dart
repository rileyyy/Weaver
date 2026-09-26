import 'package:weaver/features/board/models/hierarchy_item.dart';

/// Nests a flat item list under each item's parent, top-level items first.
/// Each sibling group is sorted by [comparator] (sorting the flat list would
/// destroy the nesting). An item that isn't [isVisible] is still kept when a
/// descendant is, so a match's ancestors show where it sits in the tree.
List<HierarchyNode> buildHierarchyTree(
  List<HierarchyItem> items, {
  required bool Function(HierarchyItem item) isVisible,
  Comparator<HierarchyItem>? comparator,
}) {
  final byParent = <String?, List<HierarchyItem>>{};
  for (final item in items) {
    (byParent[item.parentId] ??= []).add(item);
  }

  List<HierarchyNode> buildLevel(String? parentId) {
    final children = byParent[parentId] ?? const [];
    final ordered = comparator == null
        ? children
        : ([...children]..sort(comparator));

    return [
      for (final item in ordered)
        if (buildLevel(item.id) case final childNodes
            when isVisible(item) || childNodes.isNotEmpty)
          HierarchyNode(item: item, children: childNodes),
    ];
  }

  return buildLevel(null);
}
