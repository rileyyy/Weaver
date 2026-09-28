import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/models/admin_status.dart';
import 'package:weaver/features/admin/models/status_category.dart';
import 'package:weaver/features/admin/models/status_details.dart';
import 'package:weaver/features/admin/widgets/admin_form_dialog.dart';
import 'package:weaver/features/admin/widgets/dialog_submission.dart';
import 'package:weaver/features/admin/widgets/status_color_picker.dart';

final _hexColor = RegExp(r'^#[0-9a-fA-F]{6}$');

/// Adds a status, or edits [initial]. [onSave] returns null on success or
/// an error to show, keeping the dialog open. Completes with whether a save
/// succeeded.
Future<bool> showStatusDialog(
  BuildContext context, {
  required Future<String?> Function(StatusDetails details) onSave,
  AdminStatus? initial,
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (_) => _StatusDialog(initial: initial, onSave: onSave),
    ) ??
    false;

class _StatusDialog extends StatefulWidget {
  const _StatusDialog({required this.initial, required this.onSave});

  final AdminStatus? initial;
  final Future<String?> Function(StatusDetails details) onSave;

  @override
  State<_StatusDialog> createState() => _StatusDialogState();
}

class _StatusDialogState extends State<_StatusDialog>
    with DialogSubmission<_StatusDialog> {
  late final TextEditingController _name = TextEditingController(
    text: widget.initial?.name,
  );
  late final TextEditingController _color = TextEditingController(
    text: widget.initial?.color ?? '#1E88E5',
  );
  late StatusCategory _category =
      widget.initial?.category ?? StatusCategory.toDo;

  @override
  void dispose() {
    _name.dispose();
    _color.dispose();
    super.dispose();
  }

  bool get _colorIsValid => _hexColor.hasMatch(_color.text.trim());

  bool get _canSave => _name.text.trim().isNotEmpty && _colorIsValid;

  void _submit() {
    if (!_canSave) return;
    final details = StatusDetails(
      name: _name.text.trim(),
      category: _category,
      color: _color.text.trim().toUpperCase(),
    );
    unawaited(runSubmission(() => widget.onSave(details)));
  }

  @override
  Widget build(BuildContext context) {
    final isNew = widget.initial == null;
    return AdminFormDialog(
      title: isNew ? 'Add status' : 'Edit status',
      submitLabel: isNew ? 'Add' : 'Save',
      isSaving: isSaving,
      error:
          error ??
          (_color.text.trim().isEmpty || _colorIsValid
              ? null
              : 'Colour must look like #1E88E5.'),
      onSubmit: _canSave ? _submit : null,
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _name,
            autofocus: isNew,
            enabled: !isSaving,
            decoration: const InputDecoration(labelText: 'Name'),
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: 16),
          DropdownButtonFormField<StatusCategory>(
            initialValue: _category,
            decoration: const InputDecoration(
              labelText: 'Category',
              helperText: 'What the status means, whatever it is called.',
            ),
            items: [
              for (final category in StatusCategory.values)
                DropdownMenuItem(value: category, child: Text(category.label)),
            ],
            onChanged: isSaving
                ? null
                : (category) => setState(() => _category = category!),
          ),
          const SizedBox(height: 16),
          StatusColorPicker(
            controller: _color,
            enabled: !isSaving,
            onChanged: () => setState(() {}),
          ),
        ],
      ),
    );
  }
}
