import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/state/hierarchy_tree_builder.dart';

HierarchyItem _item(String id, String? parentId, {String? title}) =>
    HierarchyItem(
      id: id,
      number: 1,
      parentId: parentId,
      title: title ?? id,
      statusId: 'todo',
    );

void main() {
  final items = [
    _item('root', null),
    _item('b', 'root', title: 'B'),
    _item('a', 'root', title: 'A'),
    _item('leaf', 'a'),
  ];

  test('nests items under their parents, keeping input order by default', () {
    final roots = buildHierarchyTree(items, isVisible: (_) => true);

    expect(roots.single.item.id, 'root');
    expect(roots.single.children.map((n) => n.item.id), ['b', 'a']);
    expect(roots.single.children[1].children.single.item.id, 'leaf');
  });

  test('sorts each sibling group without flattening the tree', () {
    final roots = buildHierarchyTree(
      items,
      isVisible: (_) => true,
      comparator: (x, y) => x.title.compareTo(y.title),
    );

    expect(roots.single.children.map((n) => n.item.id), ['a', 'b']);
  });

  test('keeps the ancestors of a visible item and drops everything else', () {
    final roots = buildHierarchyTree(
      items,
      isVisible: (item) => item.id == 'leaf',
    );

    expect(roots.single.item.id, 'root');
    expect(roots.single.children.single.item.id, 'a');
    expect(roots.single.children.single.children.single.item.id, 'leaf');
  });
}
