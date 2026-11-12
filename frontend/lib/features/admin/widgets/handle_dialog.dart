import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/widgets/admin_form_dialog.dart';
import 'package:weaver/features/admin/widgets/dialog_submission.dart';
import 'package:weaver/features/admin/widgets/handle_field.dart';

/// Sets or clears an agent's `@` handle. Unlike a name, it may be saved
/// blank, which means the agent can't be mentioned. [onSave] returns null on
/// success or an error to show, keeping the dialog open.
Future<bool> showHandleDialog(
  BuildContext context, {
  required String agentName,
  required String? currentHandle,
  required Future<String?> Function(String handle) onSave,
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (_) => _HandleDialog(
        agentName: agentName,
        currentHandle: currentHandle ?? '',
        onSave: onSave,
      ),
    ) ??
    false;

class _HandleDialog extends StatefulWidget {
  const _HandleDialog({
    required this.agentName,
    required this.currentHandle,
    required this.onSave,
  });

  final String agentName;
  final String currentHandle;
  final Future<String?> Function(String handle) onSave;

  @override
  State<_HandleDialog> createState() => _HandleDialogState();
}

class _HandleDialogState extends State<_HandleDialog>
    with DialogSubmission<_HandleDialog> {
  late final TextEditingController _handle = TextEditingController(
    text: widget.currentHandle,
  );

  @override
  void dispose() {
    _handle.dispose();
    super.dispose();
  }

  bool get _hasChanged => _handle.text.trim() != widget.currentHandle;

  void _submit() {
    if (!_hasChanged) return;
    unawaited(runSubmission(() => widget.onSave(_handle.text)));
  }

  @override
  Widget build(BuildContext context) {
    return AdminFormDialog(
      title: 'Handle for ${widget.agentName}',
      submitLabel: 'Save',
      isSaving: isSaving,
      error: error,
      onSubmit: _hasChanged ? _submit : null,
      content: HandleField(
        controller: _handle,
        label: 'Handle',
        enabled: !isSaving,
        autofocus: true,
        onChanged: (_) => setState(() {}),
        onSubmitted: (_) => _submit(),
      ),
    );
  }
}
