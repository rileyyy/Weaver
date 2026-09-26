import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/state/flatten_tree.dart';

HierarchyNode _node(String id, [List<HierarchyNode> children = const []]) =>
    HierarchyNode(
      item: HierarchyItem(
        id: id,
        number: 1,
        parentId: null,
        title: id,
        statusId: 'todo',
      ),
      children: children,
    );

void main() {
  final roots = [
    _node('a', [
      _node('a1', [_node('a1x')]),
      _node('a2'),
    ]),
    _node('b'),
  ];

  test('lists nodes depth-first with their depth', () {
    final rows = flattenTree(roots);

    expect(rows.map((r) => (r.node.item.id, r.depth)), [
      ('a', 0),
      ('a1', 1),
      ('a1x', 2),
      ('a2', 1),
      ('b', 0),
    ]);
  });

  test('keeps a collapsed node but skips its descendants', () {
    final rows = flattenTree(roots, collapsedIds: {'a1'});

    expect(rows.map((r) => r.node.item.id), ['a', 'a1', 'a2', 'b']);
  });

  test('an empty tree has no rows', () {
    expect(flattenTree(const []), isEmpty);
  });
}
