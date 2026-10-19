import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';
import 'package:weaver/features/admin/widgets/admin_form_dialog.dart';
import 'package:weaver/features/admin/widgets/dialog_submission.dart';
import 'package:weaver/features/admin/widgets/hex_color_picker.dart';

/// A layer's colour when the admin hasn't picked one: the neutral colour
/// the server gives layers added before layers had colours.
const String defaultLayerColor = '#546E7A';

/// Adds a layer, or edits [initial]. [onSave] gets the trimmed name and an
/// upper-case `#RRGGBB` colour, and returns null on success or an error to
/// show, keeping the dialog open. Completes with whether a save succeeded.
Future<bool> showLayerDialog(
  BuildContext context, {
  required Future<String?> Function(String name, String color) onSave,
  LookupValue? initial,
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (_) => _LayerDialog(initial: initial, onSave: onSave),
    ) ??
    false;

class _LayerDialog extends StatefulWidget {
  const _LayerDialog({required this.initial, required this.onSave});

  final LookupValue? initial;
  final Future<String?> Function(String name, String color) onSave;

  @override
  State<_LayerDialog> createState() => _LayerDialogState();
}

class _LayerDialogState extends State<_LayerDialog>
    with DialogSubmission<_LayerDialog> {
  late final TextEditingController _name = TextEditingController(
    text: widget.initial?.name,
  );
  late final TextEditingController _color = TextEditingController(
    text: widget.initial?.color ?? defaultLayerColor,
  );

  @override
  void dispose() {
    _name.dispose();
    _color.dispose();
    super.dispose();
  }

  bool get _colorIsValid => HexColorPicker.isValid(_color.text);

  bool get _canSave => _name.text.trim().isNotEmpty && _colorIsValid;

  void _submit() {
    if (!_canSave) return;
    final name = _name.text.trim();
    final color = _color.text.trim().toUpperCase();
    unawaited(runSubmission(() => widget.onSave(name, color)));
  }

  @override
  Widget build(BuildContext context) {
    final isNew = widget.initial == null;
    return AdminFormDialog(
      title: isNew ? 'Add layer' : 'Edit layer',
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
          HexColorPicker(
            controller: _color,
            enabled: !isSaving,
            onChanged: () => setState(() {}),
          ),
        ],
      ),
    );
  }
}
