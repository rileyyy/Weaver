import 'package:flutter/material.dart';
import 'package:weaver/shared/models/roll_up.dart';
import 'package:weaver/shared/widgets/field_label.dart';

/// Sub-items' totals of fields the item's own Type doesn't have, each named
/// with its Type, since two Types can each have a field of the same name.
/// Shows nothing without any.
class RollUpsSection extends StatelessWidget {
  const RollUpsSection({
    super.key,
    required this.rollUps,
    required this.typeNameFor,
  });

  final List<RollUp> rollUps;

  /// A Type's name, or null when it isn't known.
  final String? Function(String typeId) typeNameFor;

  @override
  Widget build(BuildContext context) {
    if (rollUps.isEmpty) return const SizedBox.shrink();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const FieldLabel('Totals of sub-items'),
        for (final rollUp in rollUps) Text(_line(rollUp)),
        const SizedBox(height: 16),
      ],
    );
  }

  String _line(RollUp rollUp) {
    final typeName = typeNameFor(rollUp.typeId);
    final name = typeName == null
        ? rollUp.fieldName
        : '${rollUp.fieldName} ($typeName)';
    return '$name: ${rollUp.kind.formatAmount(rollUp.total)}';
  }
}
