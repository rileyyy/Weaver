import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/widgets/admin_form_dialog.dart';
import 'package:weaver/features/admin/widgets/dialog_submission.dart';

/// Asks for one name: adding or renaming a priority or layer, or renaming a
/// user. [onSave] returns null on success or an error to show, keeping the
/// dialog open. Completes with whether a save succeeded.
Future<bool> showNameDialog(
  BuildContext context, {
  required String title,
  required String label,
  required String submitLabel,
  required Future<String?> Function(String name) onSave,
  String initialName = '',
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (_) => _NameDialog(
        title: title,
        label: label,
        submitLabel: submitLabel,
        initialName: initialName,
        onSave: onSave,
      ),
    ) ??
    false;

class _NameDialog extends StatefulWidget {
  const _NameDialog({
    required this.title,
    required this.label,
    required this.submitLabel,
    required this.initialName,
    required this.onSave,
  });

  final String title;
  final String label;
  final String submitLabel;
  final String initialName;
  final Future<String?> Function(String name) onSave;

  @override
  State<_NameDialog> createState() => _NameDialogState();
}

class _NameDialogState extends State<_NameDialog>
    with DialogSubmission<_NameDialog> {
  late final TextEditingController _name = TextEditingController(
    text: widget.initialName,
  );

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  bool get _canSave {
    final name = _name.text.trim();
    return name.isNotEmpty && name != widget.initialName;
  }

  void _submit() {
    if (!_canSave) return;
    unawaited(runSubmission(() => widget.onSave(_name.text.trim())));
  }

  @override
  Widget build(BuildContext context) {
    return AdminFormDialog(
      title: widget.title,
      submitLabel: widget.submitLabel,
      isSaving: isSaving,
      error: error,
      onSubmit: _canSave ? _submit : null,
      content: TextField(
        controller: _name,
        autofocus: true,
        enabled: !isSaving,
        decoration: InputDecoration(labelText: widget.label),
        onChanged: (_) => setState(() {}),
        onSubmitted: (_) => _submit(),
      ),
    );
  }
}
