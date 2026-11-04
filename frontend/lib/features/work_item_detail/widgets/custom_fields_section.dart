import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/widgets/custom_field_editor.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/widgets/field_label.dart';

/// The item's Type's custom fields, each with its editor. Shows nothing
/// when the Type defines none.
class CustomFieldsSection extends StatelessWidget {
  const CustomFieldsSection({
    super.key,
    required this.fields,
    required this.values,
    required this.users,
    required this.onSave,
  });

  final List<CustomField> fields;

  /// The item's values by field id.
  final Map<String, Object?> values;
  final List<User> users;
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
          const SizedBox(height: 16),
        ],
      ],
    );
  }
}
