import 'dart:math';

import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/models/hierarchy_roll_up.dart';
import 'package:weaver/shared/models/custom_field.dart';

/// The roll-ups of every item in a Space's tree, summed from the items the
/// Hierarchy already holds rather than asked of the server, so a value
/// edited anywhere moves its ancestors' totals at once. The server sums the
/// same way for the detail dialog and MCP (`RollUp.Sum`): each roll-up field
/// on its own, over every descendant at any depth, without the item's own
/// value. The Hierarchy holds no trashed items, so they're never counted.
class HierarchyRollUps {
  /// [items] must be the whole tree (filters hide rows, not sub-items'
  /// values); [fields] the Space's fields, of which only those that roll up
  /// count.
  factory HierarchyRollUps(
    Iterable<HierarchyItem> items,
    Iterable<CustomField> fields,
  ) {
    final rollUpFields = [
      for (final field in fields)
        if (field.rollsUp) field,
    ];
    if (rollUpFields.isEmpty) return empty;
    final byId = {for (final item in items) item.id: item};
    final totals = <String, Map<String, num>>{};
    final decimals = <String, int>{};
    for (final item in byId.values) {
      for (final field in rollUpFields) {
        if (item.fields[field.id] case final num value) {
          decimals[field.id] = max(decimals[field.id] ?? 0, _decimalsOf(value));
          for (final ancestorId in _ancestorIds(item, byId)) {
            final ancestorTotals = totals.putIfAbsent(ancestorId, () => {});
            ancestorTotals[field.id] = (ancestorTotals[field.id] ?? 0) + value;
          }
        }
      }
    }
    // Doubles drift (0.1 + 0.2 is 0.30000000000000004) where the server's
    // decimals don't; a sum never has more decimals than its widest value.
    for (final ancestorTotals in totals.values) {
      ancestorTotals.updateAll(
        (fieldId, total) =>
            num.parse(total.toStringAsFixed(decimals[fieldId] ?? 0)),
      );
    }
    return HierarchyRollUps._(byId, rollUpFields, totals);
  }

  /// How many decimals [value] was written with; a value only exponent
  /// notation can show keeps the most a double can hold.
  static int _decimalsOf(num value) {
    final text = value.toString();
    if (text.contains('e')) return 15;
    final point = text.indexOf('.');
    return point < 0 ? 0 : text.length - point - 1;
  }

  const HierarchyRollUps._(this._items, this._fields, this._totals);

  static const HierarchyRollUps empty = HierarchyRollUps._({}, [], {});

  final Map<String, HierarchyItem> _items;
  final List<CustomField> _fields;

  /// Item id to field id to the sub-items' total.
  final Map<String, Map<String, num>> _totals;

  /// Whether the Space has no field that rolls up, so there's nothing to
  /// show.
  bool get isEmpty => _fields.isEmpty;

  /// The item's roll-up fields that it or a sub-item has a value for, in
  /// field order.
  List<HierarchyRollUp> forItem(String itemId) {
    final ownValues = _items[itemId]?.fields ?? const {};
    final totals = _totals[itemId] ?? const {};
    final rollUps = <HierarchyRollUp>[];
    for (final field in _fields) {
      final ownValue = switch (ownValues[field.id]) {
        final num value => value,
        _ => null,
      };
      final total = totals[field.id];
      if (ownValue == null && total == null) continue;
      rollUps.add(
        HierarchyRollUp(field: field, ownValue: ownValue, total: total),
      );
    }
    return rollUps;
  }

  /// Stops at the top, or on revisiting an item, so a corrupt tree with a
  /// cycle still ends.
  static Iterable<String> _ancestorIds(
    HierarchyItem item,
    Map<String, HierarchyItem> byId,
  ) sync* {
    final seen = {item.id};
    var parentId = item.parentId;
    while (parentId != null && seen.add(parentId)) {
      yield parentId;
      parentId = byId[parentId]?.parentId;
    }
  }
}
