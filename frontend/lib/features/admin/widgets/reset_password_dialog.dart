import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/admin/widgets/admin_form_dialog.dart';
import 'package:weaver/features/admin/widgets/dialog_submission.dart';
import 'package:weaver/features/admin/widgets/password_fields.dart';

/// Sets a new password for [username]. [onReset] returns null on success or
/// an error to show, keeping the dialog open. Completes with whether the
/// password was changed.
Future<bool> showResetPasswordDialog(
  BuildContext context, {
  required String username,
  required Future<String?> Function(String password) onReset,
}) async =>
    await showDialog<bool>(
      context: context,
      builder: (_) =>
          _ResetPasswordDialog(username: username, onReset: onReset),
    ) ??
    false;

class _ResetPasswordDialog extends StatefulWidget {
  const _ResetPasswordDialog({required this.username, required this.onReset});

  final String username;
  final Future<String?> Function(String password) onReset;

  @override
  State<_ResetPasswordDialog> createState() => _ResetPasswordDialogState();
}

class _ResetPasswordDialogState extends State<_ResetPasswordDialog>
    with DialogSubmission<_ResetPasswordDialog> {
  final _password = TextEditingController();
  final _confirmation = TextEditingController();

  @override
  void dispose() {
    _password.dispose();
    _confirmation.dispose();
    super.dispose();
  }

  void _submit() {
    if (!passwordsReady(_password, _confirmation)) return;
    unawaited(runSubmission(() => widget.onReset(_password.text)));
  }

  @override
  Widget build(BuildContext context) {
    return AdminFormDialog(
      title: 'Reset password for ${widget.username}',
      submitLabel: 'Reset password',
      isSaving: isSaving,
      error: error ?? passwordMismatchError(_password, _confirmation),
      onSubmit: passwordsReady(_password, _confirmation) ? _submit : null,
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'This also unlocks the account and signs it out on every device.',
          ),
          const SizedBox(height: 12),
          PasswordFields(
            password: _password,
            confirmation: _confirmation,
            enabled: !isSaving,
            onChanged: () => setState(() {}),
          ),
        ],
      ),
    );
  }
}
