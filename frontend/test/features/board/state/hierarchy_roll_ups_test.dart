import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/state/hierarchy_roll_ups.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';

const _budget = CustomField(
  id: 'field-budget',
  typeId: 'type-room',
  name: 'Budget',
  kind: CustomFieldKind.currency,
  order: 0,
  rollsUp: true,
);

const _hours = CustomField(
  id: 'field-hours',
  typeId: 'type-task',
  name: 'Hours',
  kind: CustomFieldKind.number,
  order: 0,
  rollsUp: true,
);

const _notes = CustomField(
  id: 'field-size',
  typeId: 'type-room',
  name: 'Size',
  kind: CustomFieldKind.number,
  order: 1,
);

HierarchyItem _item(
  String id,
  String? parentId, [
  Map<String, Object?> fields = const {},
]) => HierarchyItem(
  id: id,
  number: 1,
  parentId: parentId,
  title: id,
  statusId: 'todo',
  fields: fields,
);

void main() {
  final rollUps = HierarchyRollUps(
    [
      _item('house', null, {'field-budget': 1000}),
      _item('kitchen', 'house', {'field-budget': 200}),
      _item('tiling', 'kitchen', {'field-hours': 4, 'field-size': 9}),
      _item('sink', 'kitchen', {'field-budget': 50.5}),
      _item('shed', null, {'field-size': 3}),
    ],
    const [_budget, _hours, _notes],
  );

  test("sums each field over every descendant, apart from the item's own "
      'value', () {
    final house = rollUps.forItem('house');

    expect(house.map((r) => (r.field.id, r.ownValue, r.total)), [
      ('field-budget', 1000, 250.5),
      ('field-hours', null, 4),
    ]);
  });

  test('shows an own value without a total on an item with no sub-items '
      'holding it', () {
    expect(
      rollUps.forItem('sink').map((r) => (r.field.id, r.ownValue, r.total)),
      [('field-budget', 50.5, null)],
    );
  });

  test("leaves out fields that don't roll up", () {
    expect(rollUps.forItem('shed'), isEmpty);
    expect(rollUps.forItem('tiling').map((r) => r.field.id), ['field-hours']);
  });

  test('is empty without roll-up fields', () {
    expect(HierarchyRollUps.empty.forItem('house'), isEmpty);
    expect(HierarchyRollUps.empty.isEmpty, isTrue);
    expect(rollUps.isEmpty, isFalse);
  });

  test('ends on a corrupt tree with a cycle', () {
    final cyclic = HierarchyRollUps(
      [
        _item('a', 'b', {'field-hours': 1}),
        _item('b', 'a', {'field-hours': 2}),
      ],
      const [_hours],
    );

    expect(cyclic.forItem('a').single.total, 2);
  });
}
