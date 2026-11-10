import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/widgets/custom_field_editor.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/roll_up.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/widgets/field_label.dart';

/// The item's Type's custom fields, each with its editor, and under a field
/// that rolls up its sub-items' total. Shows nothing when the Type defines
/// none.
class CustomFieldsSection extends StatelessWidget {
  const CustomFieldsSection({
    super.key,
    required this.fields,
    required this.values,
    required this.users,
    required this.rollUpFor,
    required this.onSave,
  });

  final List<CustomField> fields;

  /// The item's values by field id.
  final Map<String, Object?> values;
  final List<User> users;

  /// The sub-items' total of a field, or null when there's none to show.
  final RollUp? Function(String fieldId) rollUpFor;
  final Future<bool> Function(String fieldId, Object? value) onSave;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final field in fields) ...[
          FieldLabel(field.name),
          CustomFieldEditor(
            // Keyed by field so an editor's typed text never carries over to
            // another field after the Type changes.
            key: ValueKey(field.id),
            field: field,
            value: values[field.id],
            users: users,
            onSave: (value) => onSave(field.id, value),
          ),
          if (rollUpFor(field.id) case final rollUp?)
            Padding(
              padding: const EdgeInsets.only(top: 4),
              child: Text(
                'Sub-items total: ${rollUp.kind.formatAmount(rollUp.total)}',
                style: Theme.of(context).textTheme.bodySmall,
              ),
            ),
          const SizedBox(height: 16),
        ],
      ],
    );
  }
}
