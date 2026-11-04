import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/models/custom_field_option_draft.dart';
import 'package:weaver/features/admin/widgets/admin_form_dialog.dart';
import 'package:weaver/features/admin/widgets/dialog_submission.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/custom_field_kind.dart';

/// Adds a custom field, or edits [initial]. The kind is only chosen when
/// adding: the server fixes it, since stored values have its shape. A select
/// field also edits its options, in order. [onSave] gets the trimmed name,
/// the kind and the options, and returns null on success or an error to
/// show, keeping the dialog open. Completes with whether a save succeeded.
Future<bool> showCustomFieldDialog(
  BuildContext context, {
  required Future<String?> Function(
    String name,
    CustomFieldKind kind,
    List<CustomFieldOptionDraft> options,
  )
  onSave,
  CustomField? initial,
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (_) => _CustomFieldDialog(initial: initial, onSave: onSave),
    ) ??
    false;

class _CustomFieldDialog extends StatefulWidget {
  const _CustomFieldDialog({required this.initial, required this.onSave});

  final CustomField? initial;
  final Future<String?> Function(
    String name,
    CustomFieldKind kind,
    List<CustomFieldOptionDraft> options,
  )
  onSave;

  @override
  State<_CustomFieldDialog> createState() => _CustomFieldDialogState();
}

class _CustomFieldDialogState extends State<_CustomFieldDialog>
    with DialogSubmission<_CustomFieldDialog> {
  late final TextEditingController _name = TextEditingController(
    text: widget.initial?.name,
  );
  late CustomFieldKind _kind = widget.initial?.kind ?? CustomFieldKind.text;

  /// Each option's id (null for one added here) and its name being edited.
  late final List<({String? id, TextEditingController name})> _options = [
    for (final option in widget.initial?.options ?? const [])
      (id: option.id, name: TextEditingController(text: option.name)),
  ];

  bool get _isNew => widget.initial == null;

  bool get _removesAnOption {
    final kept = {for (final option in _options) option.id};
    return widget.initial?.options.any((o) => !kept.contains(o.id)) ?? false;
  }

  bool get _canSave =>
      _name.text.trim().isNotEmpty &&
      (!_kind.isSelect || _options.every((o) => o.name.text.trim().isNotEmpty));

  @override
  void dispose() {
    _name.dispose();
    for (final option in _options) {
      option.name.dispose();
    }
    super.dispose();
  }

  void _addOption() =>
      setState(() => _options.add((id: null, name: TextEditingController())));

  void _moveUp(int index) =>
      setState(() => _options.insert(index - 1, _options.removeAt(index)));

  void _removeOption(int index) =>
      setState(() => _options.removeAt(index).name.dispose());

  void _submit() {
    if (!_canSave) return;
    final options = _kind.isSelect
        ? [
            for (final option in _options)
              CustomFieldOptionDraft(
                id: option.id,
                name: option.name.text.trim(),
              ),
          ]
        : const <CustomFieldOptionDraft>[];
    final name = _name.text.trim();
    unawaited(runSubmission(() => widget.onSave(name, _kind, options)));
  }

  @override
  Widget build(BuildContext context) {
    return AdminFormDialog(
      title: _isNew ? 'Add field' : 'Edit field',
      submitLabel: _isNew ? 'Add' : 'Save',
      isSaving: isSaving,
      error: error,
      onSubmit: _canSave ? _submit : null,
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _name,
            autofocus: _isNew,
            enabled: !isSaving,
            decoration: const InputDecoration(labelText: 'Name'),
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: 16),
          DropdownButtonFormField<CustomFieldKind>(
            initialValue: _kind,
            decoration: InputDecoration(
              labelText: 'Kind',
              helperText: _isNew ? null : 'A field’s kind can’t be changed.',
            ),
            items: [
              for (final kind in CustomFieldKind.values)
                DropdownMenuItem(value: kind, child: Text(kind.label)),
            ],
            onChanged: _isNew && !isSaving
                ? (kind) => setState(() => _kind = kind ?? _kind)
                : null,
          ),
          if (_kind.isSelect) ..._optionEditor(context),
        ],
      ),
    );
  }

  List<Widget> _optionEditor(BuildContext context) => [
    const SizedBox(height: 16),
    Text('Options', style: Theme.of(context).textTheme.labelLarge),
    for (final (index, option) in _options.indexed)
      Row(
        children: [
          Expanded(
            child: TextField(
              controller: option.name,
              enabled: !isSaving,
              decoration: InputDecoration(hintText: 'Option ${index + 1}'),
              onChanged: (_) => setState(() {}),
            ),
          ),
          IconButton(
            icon: const Icon(Icons.arrow_upward),
            tooltip: 'Move option up',
            onPressed: isSaving || index == 0 ? null : () => _moveUp(index),
          ),
          IconButton(
            icon: const Icon(Icons.remove_circle_outline),
            tooltip: 'Remove option',
            onPressed: isSaving ? null : () => _removeOption(index),
          ),
        ],
      ),
    Align(
      alignment: Alignment.centerLeft,
      child: TextButton.icon(
        icon: const Icon(Icons.add),
        label: const Text('Add option'),
        onPressed: isSaving ? null : _addOption,
      ),
    ),
    if (_removesAnOption)
      Text(
        'Items that chose a removed option lose that choice.',
        style: TextStyle(color: Theme.of(context).colorScheme.error),
      ),
  ];
}
