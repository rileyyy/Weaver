import 'package:weaver/shared/models/custom_field.dart';

/// One roll-up field on a Hierarchy row: the item's own value and its
/// sub-items' total, kept apart. At least one of them is set.
class HierarchyRollUp {
  const HierarchyRollUp({
    required this.field,
    required this.ownValue,
    required this.total,
  });

  final CustomField field;
  final num? ownValue;

  /// The sum over every sub-item at any depth, not counting [ownValue].
  final num? total;
}
