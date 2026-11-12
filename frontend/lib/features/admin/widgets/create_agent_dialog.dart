import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/widgets/admin_form_dialog.dart';
import 'package:weaver/features/admin/widgets/dialog_submission.dart';
import 'package:weaver/features/admin/widgets/handle_field.dart';

/// What the add-agent dialog asks for; [onCreate] gets the fields as typed.
typedef CreateAgent =
    Future<String?> Function({
      required String name,
      required String description,
      required String handle,
    });

/// Adds an agent account. The name is free text (e.g. "Claude Code
/// (desktop)"), not held to the username rules. [onCreate] returns null on
/// success or an error to show, keeping the dialog open.
Future<bool> showCreateAgentDialog(
  BuildContext context, {
  required CreateAgent onCreate,
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (_) => _CreateAgentDialog(onCreate: onCreate),
    ) ??
    false;

class _CreateAgentDialog extends StatefulWidget {
  const _CreateAgentDialog({required this.onCreate});

  final CreateAgent onCreate;

  @override
  State<_CreateAgentDialog> createState() => _CreateAgentDialogState();
}

class _CreateAgentDialogState extends State<_CreateAgentDialog>
    with DialogSubmission<_CreateAgentDialog> {
  // The server's limits (User.MaxUsernameLength, MaxDescriptionLength).
  static const _maxNameLength = 50;
  static const _maxDescriptionLength = 500;

  final _name = TextEditingController();
  final _handle = TextEditingController();
  final _description = TextEditingController();

  @override
  void dispose() {
    _name.dispose();
    _handle.dispose();
    _description.dispose();
    super.dispose();
  }

  bool get _canCreate => _name.text.trim().isNotEmpty;

  void _submit() {
    if (!_canCreate) return;
    unawaited(
      runSubmission(
        () => widget.onCreate(
          name: _name.text,
          description: _description.text,
          handle: _handle.text,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return AdminFormDialog(
      title: 'Add agent',
      submitLabel: 'Add',
      isSaving: isSaving,
      error: error,
      onSubmit: _canCreate ? _submit : null,
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _name,
            autofocus: true,
            enabled: !isSaving,
            maxLength: _maxNameLength,
            decoration: const InputDecoration(
              labelText: 'Name',
              helperText:
                  'One per agent runtime, e.g. “Claude Code (desktop)”.',
            ),
            onChanged: (_) => setState(() {}),
          ),
          const SizedBox(height: 12),
          HandleField(
            controller: _handle,
            label: 'Handle (optional)',
            enabled: !isSaving,
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _description,
            enabled: !isSaving,
            maxLength: _maxDescriptionLength,
            minLines: 1,
            maxLines: 4,
            decoration: const InputDecoration(
              labelText: 'Description (optional)',
            ),
          ),
        ],
      ),
    );
  }
}
