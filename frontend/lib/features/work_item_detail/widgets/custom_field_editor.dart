import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/work_item_detail/widgets/custom_field_date_editor.dart';
import 'package:weaver/features/work_item_detail/widgets/custom_field_text_editor.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/widgets/user_name_label.dart';

/// The editor for one custom field, chosen by its kind. Each saves on its
/// own as soon as it changes (text-like kinds when they lose focus).
class CustomFieldEditor extends StatelessWidget {
  const CustomFieldEditor({
    super.key,
    required this.field,
    required this.value,
    required this.users,
    required this.onSave,
  });

  final CustomField field;

  /// The stored value as the server sends it, or null.
  final Object? value;

  /// Who a user field can name.
  final List<User> users;

  /// Called with the new value (null clears it); returns whether it saved.
  final Future<bool> Function(Object? value) onSave;

  @override
  Widget build(BuildContext context) => switch (field.kind) {
    CustomFieldKind.text ||
    CustomFieldKind.markdown ||
    CustomFieldKind.number ||
    CustomFieldKind.currency ||
    CustomFieldKind.url => CustomFieldTextEditor(
      kind: field.kind,
      value: value,
      onSave: onSave,
    ),
    CustomFieldKind.date => CustomFieldDateEditor(value: value, onSave: onSave),
    CustomFieldKind.checkbox => Align(
      alignment: Alignment.centerLeft,
      child: Checkbox(
        value: value == true,
        semanticLabel: field.name,
        onChanged: (checked) => unawaited(onSave(checked ?? false)),
      ),
    ),
    // Keyed by the stored value so a saved or reloaded value replaces the
    // one the dropdown was first given.
    CustomFieldKind.singleSelect => DropdownButtonFormField<String?>(
      key: ValueKey(value),
      initialValue: _knownOption(value),
      decoration: const InputDecoration(),
      items: [
        const DropdownMenuItem(value: null, child: Text('None')),
        for (final option in field.options)
          DropdownMenuItem(value: option.id, child: Text(option.name)),
      ],
      onChanged: (optionId) => unawaited(onSave(optionId)),
    ),
    CustomFieldKind.multiSelect => _multiSelect(),
    CustomFieldKind.user => DropdownButtonFormField<String?>(
      key: ValueKey(value),
      initialValue: users.any((u) => u.id == value) ? value as String? : null,
      decoration: const InputDecoration(),
      items: [
        const DropdownMenuItem(value: null, child: Text('None')),
        for (final user in users)
          DropdownMenuItem(
            value: user.id,
            child: UserNameLabel(user: user),
          ),
      ],
      onChanged: (userId) => unawaited(onSave(userId)),
    ),
  };

  String? _knownOption(Object? value) =>
      value is String && field.optionName(value) != null ? value : null;

  Widget _multiSelect() {
    final chosen = {
      for (final id in value as List<dynamic>? ?? const []) id as String,
    };
    return Wrap(
      spacing: 6,
      runSpacing: 6,
      children: [
        for (final option in field.options)
          FilterChip(
            label: Text(option.name),
            selected: chosen.contains(option.id),
            visualDensity: VisualDensity.compact,
            onSelected: (selected) {
              final next = [
                for (final other in field.options)
                  if (other.id == option.id
                      ? selected
                      : chosen.contains(other.id))
                    other.id,
              ];
              unawaited(onSave(next.isEmpty ? null : next));
            },
          ),
      ],
    );
  }
}
